using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RATools.ViewModels
{
    public class GameProgressionViewModel : DialogViewModelBase
    {
        public GameProgressionViewModel(List<AchievementInfo> progressionStats)
        { 
            Progress = new ProgressFieldViewModel { Label = String.Empty };
            DialogTitle = "Game Progression";
            CanClose = true;

            Achievements = progressionStats;

            ShowAchievementCommand = new DelegateCommand<AchievementInfo>(ShowAchievement);
        }

        public ProgressFieldViewModel Progress { get; private set; }

        [DebuggerDisplay("{Distance} {Title}")]
        public class AchievementInfo
        {
            public int Id { get; set; }
            public string Title { get; set; }

            public TimeSpan Distance { get; set; }

            public TimeSpan TotalDistance { get; set; }
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
