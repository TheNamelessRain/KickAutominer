using KickAutominer.Animations;
using KickAutominer.Commands;
using KickAutominer.Models;
using Microsoft.Web.WebView2.Core;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

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

        private string? _selectedtheme;
        public string? SelectedTheme
        {
            get => _selectedtheme;
            set
            {
                Set(ref _selectedtheme, value);
                ApplyTheme();
            }
        }

        private string _loadPicBtnText = "Загрузить картинки";
        public string LoadPicBtnText
        {
            get => _loadPicBtnText;
            set
            {
                _loadPicBtnText = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> Themes { get; private set; }
        public ObservableCollection<Reward> Rewards { get;  } = [];

        public RelayCommand LoadDropsCommand { get; }
        public RelayCommand StartFarmCommand { get; }
        public RelayCommand loadPicCommand { get; }

        private bool _farming;
        private bool _takeData;
        private CancellationTokenSource? _farmCTS;
        private bool _webViewHooksInitialized;
        private bool _loadingPics;

        public MainViewModel()
        {
            Themes = ["White", "Dark"];
            SelectedTheme = Properties.Settings.Default.SavedTheme;
            SelectedTheme = "White";
            LoadDropsCommand = new RelayCommand(async _ => await LoadDropsAsync(), CanLoadDrops);
            StartFarmCommand = new RelayCommand(async _ => await StartFarm(), CanStartFarm);
            loadPicCommand = new RelayCommand(async _ => await loadPic(), CanloadPic);
            //LoadRewards();
        }

        private async Task InitWebViewHooksAsync()
        {
            if (_webViewHooksInitialized)
                return;

            WebViewCore.AddWebResourceRequestedFilter(
                "*",
                CoreWebView2WebResourceContext.Image
            );

            WebViewCore.WebResourceResponseReceived += WebView_ImageLoaded;

            _webViewHooksInitialized = true;
        }

        private async void WebView_ImageLoaded(
    object? sender,
    CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            if (e.Request.Uri.Contains("reward-image"))
            {
                try
                {
                    using var stream = await e.Response.GetContentAsync();

                    if (stream == null)
                        return;

                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    ms.Position = 0;

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.DecodePixelWidth = 150;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    foreach (Reward rew in Rewards)
                    {
                        foreach (var r in rew.Rewards)
                        {
                            if (r.ImageUrl == e.Request.Uri)
                            {
                                r.Image = bitmap;
                                break;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private bool CanloadPic(object? parameter) => Rewards.Count > 0 && WebViewCore != null && !_farming;
        private async Task loadPic()
        {
            if (_loadingPics)
                return;

            _loadingPics = true;
            using DotAnimation LoadAnimation = new();
            LoadAnimation.Start(count =>
            {
                LoadPicBtnText = "Загрузка картинок" + new string('.', count);
            });

            await InitWebViewHooksAsync();

            foreach (Reward rew in Rewards)
            {
                foreach (var r in rew.Rewards)
                {
                    if (r.Image != null)
                        continue;

                    WebViewCore.Navigate(r.ImageUrl);
                    await WaitForNavigationAsync();
                }
            }
            WebViewCore.WebResourceResponseReceived -= WebView_ImageLoaded;
            _webViewHooksInitialized = false;

            WebViewCore!.Navigate("https://kick.com");

            LoadAnimation.Stop();
            LoadPicBtnText = "Готово";
            await Task.Delay(1500);
            LoadPicBtnText = "Загрузить картинки";
            _loadingPics = false;
        }

        public void ApplyTheme()
        {
            if (string.IsNullOrEmpty(SelectedTheme)) 
                return;

            Properties.Settings.Default.SavedTheme = SelectedTheme;
            Properties.Settings.Default.Save();

            var Resources = Application.Current.Resources;

            Resources.MergedDictionaries.Clear();

            var themeDict = new ResourceDictionary
            {
                Source = new Uri($"/KickAutominer;component/Themes/{SelectedTheme}.xaml", UriKind.Relative)
            };
            Resources.MergedDictionaries.Add(themeDict);
        }

        private void LoadRewards()
        {
            var reward1 = new Reward
            {
                RewarGroupdName = "Group 1",
                GameName = "Game A",
                IsSelected = true,
                Rewards =
            [
                new Drop { Title = "Drop 1 bla bla lba lba lba lba lba lba lba", RequiredMinutes = 10, WatchedMinutes = 5 },
                new Drop { Title = "Drop 2", RequiredMinutes = 15, WatchedMinutes = 5 },
                new Drop { Title = "Drop 3", RequiredMinutes = 20, WatchedMinutes = 5,Proc="15" }
            ]
            };

            // Второй Reward с 2 дропами
            var reward2 = new Reward
            {
                RewarGroupdName = "Group 2",
                GameName = "Game B",
                IsSelected = false,
                Rewards =
            [
                new Drop { Title = "Drop A", RequiredMinutes = 12, WatchedMinutes = 3 },
                new Drop { Title = "Drop B", RequiredMinutes = 8,  WatchedMinutes = 7 }
            ]
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

        private async Task ParseDrops(string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("data", out var data))
                return;

            foreach (var campaign in data.EnumerateArray())
            {
                List<string> AllChannels = [];

                DateTime endDate = campaign.GetProperty("ends_at").GetDateTime();
                if (endDate <= DateTime.UtcNow)
                    continue;

                DateTime startdate = campaign.GetProperty("starts_at").GetDateTime();
                if (startdate.AddHours(3) >= DateTime.Now)
                    continue;

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

                if (AllChannels.Count == 0)
                {
                    WebViewCore!.Navigate($"https://kick.com/category/{Reward.GameName?.ToLower()}/drops");

                    await WaitForNavigationAsync();

                    string jsCode = @"(function() {
                                                const results = [];
                                                const divsWithRust = document.querySelectorAll('div:has(a > span)');
                                                divsWithRust.forEach(div => {
                                                    const rustSpan = div.querySelector('a > span');
                                                                if (!rustSpan) return;
                                                                const text = rustSpan.textContent.trim();
                                                                if (!text.toLowerCase().includes('" + Reward.GameName?.ToLower() + @"')) return;
                                                                const links = div.querySelectorAll('a');
                                                                if (links.length < 2)
                                                                    return;
                                                                const secondLink = links[1];
                                                                const href = secondLink.href;
                                                                if (!href)
                                                                    return;
                                                                if (href.includes('category'))
                                                                    return;

                                                                const trimmedHref = href.replace('https://kick.com/', '');
                                                                results.push(trimmedHref);
                                                            });
                                                            return results;
                                                        })();";

                    string ChannelJson = await WebViewCore.ExecuteScriptAsync(jsCode);

                    string[] hrefs = JsonSerializer.Deserialize<string[]>(ChannelJson);

                    if (hrefs != null)
                        AllChannels.AddRange(hrefs);

                    if (Reward.ChannelUsernames.Count == 0)
                        Reward.ChannelUsernames = AllChannels;
                }

                Reward.RewarGroupdName = campaign.TryGetProperty("name", out var rewgrpname) &&
                                    rewgrpname.ValueKind == JsonValueKind.String ? rewgrpname.GetString() : null;

                List<Drop> SortedDrops = [.. Reward.Rewards.OrderBy(t => t.RequiredMinutes)];

                Reward.Rewards = SortedDrops;

                Rewards.Add(Reward);
            }
        }

        private bool CanLoadDrops(object? parameter) => WebViewCore != null;

        public async Task LoadDropsAsync()
        {
            using DotAnimation LoadDropsAnimation = new();
            LoadDropsAnimation.Start(count =>
            {
                LoadDropsButton = "Обновление" + new string('.', count);
            });

            _takeData = true;
            Rewards.Clear();

            WebViewCore!.Navigate("https://web.kick.com/api/v1/drops/campaigns");

            await WaitForNavigationAsync();

            string DropsJson = await WebViewCore.ExecuteScriptAsync(
                                        "document.querySelector('pre')?.innerText"
                                    );

            DropsJson = (DropsJson[..^3] + "\"").Trim('"').Replace("\\\"", "\"");

            await ParseDrops(DropsJson);

            if (Rewards?.Count == 0)
            {
                WebViewCore!.Navigate("https://kick.com");
                MessageBox.Show("Нет активных кампаний");
                _takeData = false;
                LoadDropsAnimation.Stop();
                LoadDropsButton = "Загрузить список дропов";
                return;
            }

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

            LoadDropsAnimation.Stop();
            LoadDropsButton = "Готово";
            await Task.Delay(1500);

            if (Rewards.Count > 0)
                LoadDropsButton = "Обновить список дропов";
            else
                LoadDropsButton = "Загрузить список дропов";

            WebViewCore!.Navigate("https://kick.com");

            CommandManager.InvalidateRequerySuggested();

            _takeData = false;
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

        private bool CanStartFarm(object? parameter) => Rewards.Any(d => d.IsSelected) && !_takeData;
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
            using DotAnimation FarmAnimation = new();
            FarmAnimation.Start(count =>
            {
                FarmStat = "Фарм идёт" + new string('.', count);
            });
            
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
                FarmAnimation.Stop();
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
                    if (drop.WatchedMinutes + elapsed.TotalMinutes >= drop.RequiredMinutes)
                    {
                        drop.WatchedMinutes = drop.RequiredMinutes;
                        drop.Proc = "100%";
                    }
                    else
                    {
                        drop.WatchedMinutes += elapsed.TotalMinutes;
                        drop.Proc = (drop.WatchedMinutes * 100.0 / drop.RequiredMinutes).ToString("F2") + "%";
                    }
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
