using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.ViewModels
{
    public class UnlockDistanceViewModel : DialogViewModelBase
    {
        public UnlockDistanceViewModel()
            : this(ServiceRepository.Instance.FindService<IBackgroundWorkerService>(),
                   ServiceRepository.Instance.FindService<IFileSystemService>(),
                   ServiceRepository.Instance.FindService<ISettings>())
        {
        }

        public UnlockDistanceViewModel(IBackgroundWorkerService backgroundWorkerService, IFileSystemService fileSystem, ISettings settings)
        {
            _backgroundWorkerService = backgroundWorkerService;
            _fileSystem = fileSystem;
            _settings = settings;

            Progress = new ProgressFieldViewModel { Label = String.Empty };
            DialogTitle = "Unlock Distance";
            CanClose = true;

            SearchCommand = new DelegateCommand(Search);
            ShowUserUnlocksCommand = new DelegateCommand<UnlockInfo>(ShowUserUnlocks);
        }

        private readonly IBackgroundWorkerService _backgroundWorkerService;
        private readonly IFileSystemService _fileSystem;
        private readonly ISettings _settings;
        private int _gameId;

        public ProgressFieldViewModel Progress { get; private set; }

        public static readonly ModelProperty FirstAchievementIdProperty = ModelProperty.Register(typeof(UnlockDistanceViewModel), "FirstAchievementId", typeof(int), 0);

        public int FirstAchievementId
        {
            get { return (int)GetValue(FirstAchievementIdProperty); }
            set { SetValue(FirstAchievementIdProperty, value); }
        }


        public static readonly ModelProperty SecondAchievementIdProperty = ModelProperty.Register(typeof(UnlockDistanceViewModel), "SecondAchievementId", typeof(int), 0);

        public int SecondAchievementId
        {
            get { return (int)GetValue(SecondAchievementIdProperty); }
            set { SetValue(SecondAchievementIdProperty, value); }
        }

        public CommandBase SearchCommand { get; private set; }
        private void Search()
        {
            if (FirstAchievementId == 0)
            {
                MessageBoxViewModel.ShowMessage("Invalid first achievement ID");
                return;
            }

            if (SecondAchievementId == 0)
            {
                MessageBoxViewModel.ShowMessage("Invalid second achievement ID");
                return;
            }

            Progress.Reset(2);
            Progress.Label = "Fetching Achievement " + FirstAchievementId;
            Progress.IsEnabled = true;
            _backgroundWorkerService.RunAsync(DoComparison);
        }

        [DebuggerDisplay("{User} ({FirstUnlock} => {SecondUnlock})")]
        public class UnlockInfo
        {
            public string User { get; set; }
            public DateTime FirstUnlock { get; set; }
            public DateTime SecondUnlock { get; set; }
            public TimeSpan Distance { get; set; }

            public string FormattedDistance
            {
                get
                {
                    var builder = new StringBuilder();

                    int totalMinutes = (int)Distance.TotalMinutes;
                    if (totalMinutes < 0)
                    {
                        builder.Append('-');
                        totalMinutes = -totalMinutes;
                    }

                    builder.AppendFormat("{0}h{1:D2}m", totalMinutes / 60, totalMinutes % 60);

                    return builder.ToString();
                }
            }
        }


        public static readonly ModelProperty UnlocksProperty = ModelProperty.Register(typeof(UnlockDistanceViewModel), "Unlocks", typeof(IEnumerable<UnlockInfo>), new UnlockInfo[0]);

        public IEnumerable<UnlockInfo> Unlocks
        {
            get { return (IEnumerable<UnlockInfo>)GetValue(UnlocksProperty); }
            private set { SetValue(UnlocksProperty, value); }
        }

        public static readonly ModelProperty UnlockCountProperty = ModelProperty.Register(typeof(UnlockDistanceViewModel), "UnlockCount", typeof(int), 0);

        public int UnlockCount
        {
            get { return (int)GetValue(UnlockCountProperty); }
            private set { SetValue(UnlockCountProperty, value); }
        }

        public static readonly ModelProperty MedianUnlockDistanceProperty = ModelProperty.Register(typeof(UnlockDistanceViewModel), "MedianUnlockDistance", typeof(string), "n/a");

        public string MedianUnlockDistance
        {
            get { return (string)GetValue(MedianUnlockDistanceProperty); }
            private set { SetValue(MedianUnlockDistanceProperty, value); }
        }

        private void DoComparison()
        {
            var firstUnlocks = new List<UnlockInfo>();
            var unlocks = new List<UnlockInfo>();

            var unlocksJson = RAWebCache.Instance.GetAchievementUnlocksJson(FirstAchievementId, 0);
            if (unlocksJson != null)
            {
                _gameId = unlocksJson.GetField("Game").ObjectValue.GetField("ID").IntegerValue.GetValueOrDefault();
                foreach (var unlock in unlocksJson.GetField("Unlocks").ObjectArrayValue)
                {
                    if (unlock.GetField("HardcoreMode").IntegerValue != 1)
                        continue;

                    string user = unlock.GetField("User").StringValue;
                    var unlockInfo = new UnlockInfo
                    {
                        User = user,
                        FirstUnlock = unlock.GetField("DateAwarded").DateTimeValue.GetValueOrDefault()
                    };
                    firstUnlocks.Add(unlockInfo);
                }
            }

            Progress.Label = "Fetching Achievement " + FirstAchievementId;
            Progress.Current++;

            unlocksJson = RAWebCache.Instance.GetAchievementUnlocksJson(SecondAchievementId, 0);
            if (unlocksJson != null)
            {
                var gameId2 = unlocksJson.GetField("Game").ObjectValue.GetField("ID").IntegerValue.GetValueOrDefault();
                if (gameId2 != _gameId)
                    _gameId = -1;

                foreach (var unlock in unlocksJson.GetField("Unlocks").ObjectArrayValue)
                {
                    if (unlock.GetField("HardcoreMode").IntegerValue != 1)
                        continue;

                    string user = unlock.GetField("User").StringValue;
                    var unlockInfo = firstUnlocks.FirstOrDefault(u => u.User == user);
                    if (unlockInfo == null)
                        continue;

                    unlockInfo.SecondUnlock = unlock.GetField("DateAwarded").DateTimeValue.GetValueOrDefault();
                    unlockInfo.Distance = unlockInfo.SecondUnlock - unlockInfo.FirstUnlock;
                    unlocks.Add(unlockInfo);
                }
            }

            Progress.Current++;

            unlocks.Sort((l, r) => ((int)l.Distance.TotalSeconds - (int)r.Distance.TotalSeconds));
            Unlocks = unlocks;
            UnlockCount = unlocks.Count;

            if (unlocks.Count > 0)
                MedianUnlockDistance = unlocks[unlocks.Count / 2].FormattedDistance;
            else
                MedianUnlockDistance = "n/a";

            Progress.Label = String.Empty;
        }

        public DelegateCommand<UnlockInfo> ShowUserUnlocksCommand { get; private set; }
        void ShowUserUnlocks(UnlockInfo info)
        {
            if (_gameId <= 0)
            {
                MessageBoxViewModel.ShowMessage("Achievements are from different games.");
                return;
            }

            var url = "https://retroachievements.org/gamecompare.php?ID=" + _gameId + "&f=" + info.User;
            ServiceRepository.Instance.FindService<IBrowserService>().OpenUrl(url);
        }
    }
}
