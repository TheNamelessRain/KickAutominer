using KickAutominer.Commands;
using KickAutominer.Models;
using Microsoft.Web.WebView2.Core;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace KickAutominer.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private CoreWebView2? _webViewCore;
        public CoreWebView2? WebViewCore
        {
            get => _webViewCore;
            set
            {
                Set(ref _webViewCore, value);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _farmstat = "Запустить фарм";
        public string FarmStat
        {
            get => _farmstat;
            set => Set(ref _farmstat, value);
        }

        private string _loaddropsbutton = "Загрузить список дропов";
        public string LoadDropsButton
        {
            get => _loaddropsbutton;
            set => Set(ref _loaddropsbutton, value);
        }

        private bool _mutebrowser = false;
        public bool MuteBrowser
        {
            get => _mutebrowser;
            set
            {
                Set(ref _mutebrowser, value);
                if (WebViewCore != null)
                    WebViewCore.IsMuted = _mutebrowser;
            }
        }

        public ObservableCollection<Reward> Rewards { get;  } = [];
        public RelayCommand LoadDropsCommand { get; }
        public RelayCommand StartFarmCommand { get; }

        private bool _farming;
        private CancellationTokenSource? _farmCTS;

        public MainViewModel()
        {
            LoadDropsCommand = new RelayCommand(async _ => await LoadDropsAsync(), CanLoadDrops);
            StartFarmCommand = new RelayCommand(async _ => await StartFarm(), CanStartFarm);
            //LoadRewards();
        }
        private void LoadRewards()
        {
            var reward1 = new Reward
            {
                RewarGroupdName = "Group 1",
                GameName = "Game A",
                IsSelected = true,
                Rewards = new List<Drop>
            {
                new Drop { Title = "Drop 1", RequiredMinutes = 10, WatchedMinutes = 5 },
                new Drop { Title = "Drop 2", RequiredMinutes = 15, WatchedMinutes = 5 },
                new Drop { Title = "Drop 3", RequiredMinutes = 20, WatchedMinutes = 5,Proc="15" }
            }
            };

            // Второй Reward с 2 дропами
            var reward2 = new Reward
            {
                RewarGroupdName = "Group 2",
                GameName = "Game B",
                IsSelected = false,
                Rewards = new List<Drop>
            {
                new Drop { Title = "Drop A", RequiredMinutes = 12, WatchedMinutes = 3 },
                new Drop { Title = "Drop B", RequiredMinutes = 8,  WatchedMinutes = 7 }
            }
            };

            Rewards.Add(reward1);
            Rewards.Add(reward2);
        }
        public async Task InitializeWebViewAsync()
        {
            if (WebViewCore != null)
            {
                WebViewCore.IsMuted = MuteBrowser;
                await Task.CompletedTask;
            }
        }

        private void ParseDrops(string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("data", out var data))
                return;

            List<string> AllChannels = [];
            foreach (var campaign in data.EnumerateArray())
            {
                Reward Reward = new();
                Reward.Rewards = [];

                foreach (var reward in campaign.GetProperty("rewards").EnumerateArray())
                {
                    Drop drop = new()
                    {
                        Title = reward.GetProperty("name").GetString(),
                        RequiredMinutes = reward.GetProperty("required_units").GetInt32(),
                        ImageUrl = reward.TryGetProperty("image_url", out var img)
                            ? "https://ext.cdn.kick.com/" + img.GetString()
                            : null
                    };


                    Reward.Rewards.Add(drop);
                }

                if (campaign.TryGetProperty("category", out var categories))
                {
                    if (categories.ValueKind == JsonValueKind.Object &&
                        categories.TryGetProperty("name", out var nameObj) &&
                        nameObj.ValueKind == JsonValueKind.String)
                    {
                        Reward.GameName = nameObj.GetString();
                    }
                }

                if (campaign.TryGetProperty("channels", out var channels)) 
                { 
                    foreach (var ch in channels.EnumerateArray()) 
                    { 
                        if (ch.TryGetProperty("user", out var u) && u.TryGetProperty("username", out var uname)) 
                        { 
                            string? name = uname.GetString(); 
                            if (!string.IsNullOrEmpty(name))
                            {
                                AllChannels.Add(name);
                                Reward.ChannelUsernames.Add(name);
                            }
                        } 
                    } 
                }

                Reward.RewarGroupdName = campaign.TryGetProperty("name", out var rewgrpname) &&
                                    rewgrpname.ValueKind == JsonValueKind.String ? rewgrpname.GetString() : null;

                List<Drop> SortedDrops = [.. Reward.Rewards.OrderBy(t => t.RequiredMinutes)];

                Reward.Rewards = SortedDrops;

                Rewards.Add(Reward);
            }

            foreach (Reward reward in Rewards)
            {
                if (reward.ChannelUsernames.Count == 0)
                    reward.ChannelUsernames = AllChannels;
            }
        }

        private bool CanLoadDrops(object? parameter) => WebViewCore != null;

        public async Task LoadDropsAsync()
        {
            Rewards.Clear();

            WebViewCore!.Navigate("https://web.kick.com/api/v1/drops/campaigns");

            await WaitForNavigationAsync();

            string DropsJson = await WebViewCore.ExecuteScriptAsync(
                                        "document.querySelector('pre')?.innerText"
                                    );

            DropsJson = (DropsJson[..^3] + "\"").Trim('"').Replace("\\\"", "\"");

            ParseDrops(DropsJson);

            WebViewCore!.Navigate("https://kick.com/drops/inventory");

            await WaitForNavigationAsync();

            string jsCode = @"(() => {
    const items = [...document.querySelectorAll(""main li"")];

    return JSON.stringify(
        items.map(li => {
            const divs = li.querySelectorAll(""div"");

            const firstSpan = divs[0]?.querySelector(""span"");
            const name = firstSpan?.textContent?.trim() ?? """";

            const firstDiv = divs[0]?.querySelectorAll(""div"")[1]; 
            let aria = firstDiv?.getAttribute(""aria-valuenow"");

            const lastSpan = divs[divs.length - 1]?.querySelector(""span"");
            let valText = lastSpan?.textContent?.trim() ?? """";

            let value = 0;
            if (valText.toLowerCase().includes(""полученные""))
                value = 1;
            else if (aria !== null && aria !== """") {
                aria = aria.replace(',', '.'); 
                value = parseFloat(aria);
                if (isNaN(value))
                    value = 0;
            }
            else {
                valText = valText.replace(',', '.'); 
                value = parseFloat(valText) / 100;
                if (isNaN(value))
                    value = 0;
            }

            return { name, value };
        })
    );
})();";

            string JsonReq = await WebViewCore.ExecuteScriptAsync(jsCode);

            int Rpt = 0;
            int MaxRpt = 5;
            while ((JsonReq == "null" || JsonReq == "\"[]\"") && MaxRpt > Rpt)
            {
                await Task.Delay(500);
                JsonReq = await WebViewCore.ExecuteScriptAsync(jsCode);
                Rpt++;
            }

            if (JsonReq.StartsWith('"'))
                JsonReq = JsonSerializer.Deserialize<string>(JsonReq);

            List<DropStatus>? JsonParseRes = JsonSerializer.Deserialize<List<DropStatus>>(JsonReq);

            if (JsonParseRes == null)
            {
                MessageBox.Show("Не удалось получить статусы дропов");
                return;
            }

            ParseDropsStatus(JsonParseRes);

            if (Rewards.Count > 0)
                LoadDropsButton = "Обновить список дропов";

            WebViewCore!.Navigate("https://kick.com");
        }

        private void ParseDropsStatus(List<DropStatus> DropStat)
        {
            foreach (Reward reward in Rewards)
            {
                foreach (Drop drop in reward.Rewards!)
                {
                    double Proc = DropStat.Where(d => d.Name.Equals(drop.Title)).Select(v => v.Value).FirstOrDefault();
                    drop.WatchedMinutes = drop.RequiredMinutes * Proc;
                    drop.Proc = (Proc * 100.0).ToString("F2") + "%";
                }
            }
            // мб сортировать как то)
        }

        private bool CanStartFarm(object? parameter) => Rewards.Any(d => d.IsSelected);

        private async Task StartFarm()
        {
            if (_farming)
            {
                FarmStat = "Запустить фарм";
                _farming = false;
                _farmCTS!.Cancel();
                WebViewCore!.Navigate("https://kick.com");
                return;
            }

            _farming = true;
            FarmStat = "Фарм идёт...";
            _farmCTS = new CancellationTokenSource();
            var ct = _farmCTS.Token;
            
            try
            {
                while (!ct.IsCancellationRequested &&
                       Rewards.Any(d => d.IsSelected))
                {
                    foreach (Reward reward in Rewards.Where(d => d.IsSelected))
                    {
                        if (reward.Rewards == null || reward.Rewards.Count == 0 || !reward.Rewards.Any(d => d.WatchedMinutes < d.RequiredMinutes))
                        {
                            reward.IsSelected = false;
                            continue;
                        }

                        foreach (string channel in reward.ChannelUsernames)
                        {
                            if (ct.IsCancellationRequested)
                                return;

                            bool isLive = await ChannelIsLive(channel, reward.GameName);
                            if (isLive)
                            {
                                await WatchOnChannel(reward, channel, ct);
                                if (reward.Rewards.Any(d => d.WatchedMinutes < d.RequiredMinutes))
                                    continue;
                                else
                                    break;
                            }
                        }

                        WebViewCore!.Navigate("https://kick.com");
                        await Task.Delay(1500);
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                _farming = false;
                FarmStat = "Запустить фарм";
                WebViewCore!.Navigate("https://kick.com");
            }
            
        }

        private async Task<bool> ChannelIsLive(string ChannelName,string? GameName)
        {
            WebViewCore!.Navigate($"https://kick.com/api/v2/channels/{ChannelName}");
            await WaitForNavigationAsync();

            string ChannelJson = await WebViewCore.ExecuteScriptAsync(
                                        "document.querySelector('pre')?.innerText"
                                    );

            ChannelJson = (ChannelJson[..^1] + "\"").Trim('"').Replace("\\\"", "\"");

            using JsonDocument doc = JsonDocument.Parse(ChannelJson);

            if (!doc.RootElement.TryGetProperty("livestream", out var livestreamData) || livestreamData.ValueKind == JsonValueKind.Null)
                return false;

            string? LiveGameName = null;

            if (livestreamData.TryGetProperty("categories", out var categories))
            {
                if (categories.ValueKind == JsonValueKind.Object &&
                    categories.TryGetProperty("name", out var nameObj) &&
                    nameObj.ValueKind == JsonValueKind.String)
                {
                    LiveGameName = nameObj.GetString();
                }
                else if (categories.ValueKind == JsonValueKind.Array &&
                         categories.GetArrayLength() > 0)
                {
                    var first = categories[0];

                    if (first.ValueKind == JsonValueKind.Object &&
                        first.TryGetProperty("name", out var nameArr) &&
                        nameArr.ValueKind == JsonValueKind.String)
                    {
                        LiveGameName = nameArr.GetString();
                    }
                }
            }

            return LiveGameName == GameName;
        }
        private async Task WatchOnChannel(Reward Reward, string channel, CancellationToken ct)
        {
            if (Reward.Rewards == null)
                return;

            WebViewCore!.Navigate($"https://kick.com/{channel}");
            await WaitForNavigationAsync();

            while (!ct.IsCancellationRequested &&
                   Reward.IsSelected &&
                   Reward.Rewards.Any(d => d.WatchedMinutes < d.RequiredMinutes))
            {
                DateTime startTime = DateTime.UtcNow;

                await Task.Delay(1000, ct);

                TimeSpan elapsed = DateTime.UtcNow - startTime;

                foreach (var drop in Reward.Rewards)
                {
                    drop.WatchedMinutes += elapsed.TotalMinutes;
                    drop.Proc = (drop.WatchedMinutes * 100.0 / drop.RequiredMinutes).ToString("F2") + "%";
                }

                Application.Current.Dispatcher.Invoke(() => { });

                bool isLive = await CheckCurrChannelLive();
                if (!isLive)
                    break;

                if (!Reward.Rewards.Any(d => d.WatchedMinutes < d.RequiredMinutes))
                {
                    bool DropsRec = await DropReceived(Reward);

                    if (DropsRec)
                    {
                        foreach (var drop in Reward.Rewards)
                        {
                            drop.WatchedMinutes = drop.RequiredMinutes;
                            drop.Proc = "100%";
                        }

                        Reward.IsSelected = false;
                        break;
                    }
                    else
                    {
                        WebViewCore!.Navigate($"https://kick.com/{channel}");
                        await WaitForNavigationAsync();
                        startTime = DateTime.UtcNow;
                    }
                }
            }
        }

        private async Task<bool> DropReceived(Reward Reward)
        {
            int RptCnt = 5;

            WebViewCore!.Navigate("https://kick.com/drops/inventory");

            await WaitForNavigationAsync();

            bool NotYet = false;

            foreach (Drop drop in Reward.Rewards!)
            {
                string DropName = drop.Title!;

                string jsCode = $@"(() => {{
                                const li = [...document.querySelectorAll('main li')]
                                    .find(el => el.querySelector('img')?.alt === '{DropName}');

                                if (!li) return null;

                                const divs = li.querySelectorAll('div');
                                const firstSpan = divs[0]?.querySelector('span');
                                const firstDiv = divs[0]?.querySelectorAll('div')[1]; 
                                let aria = firstDiv?.getAttribute('aria-valuenow');

                                const lastSpan = divs[divs.length - 1]?.querySelector('span');
                                let valText = lastSpan?.textContent?.trim() ?? '';
                                const button = li.querySelector('button[aria-label*=""Claim""]');

                                let value = 0;

                                if (button) {{
                                    value = 1;
                                    button.click();
                                }}
                                else if (valText.toLowerCase().includes('полученные')) {{
                                    value = 1;
                                }} 
                                else if (aria !== null && aria !== '') {{
                                    aria = aria.replace(',', '.'); 
                                    value = parseFloat(aria);
                                    if (isNaN(value)) value = 0;
                                }}
                                else {{
                                    valText = valText.replace(',', '.'); 
                                    value = parseFloat(valText) / 100;
                                    if (isNaN(value)) value = 0;
                                }}

                                return value;
                            }})();";

                string result = await WebViewCore.ExecuteScriptAsync(jsCode);

                int Rpt = 0;
                while ((string.IsNullOrWhiteSpace(result) || result == "null") && RptCnt > Rpt)
                {
                    await Task.Delay(500);
                    result = await WebViewCore.ExecuteScriptAsync(jsCode);
                    Rpt++;
                }

                if (!string.IsNullOrWhiteSpace(result) && result != "null")
                {
                    double value = JsonSerializer.Deserialize<double>(result)!;
                    if (value < 1)
                        NotYet = true;

                    drop.WatchedMinutes = value * drop.RequiredMinutes;
                }
                else
                    NotYet = true;
            }

            return !NotYet;
        }

        private Task WaitForNavigationAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            void handler(object? s, CoreWebView2NavigationCompletedEventArgs e)
            {
                WebViewCore.NavigationCompleted -= handler;
                tcs.TrySetResult(true);
            }

            WebViewCore!.NavigationCompleted += handler;
            return tcs.Task;
        }

        private async Task<bool> CheckCurrChannelLive()
        {
            string js = @"(() => {
                            const liveBadge = [...document.querySelectorAll('span')]
                                .find(el => el.textContent.trim().toUpperCase() === 'LIVE');

                            return liveBadge ? 'live' : 'offline';
                        })();
                        ";

            string result = await WebViewCore!.ExecuteScriptAsync(js);

            return result.Contains("live");
        }
        public class DropStatus
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("value")]
            public double Value { get; set; }
        }
    }
}
