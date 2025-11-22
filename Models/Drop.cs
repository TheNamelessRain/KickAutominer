using KickAutominer.Commands;
using KickAutominer.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace KickAutominer.Models
{
    public class Reward : ViewModelBase
    {
        private string? _rewardgroupname;
        public string? RewarGroupdName
        {
            get => _rewardgroupname;
            set => Set(ref _rewardgroupname, value);
        }

        private List<Drop>? _rewards;
        public List<Drop>? Rewards
        {
            get => _rewards;
            set => Set(ref _rewards, value);
        }

        private string? _gamename;
        public string? GameName
        {
            get => _gamename;
            set => Set(ref _gamename, value);
        }

        public List<string> ChannelUsernames { get; set; } = [];

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }
    }

    public class Drop : ViewModelBase
    {
        private string? _title;
        public string? Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private int _requiredMinutes;
        public int RequiredMinutes
        {
            get => _requiredMinutes;
            set => Set(ref _requiredMinutes, value);
        }

        private double _watchedMinutes;
        public double WatchedMinutes
        {
            get => _watchedMinutes;
            set => Set(ref _watchedMinutes, value);
        }

        private BitmapImage? _image;
        public BitmapImage? Image
        {
            get => _image;
            set => Set(ref _image, value);
        }

        private string? _proc;
        public string? Proc
        {
            get => _proc;
            set => Set(ref _proc, value);
        }

        public string? ImageUrl { get; set; }
    }

}
