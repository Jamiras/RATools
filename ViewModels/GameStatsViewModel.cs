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
    public class GameStatsViewModel : DialogViewModelBase
    {
        public GameStatsViewModel()
            : this(ServiceRepository.Instance.FindService<IBackgroundWorkerService>())
        {
        }

        public GameStatsViewModel(IBackgroundWorkerService backgroundWorkerService)
        {
            _backgroundWorkerService = backgroundWorkerService;

            Progress = new ProgressFieldViewModel { Label = String.Empty };
            DialogTitle = "Game Stats";
            CanClose = true;

            SearchCommand = new DelegateCommand(Search);
        }

        private readonly IBackgroundWorkerService _backgroundWorkerService;

        public ProgressFieldViewModel Progress { get; private set; }

        public static readonly ModelProperty GameIdProperty = ModelProperty.Register(typeof(GameStatsViewModel), "GameId", typeof(int), 0);

        public int GameId
        {
            get { return (int)GetValue(GameIdProperty); }
            set { SetValue(GameIdProperty, value); }
        }

        public CommandBase SearchCommand { get; private set; }
        private void Search()
        {
            Progress.Label = "Fetching Game " + GameId;
            Progress.IsEnabled = true;
            _backgroundWorkerService.RunAsync(LoadGame);
        }

        private static void OnGameIdChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            var vm = (GameStatsViewModel)sender;
            vm.Search();
        }

        [DebuggerDisplay("{Title} ({Id})")]
        public class AchievementStats
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public int Points { get; set; }
            public int EarnedBy { get; set; }
            public int EarnedHardcoreBy { get; set; }
        }

        [DebuggerDisplay("{User} ({PointsEarned} points)")]
        public class UserStats : IComparer<UserStats>
        {
            public UserStats()
            {
                Achievements = new TinyDictionary<int, DateTime>();
            }

            public string User { get; set; }
            public int PointsEarned { get; set; }
            public TimeSpan RealTime { get; set; }
            public TimeSpan GameTime { get; set; }
            public TinyDictionary<int, DateTime> Achievements { get; private set; }

            public string Summary
            {
                get
                {
                    var builder = new StringBuilder();
                    builder.AppendFormat("{0}h{1:D2}m", (int)GameTime.TotalHours, GameTime.Minutes);
                    if (RealTime.TotalDays > 1.0)
                        builder.AppendFormat(" over {0} days", (int)Math.Ceiling(RealTime.TotalDays));

                    return builder.ToString();
                }
            }

            int IComparer<UserStats>.Compare(UserStats x, UserStats y)
            {
                return String.Compare(x.User, y.User);
            }
        }

        public static readonly ModelProperty AchievementsProperty = ModelProperty.Register(typeof(GameStatsViewModel), "Achievements", typeof(IEnumerable<AchievementStats>), new AchievementStats[0]);

        public IEnumerable<AchievementStats> Achievements
        {
            get { return (IEnumerable<AchievementStats>)GetValue(AchievementsProperty); }
            private set { SetValue(AchievementsProperty, value); }
        }

        public static readonly ModelProperty HardcoreUserCountProperty = ModelProperty.Register(typeof(GameStatsViewModel), "HardcoreUserCount", typeof(int), 0);

        public int HardcoreUserCount
        {
            get { return (int)GetValue(HardcoreUserCountProperty); }
            private set { SetValue(HardcoreUserCountProperty, value); }
        }

        public static readonly ModelProperty NonHardcoreUserCountProperty = ModelProperty.Register(typeof(GameStatsViewModel), "NonHardcoreUserCount", typeof(int), 0);

        public int NonHardcoreUserCount
        {
            get { return (int)GetValue(NonHardcoreUserCountProperty); }
            private set { SetValue(NonHardcoreUserCountProperty, value); }
        }

        public static readonly ModelProperty MedianHardcoreUserScoreProperty = ModelProperty.Register(typeof(GameStatsViewModel), "MedianHardcoreUserScore", typeof(int), 0);

        public int MedianHardcoreUserScore
        {
            get { return (int)GetValue(MedianHardcoreUserScoreProperty); }
            private set { SetValue(MedianHardcoreUserScoreProperty, value); }
        }

        public static readonly ModelProperty HardcoreMasteredUserCountProperty = ModelProperty.Register(typeof(GameStatsViewModel), "HardcoreMasteredUserCount", typeof(int), 0);

        public int HardcoreMasteredUserCount
        {
            get { return (int)GetValue(HardcoreMasteredUserCountProperty); }
            private set { SetValue(HardcoreMasteredUserCountProperty, value); }
        }

        public static readonly ModelProperty HardcoreMasteredUserCountEstimatedProperty = ModelProperty.Register(typeof(GameStatsViewModel), "HardcoreMasteredUserCountEstimated", typeof(bool), false);

        public bool HardcoreMasteredUserCountEstimated
        {
            get { return (bool)GetValue(HardcoreMasteredUserCountEstimatedProperty); }
            private set { SetValue(HardcoreMasteredUserCountEstimatedProperty, value); }
        }

        public static readonly ModelProperty MedianTimeToMasterProperty = ModelProperty.Register(typeof(GameStatsViewModel), "MedianTimeToMaster", typeof(string), "n/a");

        public string MedianTimeToMaster
        {
            get { return (string)GetValue(MedianTimeToMasterProperty); }
            private set { SetValue(MedianTimeToMasterProperty, value); }
        }

        public static readonly ModelProperty TopUsersProperty = ModelProperty.Register(typeof(GameStatsViewModel), "TopUsers", typeof(IEnumerable<UserStats>), new UserStats[0]);

        public IEnumerable<UserStats> TopUsers
        {
            get { return (IEnumerable<UserStats>)GetValue(TopUsersProperty); }
            private set { SetValue(TopUsersProperty, value); }
        }

        private void LoadGame()
        {
            var gamePage = RAWebCache.Instance.GetGamePage(GameId);
            if (gamePage == null)
                return;

            var tokenizer = Tokenizer.CreateTokenizer(gamePage);
            tokenizer.ReadTo("<title>");
            if (tokenizer.Match("<title>"))
            {
                var title = tokenizer.ReadTo("</title>");
                title = title.SubToken(24);
                DialogTitle = "Game Stats - " +title.ToString();
            }

            AchievementStats mostWon = null;
            AchievementStats leastWon = null;

            var allStats = new List<AchievementStats>();
            do
            {
                tokenizer.ReadTo("<div class='achievemententry'>");
                if (tokenizer.NextChar == '\0')
                    break;

                AchievementStats stats = new AchievementStats();

                tokenizer.ReadTo("won by ");
                tokenizer.Advance(7);
                var winners = tokenizer.ReadNumber();
                stats.EarnedBy = Int32.Parse(winners.ToString());

                tokenizer.ReadTo("(");
                tokenizer.Advance();
                var hardcoreWinners = tokenizer.ReadNumber();
                stats.EarnedHardcoreBy = Int32.Parse(hardcoreWinners.ToString());

                tokenizer.ReadTo("<a href='/Achievement/");
                if (tokenizer.Match("<a href='/Achievement/"))
                {
                    var achievementId = tokenizer.ReadTo("'>");
                    stats.Id = Int32.Parse(achievementId.ToString());
                    tokenizer.Advance(2);

                    var achievementTitle = tokenizer.ReadTo("</a>");
                    Token achievementPoints = Token.Empty;
                    for (int i = achievementTitle.Length - 1; i >= 0; i--)
                    {
                        if (achievementTitle[i] == '(')
                        {
                            achievementPoints = achievementTitle.SubToken(i + 1, achievementTitle.Length - i - 2);
                            achievementTitle = achievementTitle.SubToken(0, i);
                            break;
                        }
                    }

                    stats.Title = achievementTitle.ToString();
                    stats.Points = Int32.Parse(achievementPoints.ToString());
                }

                allStats.Add(stats);

                if (mostWon == null)
                {
                    mostWon = leastWon = stats;
                }
                else
                {
                    if (stats.EarnedHardcoreBy > mostWon.EarnedHardcoreBy)
                        mostWon = stats;
                    else if (stats.EarnedHardcoreBy == mostWon.EarnedHardcoreBy && stats.EarnedBy > mostWon.EarnedBy)
                        mostWon = stats;

                    if (stats.EarnedHardcoreBy < leastWon.EarnedHardcoreBy)
                        leastWon = stats;
                    else if (stats.EarnedHardcoreBy == leastWon.EarnedHardcoreBy && stats.EarnedBy < leastWon.EarnedBy)
                        leastWon = stats;
                }
            } while (true);

            var masters = new List<string>();
            tokenizer = Tokenizer.CreateTokenizer(gamePage);
            tokenizer.ReadTo("<h3>High Scores</h3>");
            do
            {
                tokenizer.ReadTo("<td class='user'>");
                if (tokenizer.NextChar == '\0')
                    break;

                tokenizer.ReadTo("<a href='");
                tokenizer.ReadTo('>');
                tokenizer.Advance();

                var userName = tokenizer.ReadTo('<');

                tokenizer.ReadTo("<span");
                tokenizer.ReadTo('>');
                tokenizer.Advance();

                var points = tokenizer.ReadTo('<');
                if (points != "800")
                    break;

                masters.Add(userName.ToString());
            } while (true);

            Progress.Label = "Fetching user stats";
            Progress.Reset(allStats.Count);
            
            allStats.Sort((l, r) =>
            {
                var diff = r.EarnedHardcoreBy - l.EarnedHardcoreBy;
                if (diff == 0)
                    diff = String.Compare(l.Title, r.Title, StringComparison.OrdinalIgnoreCase);

                return diff;
            });

            Achievements = allStats;

            var nonHardcoreUsers = new List<string>();
            var userStats = new List<UserStats>();
            foreach (var achievement in allStats)
            {
                var achievementPage = RAWebCache.Instance.GetAchievementPage(achievement.Id);
                if (achievementPage != null)
                {
                    tokenizer = Tokenizer.CreateTokenizer(achievementPage);
                    tokenizer.ReadTo("<h3>Winners</h3>");

                    // NOTE: this only lists the ~50 most recent unlocks! For games with more than 50 users who have mastered it, the oldest may be missed!
                    do
                    {
                        tokenizer.ReadTo("<a href='/User/");
                        if (tokenizer.NextChar == '\0')
                            break;

                        tokenizer.ReadTo("'>");
                        tokenizer.Advance(2);
                        var user = tokenizer.ReadTo("</a>");
                        if (user.StartsWith("<img"))
                            continue;

                        var mid = tokenizer.ReadTo("<small>");
                        if (mid.Contains("Hardcore!"))
                        {
                            tokenizer.Advance(7);
                            var when = tokenizer.ReadTo("</small>");
                            var date = DateTime.Parse(when.ToString());
                            date = DateTime.SpecifyKind(date, DateTimeKind.Utc);

                            var stats = new UserStats { User = user.ToString() };
                            var index = userStats.BinarySearch(stats, stats);
                            if (index < 0)
                                userStats.Insert(~index, stats);
                            else
                                stats = userStats[index];

                            stats.Achievements[achievement.Id] = date;
                            stats.PointsEarned += achievement.Points;

                            if (ReferenceEquals(achievement, leastWon))
                            {
                                if (!masters.Contains(stats.User))
                                    masters.Add(stats.User);
                            }
                        }
                        else
                        {
                            if (!nonHardcoreUsers.Contains(user.ToString()))
                                nonHardcoreUsers.Add(user.ToString());
                        }

                    } while (true);
                }

                Progress.Current++;
            }

            // if more than 50 people have earned achievements, people who mastered the game early may no longer display 
            // in the individual pages. fetch mastery data by user
            if (mostWon.EarnedBy > 50)
            {
                HardcoreMasteredUserCountEstimated = (leastWon.EarnedBy > 50);

                Progress.Reset(masters.Count);
                foreach (var user in masters)
                {
                    Progress.Current++;

                    var masteryJson = RAWebCache.Instance.GetUserGameMasteryJson(user, GameId);
                    if (masteryJson == null) // not able to get - probably not logged in. don't try other users
                        break;

                    var stats = new UserStats { User = user };
                    var index = userStats.BinarySearch(stats, stats);
                    if (index < 0)
                        userStats.Insert(~index, stats);
                    else
                        stats = userStats[index];

                    if (stats.PointsEarned == 400)
                        continue;

                    var achievements = masteryJson.GetField("achievements").ObjectValue;
                    foreach (var achievement in achievements)
                    {
                        var id = Int32.Parse(achievement.FieldName);
                        if (!stats.Achievements.ContainsKey(id))
                        {
                            var dateField = achievement.ObjectValue.GetField("DateEarnedHardcore");
                            if (dateField.Type != Jamiras.IO.Serialization.JsonFieldType.String)
                                dateField = achievement.ObjectValue.GetField("DateEarned");

                            if (dateField.Type == Jamiras.IO.Serialization.JsonFieldType.String)
                            {
                                var date = DateTime.Parse(dateField.StringValue);
                                date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
                                stats.Achievements[id] = date;
                                stats.PointsEarned += Int32.Parse(achievement.ObjectValue.GetField("Points").StringValue);
                            }
                        }
                    }
                }
            }
            else
            {
                HardcoreMasteredUserCountEstimated = false;
            }

            Progress.Label = "Analyzing data";

            var idleTime = TimeSpan.FromHours(4);
            foreach (var user in userStats)
            {
                var times = new List<DateTime>(user.Achievements.Count);
                foreach (var achievement in user.Achievements)
                    times.Add(achievement.Value);

                times.Sort((l, r) => (int)((l - r).TotalSeconds));

                user.RealTime = times[times.Count - 1] - times[0];

                int start = 0, end = 0;
                while (end < times.Count)
                {
                    if (end + 1 == times.Count || (times[end + 1] - times[end]) >= idleTime)
                    {
                        user.GameTime += times[end] - times[start];
                        start = end + 1;
                    }

                    end++;
                }
            }

            userStats.Sort((l, r) => 
            {
                var diff = r.PointsEarned - l.PointsEarned;
                if (diff == 0)
                    diff = (int)((l.GameTime - r.GameTime).TotalSeconds);

                return diff;
            });

            HardcoreUserCount = userStats.Count;
            NonHardcoreUserCount = nonHardcoreUsers.Count;
            MedianHardcoreUserScore = userStats.Count > 0 ? userStats[userStats.Count / 2].PointsEarned : 0;

            int masteredCount = 0;
            while (masteredCount < userStats.Count && userStats[masteredCount].PointsEarned == 400)
                ++masteredCount;
            HardcoreMasteredUserCount = masteredCount;
            var timeToMaster = masteredCount > 0 ? userStats[masteredCount / 2].Summary : "n/a";
            var space = timeToMaster.IndexOf(' ');
            if (space > 0)
                timeToMaster = timeToMaster.Substring(0, space);
            MedianTimeToMaster = timeToMaster;

            if (userStats.Count > 100)
                userStats.RemoveRange(100, userStats.Count - 100);

            TopUsers = userStats;

            Progress.Label = String.Empty;
        }
    }
}
