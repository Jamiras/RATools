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
using System.IO;
using System.Linq;
using System.Text;

namespace RATools.ViewModels
{
    public class GameStatsViewModel : DialogViewModelBase
    {
        public GameStatsViewModel()
            : this(ServiceRepository.Instance.FindService<IBackgroundWorkerService>(),
                   ServiceRepository.Instance.FindService<IFileSystemService>(),
                   ServiceRepository.Instance.FindService<ISettings>())
        {
        }

        public GameStatsViewModel(IBackgroundWorkerService backgroundWorkerService, IFileSystemService fileSystem, ISettings settings)
        {
            _backgroundWorkerService = backgroundWorkerService;
            _fileSystem = fileSystem;
            _settings = settings;

            Progress = new ProgressFieldViewModel { Label = String.Empty };
            DialogTitle = "Game Stats";
            CanClose = true;

            SearchCommand = new DelegateCommand(Search);
            ShowUserUnlocksCommand = new DelegateCommand<UserStats>(ShowUserUnlocks);
            ShowUnlockHistoryCommand = new DelegateCommand<UserStats>(ShowUnlockHistory);
        }

        private readonly IBackgroundWorkerService _backgroundWorkerService;
        private readonly IFileSystemService _fileSystem;
        private readonly ISettings _settings;

        public ProgressFieldViewModel Progress { get; private set; }

        public static readonly ModelProperty GameIdProperty = ModelProperty.Register(typeof(GameStatsViewModel), "GameId", typeof(int), 0);

        public int GameId
        {
            get { return (int)GetValue(GameIdProperty); }
            set { SetValue(GameIdProperty, value); }
        }

        private string _gameName;

        public CommandBase SearchCommand { get; private set; }
        private void Search()
        {
            Progress.Label = "Fetching Game " + GameId;
            Progress.IsEnabled = true;
            _backgroundWorkerService.RunAsync(() =>
            {
                LoadGame(true, 100);
            });
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
            public string Description { get; set; }
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
            public bool Incomplete { get; set; }
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
                    if (Incomplete)
                        builder.Append(" (incomplete)");

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

        internal void LoadGame(bool allowFetchFromServer = false, int maxUserStats = Int32.MaxValue)
        {
            var userStats = new List<UserStats>();
            var achievementStats = new List<AchievementStats>();

            LoadGameFromFile(achievementStats, userStats, !allowFetchFromServer);

            if (allowFetchFromServer)
            {
                var gamePage = RAWebCache.Instance.GetGamePage(GameId);
                if (gamePage != null)
                {
                    // discard any previous achievement data to ensure we use the current information from the server
                    // keep the user data as the server only returns the 50 newest winners of each achievement
                    achievementStats.Clear();
                    LoadGameFromServer(gamePage, achievementStats, userStats);
                }
            }

            DialogTitle = "Game Stats - " + _gameName;

            if (achievementStats.Count > 0)
            {
                AnalyzeData(achievementStats, userStats);

                // only display the top N entries
                if (userStats.Count > maxUserStats)
                    userStats.RemoveRange(maxUserStats, userStats.Count - maxUserStats);
            }

            Achievements = achievementStats;
            TopUsers = userStats;

            Progress.Label = String.Empty;
        }

        private bool LoadGameFromFile(List<AchievementStats> achievementStats, List<UserStats> userStats, bool allowOldData)
        {
            var file = Path.Combine(_settings.DumpDirectory, GameId + ".txt");
            if (!_fileSystem.FileExists(file))
                return false;

            TotalPoints = 0;
            using (var reader = new StreamReader(_fileSystem.OpenFile(file, OpenFileMode.Read)))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line.StartsWith("NonHardCoreCaptured="))
                    {
                        NonHardcoreUserCount = Int32.Parse(line.Substring(20));
                        continue;
                    }

                    if (line.StartsWith("NumberOfPlayers="))
                    {
                        NumberOfPlayers = Int32.Parse(line.Substring(16));
                        continue;
                    }

                    if (line.StartsWith("Game="))
                    {
                        _gameName = line.Substring(5);
                        continue;
                    }

                    if (line.StartsWith("Achievement:"))
                    {
                        var stats = new AchievementStats();
                        var index = line.IndexOf(';');
                        var parts = line.Substring(0, index).Split(':', '=', '/');
                        stats.Id = Int32.Parse(parts[1]);
                        stats.EarnedHardcoreBy = Int32.Parse(parts[2]);
                        stats.EarnedBy = Int32.Parse(parts[3]);
                        stats.Points = Int32.Parse(parts[4]);
                        stats.Title = line.Substring(index + 1);

                        achievementStats.Add(stats);
                        TotalPoints += stats.Points;

                        while (!reader.EndOfStream)
                        {
                            line = reader.ReadLine();
                            if (line.Length < 1)
                                break;

                            parts = line.Split('@');

                            var user = new UserStats { User = parts[0] };
                            index = userStats.BinarySearch(user, user);
                            if (index < 0)
                                userStats.Insert(~index, user);
                            else
                                user = userStats[index];

                            user.Achievements[stats.Id] = DateTime.Parse(parts[1]);
                        }
                    }
                }
            }

            return true;
        }

        internal void LoadGameFromServer(string gamePage, List<AchievementStats> achievementStats, List<UserStats> userStats)
        {
            AchievementStats mostWon, leastWon;
            TotalPoints = LoadAchievementStatsFromServer(gamePage, achievementStats, out mostWon, out leastWon);

            var masteryPoints = (TotalPoints * 2).ToString();
            var masters = GetMastersFromServer(gamePage, masteryPoints);

            Progress.Label = "Fetching user stats";
            Progress.Reset(achievementStats.Count);

            achievementStats.Sort((l, r) =>
            {
                var diff = r.EarnedHardcoreBy - l.EarnedHardcoreBy;
                if (diff == 0)
                    diff = String.Compare(l.Title, r.Title, StringComparison.OrdinalIgnoreCase);

                return diff;
            });

            var possibleMasters = new List<string>();
            var nonHardcoreUsers = new List<string>();
            foreach (var achievement in achievementStats)
            {
                var achievementPage = RAWebCache.Instance.GetAchievementPage(achievement.Id);
                if (achievementPage != null)
                {
                    var tokenizer = Tokenizer.CreateTokenizer(achievementPage);
                    tokenizer.ReadTo("<h3>Winners</h3>");

                    // NOTE: this only lists the ~50 most recent unlocks! For games with more than 50 users who have mastered it, the oldest may be missed!
                    do
                    {
                        tokenizer.ReadTo("<a href='/user/");
                        if (tokenizer.NextChar == '\0')
                            break;

                        tokenizer.ReadTo("'>");
                        tokenizer.Advance(2);

                        // skip user image, we'll get the name from the text
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

                            if (ReferenceEquals(achievement, leastWon))
                            {
                                if (!masters.Contains(stats.User))
                                    possibleMasters.Add(stats.User);
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
            if (mostWon == null || mostWon.EarnedBy <= 50)
            {
                HardcoreMasteredUserCountEstimated = false;
            }
            else
            {
                HardcoreMasteredUserCountEstimated = (leastWon.EarnedBy > 50);

                bool incompleteData = false;
                possibleMasters.AddRange(masters);

                Progress.Reset(possibleMasters.Count);
                foreach (var user in possibleMasters)
                {
                    Progress.Current++;

                    var stats = new UserStats { User = user };
                    var index = userStats.BinarySearch(stats, stats);
                    if (index < 0)
                    {
                        userStats.Insert(~index, stats);
                    }
                    else
                    {
                        stats = userStats[index];
                        if (stats.Achievements.Count >= achievementStats.Count)
                            continue;
                    }

                    if (!incompleteData && !MergeUserGameMastery(stats))
                        incompleteData = true;

                    stats.Incomplete = incompleteData;
                }

                if (incompleteData)
                {
                    _backgroundWorkerService.InvokeOnUiThread(() =>
                    {
                        var settings = ServiceRepository.Instance.FindService<ISettings>();
                        if (String.IsNullOrEmpty(settings.ApiKey))
                            MessageBoxViewModel.ShowMessage("Data is limited without an ApiKey in the ini file.");
                        else
                            MessageBoxViewModel.ShowMessage("Failed to fetch mastery information. Please make sure the ApiKey value is up to date in your ini file.");
                    });
                }
            }

            NonHardcoreUserCount = nonHardcoreUsers.Count;

            WriteGameStats(_gameName, achievementStats, userStats);
        }

        public void RefreshUsers(List<UserStats> users)
        {
            var userStats = new List<UserStats>();
            var achievementStats = new List<AchievementStats>();

            if (!LoadGameFromFile(achievementStats, userStats, false))
                return;

            foreach (var user in users)
            {
                var stats = new UserStats { User = user.User };
                var index = userStats.BinarySearch(stats, stats);
                if (index < 0)
                    userStats.Insert(~index, stats);
                else
                    stats = userStats[index];

                MergeUserGameMastery(stats);

                foreach (var kvp in stats.Achievements)
                    user.Achievements[kvp.Key] = kvp.Value;
            }

            WriteGameStats(_gameName, achievementStats, userStats);
        }

        private bool MergeUserGameMastery(UserStats stats)
        {
            var masteryJson = RAWebCache.Instance.GetUserGameMasteryJson(stats.User, GameId);
            if (masteryJson == null)
                return false;

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
                    }
                }
            }

            return true;
        }

        private static List<string> GetMastersFromServer(string gamePage, string masteryPoints)
        {
            var masters = new List<string>();
            var tokenizer = Tokenizer.CreateTokenizer(gamePage);

            tokenizer.ReadTo("<div id='latestmasters'");
            var latestMasters = tokenizer.ReadTo("<div id='highscores'");

            // parse the Latest Masters
            var tokenizer2 = Tokenizer.CreateTokenizer(latestMasters);
            do
            {
                tokenizer2.ReadTo("<td class='user'>");
                if (tokenizer2.NextChar == '\0')
                    break;

                tokenizer2.ReadTo("<a href='");
                tokenizer2.ReadTo('>');
                tokenizer2.Advance();

                var userName = tokenizer2.ReadTo('<');

                masters.Add(userName.ToString());
            } while (true);

            // merge the users from High Scores who have all points
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

                if (!masters.Contains(userName.ToString()))
                    masters.Add(userName.ToString());
            } while (true);

            return masters;
        }

        private int LoadAchievementStatsFromServer(string gamePage, List<AchievementStats> allStats, out AchievementStats mostWon, out AchievementStats leastWon)
        {
            int totalPoints = 0;

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

                _gameName = titleString;
            }

            mostWon = null;
            leastWon = null;
            totalPoints = 0;
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

                    if (tokenizer.NextChar == '<')
                    {
                        tokenizer.ReadTo('>');
                        tokenizer.Advance();
                        tokenizer.SkipWhitespace();
                    }

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

                    tokenizer.ReadTo("<br>");
                    tokenizer.Advance(4);
                    var achievementDescription = tokenizer.ReadTo("<br>");
                    stats.Description = achievementDescription.Trim().ToString();
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

            return totalPoints;
        }

        private void AnalyzeData(List<AchievementStats> achievementStats, List<UserStats> userStats)
        {
            Progress.Label = "Analyzing data";

            var sessions = new List<int>(userStats.Count);
            var days = new List<int>(userStats.Count);
            var idleTime = TimeSpan.FromHours(4);
            foreach (var user in userStats)
            {
                if (user.Achievements.Count == 0)
                    continue;

                user.PointsEarned = 0;
                var times = new List<DateTime>(user.Achievements.Count);
                foreach (var achievement in user.Achievements)
                {
                    var achievementData = achievementStats.FirstOrDefault(a => a.Id == achievement.Key);
                    if (achievementData != null)
                        user.PointsEarned += achievementData.Points;

                    times.Add(achievement.Value);
                }

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

                // if the user mastered the set, capture data for median calculations
                if (user.PointsEarned == TotalPoints)
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
            else
            {
                HardcoreMasteredUserCount = 0;
                MedianTimeToMaster = "n/a";
                MedianSessionsToMaster = "n/a";
                MedianDaysToMaster = "n/a";
            }
        }

        private void WriteGameStats(string gameName, List<AchievementStats> achievementStats,
                                    List<UserStats> userStats)
        {
            if (String.IsNullOrEmpty(_settings.DumpDirectory))
                return;

            var filename = Path.Combine(_settings.DumpDirectory, GameId + ".txt");
            using (var file = new StreamWriter(_fileSystem.CreateFile(filename)))
            {
                if (gameName != null)
                    file.WriteLine("Game=" + gameName);

                file.WriteLine("NumberOfPlayers=" + NumberOfPlayers);
                file.WriteLine("HardCoreCaptured=" + userStats.Count);
                file.WriteLine("NonHardCoreCaptured=" + NonHardcoreUserCount);

                foreach (var achievement in achievementStats)
                {
                    file.WriteLine();
                    file.Write("Achievement:");
                    file.Write(achievement.Id);
                    file.Write('=');
                    file.Write(achievement.EarnedHardcoreBy);
                    file.Write('/');
                    file.Write(achievement.EarnedBy);
                    file.Write(':');
                    file.Write(achievement.Points);
                    file.Write(';');
                    file.Write(achievement.Title);
                    file.WriteLine();

                    foreach (var user in userStats)
                    {
                        DateTime when;
                        if (user.Achievements.TryGetValue(achievement.Id, out when))
                        {
                            file.Write(user.User);
                            file.Write('@');
                            file.Write(when.ToString("MM/dd/yyyy HH:mm:ss"));
                            file.WriteLine();
                        }
                    }
                }
            }
        }

        public DelegateCommand<UserStats> ShowUserUnlocksCommand { get; private set; }
        void ShowUserUnlocks(UserStats stats)
        {
            var url = "https://retroachievements.org/gamecompare.php?ID=" + GameId + "&f=" + stats.User;
            Process.Start(url);
        }

        [DebuggerDisplay("{Title} ({UnlockTime})")]
        public class AchievementUnlockInfo
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public DateTime? UnlockTime { get; set; }
        }

        public class UserHistoryViewModel : DialogViewModelBase
        {
            public UserHistoryViewModel()
            {
                CanClose = true;
            }

            public string Summary { get; set; }

            public List<AchievementUnlockInfo> Unlocks { get; set; }
        }

        public DelegateCommand<UserStats> ShowUnlockHistoryCommand { get; private set; }
        void ShowUnlockHistory(UserStats stats)
        {
            var vm = new UserHistoryViewModel();
            vm.DialogTitle = stats.User + " Unlocks for " + _gameName;
            vm.Summary = String.Format("{0}/{1} points earned in {2}", stats.PointsEarned, TotalPoints, stats.Summary);

            vm.Unlocks = new List<AchievementUnlockInfo>();
            foreach (var achievement in Achievements)
            {
                var unlockInfo = new AchievementUnlockInfo
                {
                    Id = achievement.Id,
                    Title = achievement.Title,
                    Description = achievement.Description
                };

                DateTime when;
                if (stats.Achievements.TryGetValue(achievement.Id, out when))
                    unlockInfo.UnlockTime = when;

                vm.Unlocks.Add(unlockInfo);
            }

            vm.Unlocks.Sort((l, r) => DateTime.Compare(l.UnlockTime.GetValueOrDefault(), r.UnlockTime.GetValueOrDefault()));
            vm.ShowDialog();
        }
    }
}
