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
            : this(ServiceRepository.Instance.FindService<IBackgroundWorkerService>())
        {
        }

        public UnlockDistanceViewModel(IBackgroundWorkerService backgroundWorkerService)
        {
            _backgroundWorkerService = backgroundWorkerService;

            Progress = new ProgressFieldViewModel { Label = String.Empty };
            DialogTitle = "Unlock Distance";
            CanClose = true;

            var comparisonLookup = new[]
            {
                new LookupItem((int)Comparison.Distance, "Distance"),
                new LookupItem((int)Comparison.FirstWithoutSecond, "First without Second"),
                new LookupItem((int)Comparison.SecondWithoutFirst, "Second without First"),
            };

            FirstAchievementId = new IntegerFieldViewModel("First Achievement ID", 0, 9999999);
            SecondAchievementId = new IntegerFieldViewModel("Second Achievement ID", 0, 9999999);
            ComparisonType = new LookupFieldViewModel("Comparison Type", comparisonLookup);
            ComparisonType.SelectedId = (int)Comparison.Distance;

            SearchCommand = new DelegateCommand(Search);
            ShowUserUnlocksCommand = new DelegateCommand<UnlockInfo>(ShowUserUnlocks);
        }

        private readonly IBackgroundWorkerService _backgroundWorkerService;
        private int _gameId;

        public ProgressFieldViewModel Progress { get; private set; }

        public IntegerFieldViewModel FirstAchievementId { get; private set; }
        public IntegerFieldViewModel SecondAchievementId { get; private set; }
        public LookupFieldViewModel ComparisonType { get; private set; }

        public enum Comparison
        {
            None = 0,
            Distance,
            FirstWithoutSecond,
            SecondWithoutFirst,
        }

        public static readonly ModelProperty FirstAchievementNameProperty = ModelProperty.Register(typeof(UnlockDistanceViewModel), "FirstAchievementName", typeof(string), "Unlock 1");
        public string FirstAchievementName
        {
            get { return (string)GetValue(FirstAchievementNameProperty); }
            private set { SetValue(FirstAchievementNameProperty, value); }
        }

        public static readonly ModelProperty SecondAchievementNameProperty = ModelProperty.Register(typeof(UnlockDistanceViewModel), "SecondAchievementName", typeof(string), "Unlock 2");
        public string SecondAchievementName
        {
            get { return (string)GetValue(SecondAchievementNameProperty); }
            private set { SetValue(SecondAchievementNameProperty, value); }
        }

        public static readonly ModelProperty SummaryTextProperty = ModelProperty.Register(typeof(UnlockDistanceViewModel), "SummaryText", typeof(string), String.Empty);
        public string SummaryText
        {
            get { return (string)GetValue(SummaryTextProperty); }
            private set { SetValue(SummaryTextProperty, value); }
        }

        public CommandBase SearchCommand { get; private set; }
        private void Search()
        {
            if (FirstAchievementId.Value == 0)
            {
                MessageBoxViewModel.ShowMessage("Invalid first achievement ID");
                return;
            }

            if (SecondAchievementId.Value == 0)
            {
                MessageBoxViewModel.ShowMessage("Invalid second achievement ID");
                return;
            }

            Progress.Reset(2);
            Progress.Label = "Fetching Achievement " + FirstAchievementId.Value.GetValueOrDefault();
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
                    if (FirstUnlock.Year < 2000 || SecondUnlock.Year < 2000)
                        return "n/a";

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

            public string FormattedFirstUnlock
            {
                get
                {
                    if (FirstUnlock.Year < 2000)
                        return "n/a";

                    return FirstUnlock.ToString();
                }
            }

            public string FormattedSecondUnlock
            {
                get
                {
                    if (SecondUnlock.Year < 2000)
                        return "n/a";

                    return SecondUnlock.ToString();
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

            var page = 0;
            var count = 0;
            do
            {
                var unlocksJson = RAWebCache.Instance.GetAchievementUnlocksJson(FirstAchievementId.Value.GetValueOrDefault(), page++);
                if (unlocksJson == null)
                    break;

                _gameId = unlocksJson.GetField("Game").ObjectValue.GetField("ID").IntegerValue.GetValueOrDefault();
                FirstAchievementName = unlocksJson.GetField("Achievement").ObjectValue.GetField("Title").StringValue;

                count = 0;
                foreach (var unlock in unlocksJson.GetField("Unlocks").ObjectArrayValue)
                {
                    count++;
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
            } while (count == 500);

            Progress.Label = "Fetching Achievement " + FirstAchievementId.Value.GetValueOrDefault();
            Progress.Current++;

            var compareType = (Comparison)ComparisonType.SelectedId;
            var unlocks = (compareType == Comparison.FirstWithoutSecond) ? firstUnlocks : new List<UnlockInfo>();

            page = 0;
            do
            {
                var unlocksJson = RAWebCache.Instance.GetAchievementUnlocksJson(SecondAchievementId.Value.GetValueOrDefault(), page++);
                if (unlocksJson == null)
                    break;

                var gameId2 = unlocksJson.GetField("Game").ObjectValue.GetField("ID").IntegerValue.GetValueOrDefault();
                if (gameId2 != _gameId)
                    _gameId = -1;

                SecondAchievementName = unlocksJson.GetField("Achievement").ObjectValue.GetField("Title").StringValue;

                count = 0;
                UnlockInfo unlockInfo;
                foreach (var unlock in unlocksJson.GetField("Unlocks").ObjectArrayValue)
                {
                    count++;
                    if (unlock.GetField("HardcoreMode").IntegerValue != 1)
                        continue;

                    string user = unlock.GetField("User").StringValue;

                    switch (compareType)
                    {
                        case Comparison.Distance:
                            unlockInfo = firstUnlocks.FirstOrDefault(u => u.User == user);
                            if (unlockInfo == null)
                                continue;

                            unlockInfo.SecondUnlock = unlock.GetField("DateAwarded").DateTimeValue.GetValueOrDefault();
                            unlockInfo.Distance = unlockInfo.SecondUnlock - unlockInfo.FirstUnlock;
                            unlocks.Add(unlockInfo);
                            break;

                        case Comparison.FirstWithoutSecond:
                            firstUnlocks.RemoveAll(u => u.User == user);
                            break;

                        case Comparison.SecondWithoutFirst:
                            unlockInfo = firstUnlocks.FirstOrDefault(u => u.User == user);
                            if (unlockInfo != null)
                                continue;

                            unlockInfo = new UnlockInfo { User = user };
                            unlockInfo.SecondUnlock = unlock.GetField("DateAwarded").DateTimeValue.GetValueOrDefault();
                            unlockInfo.Distance = unlockInfo.SecondUnlock - unlockInfo.FirstUnlock;
                            unlocks.Add(unlockInfo);
                            break;
                    }
                }
            } while (count == 500);

            Progress.Current++;

            unlocks.Sort((l, r) => ((int)l.Distance.TotalSeconds - (int)r.Distance.TotalSeconds));
            Unlocks = unlocks;
            UnlockCount = unlocks.Count;

            if (unlocks.Count > 0)
                MedianUnlockDistance = unlocks[unlocks.Count / 2].FormattedDistance;
            else
                MedianUnlockDistance = "n/a";

            switch (compareType)
            {
                case Comparison.Distance:
                    SummaryText = String.Format("{0} users unlocked both achievements. Median unlock distance: {1}", UnlockCount, MedianUnlockDistance);
                    break;

                case Comparison.FirstWithoutSecond:
                    SummaryText = String.Format("{0} users unlocked {1}, but not {2}", UnlockCount, FirstAchievementName, SecondAchievementName);
                    break;

                case Comparison.SecondWithoutFirst:
                    SummaryText = String.Format("{0} users unlocked {1}, but not {2}", UnlockCount, SecondAchievementName, FirstAchievementName);
                    break;
            }

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
