using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.IO.Serialization;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Services;
using System;
using System.Collections;
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

            DetailedProgressCommand = new DelegateCommand(ShowDetailedProgress);
        }

        private readonly IBackgroundWorkerService _backgroundWorkerService;
        private readonly IFileSystemService _fileSystem;
        private readonly ISettings _settings;

        private List<GameProgressionViewModel.AchievementInfo> _progressionStats;

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
                Achievements = new Dictionary<int, DateTime>();
            }

            public string User { get; set; }
            public int PointsEarned { get; set; }
            public TimeSpan RealTime { get; set; }
            public TimeSpan GameTime { get; set; }
            public int Sessions { get; set; }
            public Dictionary<int, DateTime> Achievements { get; private set; }

            public int MasteryRank { get; set; }

            public bool IsEstimateReliable
            {
                get
                {
                    // if the use earned less than 3 achievements per session, the estimate will be off.
                    // don't use it for calculations.
                    return (Sessions == 1 || Sessions < Achievements.Count / 3);
                }
            }

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

            public void UpdateGameTime(Func<int, int> getAchievementPointsFunc, List<GameProgressionViewModel.AchievementInfo> progressionStats)
            {
                PointsEarned = 0;

                if (Achievements.Count == 0)
                    return;

                var times = new List<DateTime>(Achievements.Count);
                foreach (var achievement in Achievements)
                {
                    PointsEarned += getAchievementPointsFunc(achievement.Key);
                    times.Add(achievement.Value);
                }

                times.Sort((l, r) => (int)((l - r).TotalSeconds));

                RealTime = times[times.Count - 1] - times[0];

                var sessionStartIndices = new List<int>();
                sessionStartIndices.Add(0);
                var achievementSessions = new Dictionary<int, int>();

                var idleTime = TimeSpan.FromHours(4);
                int start = 0, end = 0;
                while (end < times.Count)
                {
                    foreach (var achievement in Achievements)
                    {
                        if (achievement.Value == times[end])
                            achievementSessions[achievement.Key] = Sessions;
                    }

                    if (end + 1 == times.Count || (times[end + 1] - times[end]) >= idleTime)
                    {
                        Sessions++;
                        GameTime += times[end] - times[start];
                        start = end + 1;
                        sessionStartIndices.Add(start);
                    }

                    end++;
                }

                // assume every achievement took roughly the same amount of time to earn. divide the user's total known playtime
                // by the number of achievements they've earned to get the approximate time per achievement earned. add this value
                // to each session to account for time played before getting the first achievement of the session and time played
                // after gettin the last achievement of the session.
                double perSessionAdjustment = GameTime.TotalSeconds / Achievements.Count;
                GameTime += TimeSpan.FromSeconds(Sessions * perSessionAdjustment);

                if (progressionStats != null)
                {
                    var sessionOffsets = new List<int>();
                    sessionOffsets.Add((int)perSessionAdjustment);
                    for (int i = 1; i < sessionStartIndices.Count - 1; i++)
                    {
                        var sessionLength = (times[sessionStartIndices[i] - 1] - times[sessionStartIndices[i - 1]]).TotalSeconds + perSessionAdjustment;
                        sessionOffsets.Add((int)(sessionOffsets[i - 1] + sessionLength));
                    }

                    // calculate the distance for each earned achievement from the start of the user's playtime
                    foreach (var achievement in achievementSessions)
                    {
                        var info = progressionStats.FirstOrDefault(a => a.Id == achievement.Key);
                        if (info != null)
                        {
                            var sessionStart = times[sessionStartIndices[achievement.Value]];
                            var elapsed = (Achievements[achievement.Key] - sessionStart).TotalSeconds + sessionOffsets[achievement.Value];
                            info.TotalDistance += TimeSpan.FromSeconds(elapsed);
                            info.TotalDistanceCount++;
                        }
                    }
                }
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

            TopUsers = new UserStats[0];

            LoadGameFromFile(achievementStats, userStats, !allowFetchFromServer);

            if (allowFetchFromServer)
            {
                var gameJson = RAWebCache.Instance.GetGameJson(GameId);
                if (gameJson != null)
                {
                    // discard any previous achievement data to ensure we use the current information from the server
                    // keep the user data as the server only returns the 50 newest winners of each achievement
                    achievementStats.Clear();
                    LoadGameFromServer(gameJson, achievementStats, userStats);
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

        internal void LoadGameFromServer(JsonObject gameJson, List<AchievementStats> achievementStats, List<UserStats> userStats)
        {
            _gameName = gameJson.GetField("Title").StringValue;
            NumberOfPlayers = gameJson.GetField("NumDistinctPlayersCasual").IntegerValue.GetValueOrDefault();

            var pagesNeeded = 0;
            int totalPoints = 0;
            if (gameJson.GetField("Achievements").Type == JsonFieldType.Object)
            {
                foreach (var pair in gameJson.GetField("Achievements").ObjectValue)
                {
                    var achievement = pair.ObjectValue;

                    AchievementStats stats = new AchievementStats();
                    stats.Id = achievement.GetField("ID").IntegerValue.GetValueOrDefault();
                    stats.Title = achievement.GetField("Title").StringValue;
                    stats.Description = achievement.GetField("Description").StringValue;
                    stats.Points = achievement.GetField("Points").IntegerValue.GetValueOrDefault();
                    totalPoints += stats.Points;

                    stats.EarnedHardcoreBy = achievement.GetField("NumAwardedHardcore").IntegerValue.GetValueOrDefault();
                    stats.EarnedBy = achievement.GetField("NumAwarded").IntegerValue.GetValueOrDefault();
                    pagesNeeded++;// += (stats.EarnedBy + RAWebCache.AchievementUnlocksPerPage - 1) / RAWebCache.AchievementUnlocksPerPage;

                    achievementStats.Add(stats);
                }
            }

            TotalPoints = totalPoints;

            Progress.Label = "Fetching unlocks";
            Progress.Reset(pagesNeeded);

            foreach (var achievement in achievementStats)
            {
                int pages = (achievement.EarnedBy + RAWebCache.AchievementUnlocksPerPage - 1) / RAWebCache.AchievementUnlocksPerPage;
                if (pages > 1)
                {
                    HardcoreMasteredUserCountEstimated = true;
                    pages = 1;
                }

                for (int i = 0; i < pages; i++)
                {
                    var unlocksJson = RAWebCache.Instance.GetAchievementUnlocksJson(achievement.Id, i);
                    if (unlocksJson != null)
                    {
                        foreach (var unlock in unlocksJson.GetField("Unlocks").ObjectArrayValue)
                        {
                            if (unlock.GetField("HardcoreMode").IntegerValue != 1)
                                continue;

                            string user = unlock.GetField("User").StringValue;
                            var stats = new UserStats { User = user };
                            var index = userStats.BinarySearch(stats, stats);
                            if (index < 0)
                                userStats.Insert(~index, stats);
                            else
                                stats = userStats[index];

                            stats.Achievements[achievement.Id] = unlock.GetField("DateAwarded").DateTimeValue.GetValueOrDefault();
                        }
                    }

                    Progress.Current++;
                }
            }

            if (HardcoreMasteredUserCountEstimated)
            {
                int masterCount = 0;
                foreach (var user in userStats)
                {
                    if (user.Achievements.Count == achievementStats.Count)
                        masterCount++;
                }

                var distribution = RAWebCache.Instance.GetGameAchievementDistribution(GameId);
                if (distribution != null)
                {
                    int actualNumberOfMasteries = distribution.GetField(achievementStats.Count.ToString()).IntegerValue.GetValueOrDefault();
                    if (actualNumberOfMasteries == masterCount)
                        HardcoreMasteredUserCountEstimated = false;
                }

                if (HardcoreMasteredUserCountEstimated)
                {
                    var topScores = RAWebCache.Instance.GetGameTopScores(GameId);
                    if (topScores != null)
                    {
                        foreach (var topScore in topScores.GetField("items").ObjectArrayValue)
                        {
                            if (topScore.GetField("TotalScore").IntegerValue == totalPoints * 2)
                            {
                                string user = topScore.GetField("User").StringValue;
                                var stats = new UserStats { User = user };
                                var index = userStats.BinarySearch(stats, stats);
                                if (index < 0)
                                    userStats.Insert(~index, stats);
                                else
                                    stats = userStats[index];

                                if (stats.Achievements.Count < achievementStats.Count)
                                    MergeUserGameMastery(stats);
                            }
                            else
                            {
                                HardcoreMasteredUserCountEstimated = false;
                            }
                        }
                    }
                }
            }

            NonHardcoreUserCount = NumberOfPlayers -
                gameJson.GetField("NumDistinctPlayersHardcore").IntegerValue.GetValueOrDefault();

            WriteGameStats(_gameName, achievementStats, userStats);
        }

        public void RefreshUsers(List<UserStats> users)
        {
            var userStats = new List<UserStats>();
            var achievementStats = new List<AchievementStats>();

            bool updateFile = LoadGameFromFile(achievementStats, userStats, false);

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

            if (updateFile)
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
                    if (dateField.Type != JsonFieldType.String)
                        dateField = achievement.ObjectValue.GetField("DateEarned");

                    if (dateField.Type == JsonFieldType.String)
                        stats.Achievements[id] = dateField.DateTimeValue.GetValueOrDefault();
                }
            }

            return true;
        }

        private void AnalyzeData(List<AchievementStats> achievementStats, List<UserStats> userStats)
        {
            Progress.Label = "Analyzing data";

            // initialize the progression stats
            _progressionStats = new List<GameProgressionViewModel.AchievementInfo>();
            foreach (var achievement in achievementStats)
            {
                _progressionStats.Add(new GameProgressionViewModel.AchievementInfo
                {
                    Id = achievement.Id,
                    Title = achievement.Title,
                });
            }

            // estimate the time spent for each user
            foreach (var user in userStats)
            {
                user.UpdateGameTime(id =>
                {
                    var achievement = achievementStats.FirstOrDefault(a => a.Id == id);
                    return (achievement != null) ? achievement.Points : 0;
                }, _progressionStats);
            }

            // sort the results by the most points earned, then the quickest
            userStats.Sort((l, r) => 
            {
                var diff = r.PointsEarned - l.PointsEarned;
                if (diff == 0)
                    diff = (int)((l.GameTime - r.GameTime).TotalSeconds);

                return diff;
            });

            // finalize the progression stats
            foreach (var info in _progressionStats)
            {
                if (info.TotalDistanceCount > 0)
                    info.Distance = TimeSpan.FromSeconds(info.TotalDistance.TotalSeconds / info.TotalDistanceCount);
                else
                    info.Distance = TimeSpan.MaxValue;
            }

            _progressionStats.Sort((l, r) =>
            {
                // can't just return the difference between two distances, as it's entirely
                // possible that a value could be a few milliseconds or several years, so
                // there's no easy way to convert the different into a 32-bit integer.
                if (l.Distance == r.Distance)
                    return 0;
                return (l.Distance > r.Distance) ? 1 : -1;
            });

            // determine how many players mastered the set and how many sessions/days it took them
            var sessions = new List<int>(32);
            var days = new List<int>(32);
            var reliableEstimateIndices = new List<int>(32);
            int masteredCount = 0;
            foreach (var user in userStats)
            {
                if (user.PointsEarned != TotalPoints)
                    break;

                if (user.IsEstimateReliable)
                {
                    reliableEstimateIndices.Add(masteredCount);
                    sessions.Add(user.Sessions);
                    days.Add((int)Math.Ceiling(user.RealTime.TotalDays));
                }

                user.MasteryRank = ++masteredCount;
            }

            HardcoreUserCount = userStats.Count;
            MedianHardcoreUserScore = userStats.Count > 0 ? userStats[userStats.Count / 2].PointsEarned : 0;
            HardcoreMasteredUserCount = masteredCount;

            int sessionCount = sessions.Count;
            if (sessionCount > 0)
            {
                // sessionCount is only the number of reliable estimates. Find the median index of those, and use
                // that record's time as the median time
                var medianIndex = reliableEstimateIndices[sessionCount / 2];
                var timeToMaster = userStats[medianIndex].Summary;
                var space = timeToMaster.IndexOf(' ');
                if (space > 0)
                    timeToMaster = timeToMaster.Substring(0, space);
                MedianTimeToMaster = timeToMaster;

                sessions.Sort();
                MedianSessionsToMaster = sessions[sessionCount / 2].ToString();

                days.Sort();
                MedianDaysToMaster = days[sessionCount / 2].ToString();
            }
            else
            {
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
            ServiceRepository.Instance.FindService<IBrowserService>().OpenUrl(url);
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

        public DelegateCommand DetailedProgressCommand { get; private set; }

        private void ShowDetailedProgress()
        {
            var vm = new GameProgressionViewModel(_progressionStats);
            vm.DialogTitle += " - " + _gameName;
            vm.ShowDialog();
        }
    }
}
