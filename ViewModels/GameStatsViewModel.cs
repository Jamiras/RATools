﻿using Jamiras.Commands;
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
            public int Sessions { get; set; }
            public TinyDictionary<int, DateTime> Achievements { get; private set; }

            public string Summary
            {
                get
                {
                    var builder = new StringBuilder();
                    builder.AppendFormat("{0}h{1:D2}m", (int)GameTime.TotalHours, GameTime.Minutes);
                    if (Sessions > 1)
                        builder.AppendFormat(" in {0} sessions", Sessions);
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

        public static readonly ModelProperty MedianSessionsToMasterProperty = ModelProperty.Register(typeof(GameStatsViewModel), "MedianSessionsToMaster", typeof(string), "n/a");

        public string MedianSessionsToMaster
        {
            get { return (string)GetValue(MedianSessionsToMasterProperty); }
            private set { SetValue(MedianSessionsToMasterProperty, value); }
        }

        public static readonly ModelProperty MedianDaysToMasterProperty = ModelProperty.Register(typeof(GameStatsViewModel), "MedianDaysToMaster", typeof(string), "n/a");

        public string MedianDaysToMaster
        {
            get { return (string)GetValue(MedianDaysToMasterProperty); }
            private set { SetValue(MedianDaysToMasterProperty, value); }
        }

        public static readonly ModelProperty TopUsersProperty = ModelProperty.Register(typeof(GameStatsViewModel), "TopUsers", typeof(IEnumerable<UserStats>), new UserStats[0]);

        public IEnumerable<UserStats> TopUsers
        {
            get { return (IEnumerable<UserStats>)GetValue(TopUsersProperty); }
            private set { SetValue(TopUsersProperty, value); }
        }

        public static readonly ModelProperty NumberOfPlayersProperty = ModelProperty.Register(typeof(GameStatsViewModel), "NumberOfPlayers", typeof(int), 0);

        public int NumberOfPlayers
        {
            get { return (int)GetValue(NumberOfPlayersProperty); }
            private set { SetValue(NumberOfPlayersProperty, value); }
        }

        public static readonly ModelProperty TotalPointsProperty = ModelProperty.Register(typeof(GameStatsViewModel), "TotalPoints", typeof(int), 400);

        public int TotalPoints
        {
            get { return (int)GetValue(TotalPointsProperty); }
            private set { SetValue(TotalPointsProperty, value); }
        }

        internal void LoadGame()
        {
            var gamePage = RAWebCache.Instance.GetGamePage(GameId);
            if (gamePage == null)
                return;

            var tokenizer = Tokenizer.CreateTokenizer(gamePage);
            tokenizer.ReadTo("<title>");
            if (tokenizer.Match("<title>"))
            {
                var title = tokenizer.ReadTo("</title>");
                var titleString = title.ToString();
                var index = title.IndexOf("RetroAchievements", StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    var length = 17;
                    if (index > 3 && title.SubToken(index - 3, 3) == " - ")
                    {
                        index -= 3;
                        length += 3;
                    }

                    if (index + length < title.Length - 4 && title.SubToken(index + length, 4) == ".org")
                        length += 4;

                    if (index + length < title.Length - 3 && title.SubToken(index + length, 3) == " - ")
                        length += 3;

                    titleString = titleString.Substring(0, index) + titleString.Substring(index + length);
                }

                DialogTitle = "Game Stats - " + titleString;
            }

            AchievementStats mostWon = null;
            AchievementStats leastWon = null;
            var totalPoints = 0;

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

                if (stats.EarnedBy > 0)
                {
                    tokenizer.SkipWhitespace();
                    if (tokenizer.NextChar == '(')
                    {
                        tokenizer.Advance();
                        var hardcoreWinners = tokenizer.ReadNumber();
                        stats.EarnedHardcoreBy = Int32.Parse(hardcoreWinners.ToString());
                    }
                }

                if (NumberOfPlayers == 0)
                {
                    tokenizer.ReadTo("of ");
                    tokenizer.Advance(3);
                    var players = tokenizer.ReadNumber();
                    NumberOfPlayers = Int32.Parse(players.ToString());
                }

                tokenizer.ReadTo("<a href='/achievement/");
                if (tokenizer.Match("<a href='/achievement/"))
                {
                    var achievementId = tokenizer.ReadTo("'>");
                    stats.Id = Int32.Parse(achievementId.ToString());
                    tokenizer.Advance(2);

                    var achievementTitle = tokenizer.ReadTo("</a>").TrimRight();
                    Token achievementPoints = Token.Empty;
                    if (achievementTitle.EndsWith(")"))
                    {
                        for (int i = achievementTitle.Length - 1; i >= 0; i--)
                        {
                            if (achievementTitle[i] == '(')
                            {
                                achievementPoints = achievementTitle.SubToken(i + 1, achievementTitle.Length - i - 2);
                                achievementTitle = achievementTitle.SubToken(0, i);
                                break;
                            }
                        }
                    }

                    stats.Title = achievementTitle.TrimRight().ToString();

                    int points;
                    if (Int32.TryParse(achievementPoints.ToString(), out points))
                        stats.Points = points;
                }

                allStats.Add(stats);
                totalPoints += stats.Points;

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

            var masteryPoints = (totalPoints * 2).ToString();
            TotalPoints = totalPoints;

            var masters = new List<string>();
            tokenizer = Tokenizer.CreateTokenizer(gamePage);
            tokenizer.ReadTo("<div id='highscores'");
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
                if (points != masteryPoints)
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

            if (allStats.Count == 0)
            {
                TopUsers = new List<UserStats>();
                Progress.Label = String.Empty;
                return;
            }

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

                    if (stats.PointsEarned == totalPoints)
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

            var sessions = new List<int>(userStats.Count);
            var days = new List<int>(userStats.Count);
            var idleTime = TimeSpan.FromHours(4);
            foreach (var user in userStats)
            {
                if (user.Achievements.Count == 0)
                    continue;

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
                        user.Sessions++;
                        user.GameTime += times[end] - times[start];
                        start = end + 1;
                    }

                    end++;
                }

                // assume every achievement took roughly the same amount of time to earn. divide the user's total known playtime
                // by the number of achievements they've earned to get the approximate time per achievement earned. add this value
                // to each session to account for time played before getting the first achievement of the session and time played
                // after gettin the last achievement of the session.
                double perSessionAdjustment = user.GameTime.TotalSeconds / user.Achievements.Count;
                user.GameTime += TimeSpan.FromSeconds(user.Sessions * perSessionAdjustment);

                if (user.PointsEarned == totalPoints)
                {
                    sessions.Add(user.Sessions);
                    days.Add((int)Math.Ceiling(user.RealTime.TotalDays));
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

            int masteredCount = sessions.Count;
            if (masteredCount > 0)
            {
                HardcoreMasteredUserCount = masteredCount;
                var timeToMaster = masteredCount > 0 ? userStats[masteredCount / 2].Summary : "n/a";
                var space = timeToMaster.IndexOf(' ');
                if (space > 0)
                    timeToMaster = timeToMaster.Substring(0, space);
                MedianTimeToMaster = timeToMaster;

                sessions.Sort();
                MedianSessionsToMaster = sessions[masteredCount / 2].ToString();

                days.Sort();
                MedianDaysToMaster = days[masteredCount / 2].ToString();
            }

            if (userStats.Count > 100)
                userStats.RemoveRange(100, userStats.Count - 100);

            TopUsers = userStats;

            Progress.Label = String.Empty;
        }
    }
}
