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
using System.Text;

namespace RATools.ViewModels
{
    public class GameProgressionViewModel : DialogViewModelBase
    {
        public GameProgressionViewModel()
            : this(ServiceRepository.Instance.FindService<IBackgroundWorkerService>())
        {

        }

        public GameProgressionViewModel(IBackgroundWorkerService backgroundWorkerService)
        { 
            _backgroundWorkerService = backgroundWorkerService;

            Progress = new ProgressFieldViewModel { Label = String.Empty };
            DialogTitle = "Game Progression";
            CanClose = true;

            SearchCommand = new DelegateCommand(Search);

            ShowAchievementCommand = new DelegateCommand<AchievementInfo>(ShowAchievement);
        }

        private readonly IBackgroundWorkerService _backgroundWorkerService;

        public static readonly ModelProperty GameIdProperty = ModelProperty.Register(typeof(GameStatsViewModel), "GameId", typeof(int), 0);

        public int GameId
        {
            get { return (int)GetValue(GameIdProperty); }
            set { SetValue(GameIdProperty, value); }
        }

        public CommandBase SearchCommand { get; private set; }
        private void Search()
        {
            Progress.Label = "Fetching progression data";
            Progress.Reset(1);
            Progress.IsEnabled = true;

            _backgroundWorkerService.RunAsync(() =>
            {
                var progressionJson = RAWebCache.Instance.GetGameProgressionJson(this.GameId);
                Progress.Current++;

                DialogTitle = "Game Progression - " + progressionJson.GetField("Title").StringValue;

                var achievements = new List<AchievementInfo>();
                foreach (var achievement in progressionJson.GetField("Achievements").ObjectArrayValue)
                {
                    achievements.Add(new AchievementInfo
                    {
                        Id = achievement.GetField("ID").IntegerValue.GetValueOrDefault(),
                        Title = achievement.GetField("Title").StringValue,
                        Distance = TimeSpan.FromSeconds(achievement.GetField("MedianTimeToUnlockHardcore").IntegerValue.GetValueOrDefault()),
                        TotalDistanceCount = achievement.GetField("TimesUsedInHardcoreUnlockMedian").IntegerValue.GetValueOrDefault(),
                    });
                }

                achievements.Sort((l, r) =>
                {
                    // can't just return the difference between two distances, as it's entirely
                    // possible that a value could be a few milliseconds or several years, so
                    // there's no easy way to convert the different into a 32-bit integer.
                    if (l.Distance == r.Distance)
                        return 0;
                    return (l.Distance > r.Distance) ? 1 : -1;
                });

                Achievements = achievements;
                OnPropertyChanged(() => Achievements);

                Progress.Label = String.Empty;
            });
        }

        public ProgressFieldViewModel Progress { get; private set; }

        [DebuggerDisplay("{Distance} {Title}")]
        public class AchievementInfo
        {
            public int Id { get; set; }
            public string Title { get; set; }

            public TimeSpan Distance { get; set; }

            public int TotalDistanceCount { get; set; }

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

        public List<AchievementInfo> Achievements { get; private set; }

        public DelegateCommand<AchievementInfo> ShowAchievementCommand { get; private set; }

        private static void ShowAchievement(AchievementInfo info)
        {
            var url = "https://retroachievements.org/achievement/" + info.Id;
            ServiceRepository.Instance.FindService<IBrowserService>().OpenUrl(url);
        }
    }
}
