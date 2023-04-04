using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.IO.Serialization;
using Jamiras.Services;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RATools.ViewModels
{
    public class MasteryViewModel : DialogViewModelBase
    {
        private const int CountPerSection = 20;

        public MasteryViewModel()
            : this(ServiceRepository.Instance.FindService<IBackgroundWorkerService>(), ServiceRepository.Instance.FindService<ISettings>())
        {
        }

        public MasteryViewModel(IBackgroundWorkerService backgroundWorkerService, ISettings settings)
        {
            _backgroundWorkerService = backgroundWorkerService;
            _settings = settings;

            Progress = new ProgressFieldViewModel { Label = String.Empty };
            DialogTitle = "Mastery Analyzer";
            CanClose = true;

            Snapshot = new GameDataSnapshotViewModel(Progress, backgroundWorkerService, settings);
            Snapshot.DataRefreshed += Snapshot_DataRefreshed;

            Results = new ObservableCollection<MasteryStats>();

            RefreshCommand = new DelegateCommand(RefreshGames);
            ExportCommand = new DelegateCommand(Export);
            SummarizeCommand = new DelegateCommand(Summarize);

            OpenGameCommand = new DelegateCommand<Result>(OpenGame);

            AddPropertyChangedHandler(DialogResultProperty, OnDialogResultPropertyChanged);
        }

        private readonly IBackgroundWorkerService _backgroundWorkerService;
        private readonly ISettings _settings;

        public ProgressFieldViewModel Progress { get; private set; }

        public GameDataSnapshotViewModel Snapshot { get; private set; }

        private void OnDialogResultPropertyChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            // stop any ongoing operations
            Progress.IsEnabled = false;
        }

        public class Result
        {
            public int GameId;
            public string GameName;
        }

        public CommandBase<Result> OpenGameCommand { get; private set; }
        private void OpenGame(Result result)
        {
            var url = "https://retroachievements.org/game/" + result.GameId;
            ServiceRepository.Instance.FindService<IBrowserService>().OpenUrl(url);
        }

        private void Snapshot_DataRefreshed(object sender, EventArgs e)
        {
            _backgroundWorkerService.RunAsync(() => CalculateMastery(false));
        }

        public CommandBase RefreshCommand { get; private set; }
        private void RefreshGames()
        {
            _backgroundWorkerService.RunAsync(() =>
            {
                RAWebCache.ExpireHours = 96;
                CalculateMastery(true);
                RAWebCache.ExpireHours = 0;
            });
        }

        private void CalculateMastery(bool refresh)
        {
            Progress.Reset(Snapshot.AchievementGameCount);
            Progress.IsEnabled = true;

            var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var masteryStats = new List<MasteryStats>();
            var mostAwardedAchievements = new List<GameStatsViewModel.AchievementStats>();

            var achievementIds = new List<int>(Snapshot.GamesWithAchievements);
            achievementIds.Sort();
            foreach (var gameId in achievementIds)
            {
                Progress.Label = "Processing game " + gameId;
                Progress.Current++;

                if (!Progress.IsEnabled)
                    break;

                string gameName = "";
                int created = Int32.MaxValue;
                int consoleId = 0;
                string consoleName = "Unknown";
                using (var stream = File.OpenRead(Path.Combine(_settings.DumpDirectory, gameId + ".json")))
                {
                    var json = new JsonObject(stream);
                    var patchData = json.GetField("PatchData").ObjectValue;
                    var achievements = patchData.GetField("Achievements");
                    if (achievements.Type != JsonFieldType.ObjectArray)
                        continue;

                    if (!achievements.ObjectArrayValue.Any(a => a.GetField("Flags").IntegerValue != 5))
                        continue;

                    foreach (var achievement in achievements.ObjectArrayValue)
                    {
                        var createdValue = achievement.GetField("Created").IntegerValue.GetValueOrDefault();
                        if (createdValue > 0 && createdValue < created)
                            created = createdValue;
                    }

                    gameName = patchData.GetField("Title").StringValue;
                    consoleId = patchData.GetField("ConsoleID").IntegerValue.GetValueOrDefault();
                    consoleName = patchData.GetField("ConsoleName").StringValue;
                }

                if (consoleId >= 100) // ignore Hubs and Events
                    continue;

                var gameStats = new GameStatsViewModel() { GameId = gameId };
                Debug.WriteLine(String.Format("{0} processing {1}", DateTime.Now, gameId));
                gameStats.LoadGame(refresh);

                if (!gameStats.Achievements.Any())
                    continue;

                var twentyFifthPlayers = gameStats.NumberOfPlayers * 1 / 4;
                var fiftiethPlayers = gameStats.NumberOfPlayers / 2;
                var seventyFifthPlayers = gameStats.NumberOfPlayers * 3 / 4;
                var ninetiethPlayers = gameStats.NumberOfPlayers * 9 / 10;

                var twentyFifthPercentilePoints = 0;
                var fiftiethPercentilePoints = 0;
                var seventyFifthPercentilePoints = 0;
                var ninetiethPercentilePoints = 0;
                var ninetiethPercentileAchievements = 0;
                foreach (var achievement in gameStats.Achievements)
                {
                    if (achievement.EarnedBy > twentyFifthPlayers)
                    {
                        twentyFifthPercentilePoints += achievement.Points;
                        if (achievement.EarnedBy >= fiftiethPlayers)
                        {
                            fiftiethPercentilePoints += achievement.Points;
                            if (achievement.EarnedBy >= seventyFifthPlayers)
                            {
                                seventyFifthPercentilePoints += achievement.Points;
                                if (achievement.EarnedBy >= ninetiethPlayers)
                                {
                                    ninetiethPercentilePoints += achievement.Points;
                                    ninetiethPercentileAchievements++;
                                }
                            }
                        }
                    }

                    int i = 0;
                    while (i < mostAwardedAchievements.Count)
                    {
                        if (achievement.EarnedBy > mostAwardedAchievements[i].EarnedBy)
                            break;
                        ++i;
                    }
                    if (i < 20)
                    {
                        if (mostAwardedAchievements.Count == 20)
                            mostAwardedAchievements.RemoveAt(19);

                        achievement.Description = gameName;
                        mostAwardedAchievements.Insert(i, achievement);
                    }
                }

                var standardDeviation = 0.0;
                var mean = 0.0;
                if (gameStats.HardcoreMasteredUserCount > 0)
                {
                    var times = new List<double>();
                    foreach (var user in gameStats.TopUsers)
                    {
                        if (user.PointsEarned == gameStats.TotalPoints && user.IsEstimateReliable)
                            times.Add(user.GameTime.TotalMinutes);
                    }

                    if (times.Count > 0)
                    {
                        mean = times.Average();
                        if (gameStats.HardcoreMasteredUserCountEstimated)
                            standardDeviation = StandardDeviation.CalculateFromSample(times);
                        else
                            standardDeviation = StandardDeviation.Calculate(times);
                    }
                }

                var minutesPerPoint = new List<float>();
                foreach (var user in gameStats.TopUsers)
                {
                    if (user.PointsEarned > 0)
                        minutesPerPoint.Add((float)user.GameTime.TotalMinutes / user.PointsEarned);
                }
                minutesPerPoint.Sort();

                var stats = new MasteryStats
                {
                    GameId = gameStats.GameId,
                    GameName = gameStats.DialogTitle.Substring(12).Trim(),
                    ConsoleId = consoleId,
                    ConsoleName = consoleName,
                    Created = unixEpoch + TimeSpan.FromSeconds(created),
                    Points = gameStats.TotalPoints,
                    NumPlayers = gameStats.NumberOfPlayers,
                    HardcoreMasteredUserCount = gameStats.HardcoreMasteredUserCount,
                    MeanTimeToMaster = mean,
                    StdDevTimeToMaster = standardDeviation,
                    MinutesPerPointToMaster = mean / gameStats.TotalPoints,
                    MinutesPerPoint = minutesPerPoint.Count > 0 ? minutesPerPoint[minutesPerPoint.Count / 2] : 0,
                    TwentyFifthPercentilePoints = twentyFifthPercentilePoints,
                    FiftiethPercentilePoints = fiftiethPercentilePoints,
                    SeventyFifthPercentilePoints = seventyFifthPercentilePoints,
                    NintiethPercentilePoints = ninetiethPercentilePoints,
                    NintiethPercentileAchievements = ninetiethPercentileAchievements,
                };

                if (gameStats.NumberOfPlayers == 0)
                {
                    stats.PlayersPerDay = 0.0;
                }
                else
                {
                    var age = (DateTime.Now - stats.Created).TotalDays;
                    stats.PlayersPerDay = gameStats.NumberOfPlayers / age;
                }

                masteryStats.Add(stats);
            }

            masteryStats.Sort((l, r) =>
            {
                if (l.MinutesPerPoint < r.MinutesPerPoint)
                    return -1;
                else if (l.MinutesPerPoint > r.MinutesPerPoint)
                    return 1;
                return 0;
            });

            _backgroundWorkerService.InvokeOnUiThread(() =>
            {
                Results.Clear();
                foreach (var stats in masteryStats)
                    Results.Add(stats);
            });

            _mostAwardedAchievements = mostAwardedAchievements;

            Progress.Label = String.Empty;
        }

        public class MasteryStats
        {
            public int GameId { get; set; }
            public string GameName { get; set; }
            public int ConsoleId { get; set; }
            public string ConsoleName { get; set; }
            public DateTime Created { get; set; }
            public int Points { get; set; }
            public int NumPlayers { get; set; }
            public int HardcoreMasteredUserCount { get; set; }
            public double MeanTimeToMaster { get; set; }
            public double StdDevTimeToMaster { get; set; }
            public double MinutesPerPointToMaster { get; set; }
            public double MinutesPerPoint { get; set; }
            public int TwentyFifthPercentilePoints { get; set; }
            public int FiftiethPercentilePoints { get; set; }
            public int SeventyFifthPercentilePoints { get; set; }
            public int NintiethPercentilePoints { get; set; }
            public int NintiethPercentileAchievements { get; set; }
            public double PlayersPerDay { get; set; }
        }

        public ObservableCollection<MasteryStats> Results { get; private set; }

        private List<GameStatsViewModel.AchievementStats> _mostAwardedAchievements;

        public CommandBase ExportCommand { get; private set; }
        private void Export()
        {
            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Export search results";
            vm.Filters["CSV file"] = "*.csv";
            vm.FileNames = new[] { "results.csv" };
            vm.OverwritePrompt = true;

            if (vm.ShowSaveFileDialog() == DialogResult.Ok)
            {
                using (var file = File.CreateText(vm.FileNames[0]))
                {
                    file.WriteLine("Id,Title,ConsoleId,Created,Age," + 
                                   "Points,Players,TimesMastered," +
                                   "MeanTimeToMaster,StdDevTimeToMaster," +
                                   "MinutesPerPointToMaster,MinutesPerPoint," +
                                   "TwentyFifthPercentilePoints,FiftiethPercentilePoints,SeventyFifthPercentilePoints,NintiethPercentilePoints");

                    foreach (var game in Results)
                    {
                        file.Write("{0},\"{1}\",", game.GameId, game.GameName.Replace("\"", "\\\""));
                        file.Write("{0},{1},{2},", game.ConsoleId, game.Created, (DateTime.Today - game.Created).TotalDays);
                        file.Write("{0},{1},{2},", game.Points, game.NumPlayers, game.HardcoreMasteredUserCount);
                        file.Write("{0},{1},", game.MeanTimeToMaster, game.StdDevTimeToMaster);
                        file.Write("{0},{1},", game.MinutesPerPointToMaster, game.MinutesPerPoint);
                        file.Write("{0},{1},{2},{3}", game.TwentyFifthPercentilePoints, game.FiftiethPercentilePoints, game.SeventyFifthPercentilePoints, game.NintiethPercentilePoints);
                        file.WriteLine();
                    }
                }
            }
        }

        public CommandBase SummarizeCommand { get; private set; }
        private void Summarize()
        {
            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Export summary results";
            vm.Filters["TXT file"] = "*.txt";
            vm.FileNames = new[] { "mastery.txt" };
            vm.OverwritePrompt = true;

            if (vm.ShowSaveFileDialog() == DialogResult.Ok)
            {
                _backgroundWorkerService.RunAsync(() => SaveSummary(vm.FileNames[0]));
            }
        }

        private void SaveSummary(string filename)
        {
            var results = new List<MasteryStats>(Results);
            results.RemoveAll(r => r.GameName.Contains("[Subset - Bonus]") || r.GameName.Contains("[Subset - Multi]") || r.ConsoleId >= 100);

            DateTime now = DateTime.Now;

            var cheaters = new List<CheaterInfo>();
            var cheatedGames = new List<CheatedGameInfo>();
            results.Sort((l, r) =>
            {
                if (l == null)
                    return -1;
                if (r == null)
                    return 1;
                return (l.GameId - r.GameId);
            });

            var detailedUserMasteryInfo = new List<string>();

            Progress.Label = "Analyzing data...";
            Progress.Reset(results.Count);
            Progress.IsEnabled = true;
            foreach (var result in results)
            {
                ++Progress.Current;

                GameStatsViewModel gameStats = null;

                // if the set has less than 8 masteries, we can't assume the median and standard deviation are
                // a viable gauge of cheating
                if (result.HardcoreMasteredUserCount < 8 || result.Points < 50)
                    continue;

                // identify anyone who completes the set more than two standard deviations faster than the average user.
                // if two standard deviations is more than 90% faster than median, look for anyone who is more than 90% faster than median.
                var threshold = Math.Max(result.MeanTimeToMaster / 10, result.MeanTimeToMaster - result.StdDevTimeToMaster * 2);

                if (gameStats == null)
                {
                    gameStats = new GameStatsViewModel() { GameId = result.GameId };
                    gameStats.LoadGame();
                }

                var usersToRefresh = new List<GameStatsViewModel.UserStats>();
                CheatedGameInfo gameEntry = null;

                foreach (var user in gameStats.TopUsers)
                {
                    if (user.PointsEarned == result.Points && user.GameTime.TotalMinutes < threshold)
                    {
                        // if the user isn't averaging at least three achievements per session, the
                        // estimate will be off. ignore it.
                        if (!user.IsEstimateReliable)
                            continue;

                        // some things that appear like cheating aren't. check the exceptions list.
                        if (IgnoreCheater(user, result.GameId))
                            continue;

                        // if the user data contains a bunch of entries without seconds, it's old. try refreshing it
                        if (user.Achievements.Count(a => a.Value.Second == 0) > gameStats.Achievements.Count() / 2)
                            usersToRefresh.Add(user);

                        // add a new cheating entry for the user
                        var entry = cheaters.FirstOrDefault(c => c.UserName == user.User);
                        if (entry == null)
                        {
                            entry = new CheaterInfo()
                            {
                                UserName = user.User,
                                Results = new List<KeyValuePair<MasteryStats, GameStatsViewModel.UserStats>>()
                            };
                            cheaters.Add(entry);
                        }
                        entry.Results.Add(new KeyValuePair<MasteryStats, GameStatsViewModel.UserStats>(result, user));

                        // add a new cheating entry for the game
                        if (gameEntry == null)
                        {
                            gameEntry = new CheatedGameInfo()
                            {
                                Game = result,
                                Users = new List<GameStatsViewModel.UserStats>()
                            };
                            cheatedGames.Add(gameEntry);
                        }
                        gameEntry.Users.Add(user);
                    }
                }

                if (usersToRefresh.Count > 0)
                    gameStats.RefreshUsers(usersToRefresh);
            }

            cheaters.Sort((l, r) =>
            {
                int diff = (r.Results.Count - l.Results.Count);
                if (diff == 0)
                    diff = String.Compare(l.UserName, r.UserName);
                return diff;
            });

            using (var file = File.CreateText(filename))
            {
                WriteSummaryAndTopLists(file, results);
                WritePossibleCheaters(file, cheaters);

                file.WriteLine("Most cheated games: Count|ID|Name [most frequent games from previous list]");
                file.WriteLine("```");
                cheatedGames.Sort((l, r) =>
                {
                    int diff = (r.Users.Count - l.Users.Count);
                    if (diff == 0)
                        diff = String.Compare(l.Game.GameName, r.Game.GameName);
                    return diff;
                });
                for (int i = 0, count = 0; (count < CountPerSection && cheatedGames[i].Users.Count > 1) || (i > 0 && cheatedGames[i].Users.Count == cheatedGames[i - 1].Users.Count); i++)
                {
                    file.WriteLine(String.Format("{0,2:D} {1,5:D} {2}", cheatedGames[i].Users.Count,
                        cheatedGames[i].Game.GameId, cheatedGames[i].Game.GameName));
                }
                file.WriteLine("```");
                file.WriteLine();
            }

            Progress.Label = String.Empty;
        }

        private void WriteSummaryAndTopLists(StreamWriter file, List<MasteryStats> results)
        {
            DateTime thirtyDaysAgo = DateTime.Today - TimeSpan.FromDays(30);

            file.WriteLine("Games:         {0,6:D}", Snapshot.GameCount);
            file.WriteLine("Achievements:  {0,6:D} ({1} games with achievements)", Snapshot.AchievementCount, Snapshot.AchievementGameCount);
            file.WriteLine("Leaderboards:  {0,6:D} ({1} games with leaderboards)", Snapshot.LeaderboardCount, Snapshot.LeaderboardGameCount);
            file.WriteLine("RichPresences: {0,6:D} ({1} static)", Snapshot.RichPresenceCount, Snapshot.StaticRichPresenceCount);
            file.WriteLine("Authors:       {0,6:D}", Snapshot.AuthorCount);
            file.WriteLine("Systems:       {0,6:D}", Snapshot.SystemCount);
            file.WriteLine();

            file.WriteLine("Most played: MAX(Players)");
            file.WriteLine("```");
            results.Sort((l, r) => l.NumPlayers - r.NumPlayers);
            for (int i = results.Count - 1, count = 0; count < CountPerSection; i--, count++)
                file.WriteLine(String.Format("{0,5:D} {1}", results[i].NumPlayers, results[i].GameName));
            file.WriteLine("```");
            file.WriteLine();

            file.WriteLine("Least played: MIN(Players) [Players > 0, Age > 30 days]");
            file.WriteLine("```");
            for (int i = 0, count = 0; count < CountPerSection || results[i].NumPlayers == results[i - 1].NumPlayers; i++)
            {
                if (results[i].NumPlayers > 0 && results[i].Created < thirtyDaysAgo)
                {
                    file.WriteLine(String.Format("{0,5:D} {1}", results[i].NumPlayers, results[i].GameName));
                    count++;
                }
            }
            file.WriteLine("```");
            file.WriteLine();

            file.WriteLine("Most Popular: MAX(Players/Day) [Age > 30 days]");
            file.WriteLine("```");
            results.Sort((l, r) => (int)((l.PlayersPerDay - r.PlayersPerDay) * 100000));
            for (int i = results.Count - 1, count = 0; count < CountPerSection; i--)
            {
                if (results[i].NumPlayers > 0 && results[i].Created < thirtyDaysAgo)
                {
                    file.WriteLine(String.Format("{0:F3} {1}", results[i].PlayersPerDay, results[i].GameName));
                    count++;
                }
            }
            file.WriteLine("```");
            file.WriteLine();

            file.WriteLine("Least Popular: MIN(Players/Day) [Age > 30 days]");
            file.WriteLine("```");
            for (int i = 0, count = 0; count < CountPerSection; i++)
            {
                if (results[i].NumPlayers > 0 && results[i].Created < thirtyDaysAgo)
                {
                    file.WriteLine(String.Format("{0:F4} {1}", results[i].PlayersPerDay, results[i].GameName));
                    count++;
                }
            }
            file.WriteLine("```");
            file.WriteLine();

            file.WriteLine("Slowest to Master: MAX(MeanTimeToMaster) MasteryRate|MeanTimeToMaster|StdDev [Players Mastered >= 3]");
            file.WriteLine("```");
            results.Sort((l, r) => (int)((l.MeanTimeToMaster - r.MeanTimeToMaster) * 100000));
            for (int i = results.Count - 1, count = 0; count < CountPerSection; i--)
            {
                if (results[i].HardcoreMasteredUserCount >= 3)
                {
                    file.WriteLine(String.Format("{0,4:D}/{1,4:D} {2,8:F2} {3,8:F2} {4}",
                        results[i].HardcoreMasteredUserCount, results[i].NumPlayers,
                        results[i].MeanTimeToMaster, results[i].StdDevTimeToMaster,
                        results[i].GameName));
                    count++;
                }
            }
            file.WriteLine("```");
            file.WriteLine();

            file.WriteLine("Fastest to Master: MIN(MeanTimeToMaster) MasteryRate|MeanTimeToMaster|StdDev [Players Mastered >= 3, Points >= 50]");
            file.WriteLine("```");
            for (int i = 0, count = 0; count < CountPerSection; i++)
            {
                if (results[i].HardcoreMasteredUserCount >= 3 && results[i].Points >= 50 && results[i].MeanTimeToMaster > 0.0)
                {
                    file.WriteLine(String.Format("{0,4:D}/{1,4:D} {2,8:F2} {3,8:F2} {4}",
                        results[i].HardcoreMasteredUserCount, results[i].NumPlayers,
                        results[i].MeanTimeToMaster, results[i].StdDevTimeToMaster,
                        results[i].GameName));
                    count++;
                }
            }
            file.WriteLine("```");
            file.WriteLine();

            file.WriteLine("Fastest to Master: MIN(MeanTimeToMaster) MasteryRate|MeanTimeToMaster|StdDev [Players Mastered >= 3, Points >= 400]");
            file.WriteLine("```");
            for (int i = 0, count = 0; count < CountPerSection; i++)
            {
                if (results[i].HardcoreMasteredUserCount >= 3 && results[i].Points >= 400 && results[i].MeanTimeToMaster > 0.0)
                {
                    file.WriteLine(String.Format("{0,4:D}/{1,4:D} {2,8:F2} {3,8:F2} {4}",
                        results[i].HardcoreMasteredUserCount, results[i].NumPlayers,
                        results[i].MeanTimeToMaster, results[i].StdDevTimeToMaster,
                        results[i].GameName));
                    count++;
                }
            }
            file.WriteLine("```");
            file.WriteLine();

            file.WriteLine("Points requiring the least effort: MIN(MinutesPerPoint)|NintiethPercentilePoints|Players [>= 2 achievements earned by 90% of players, Players >= 3]");
            file.WriteLine("```");
            results.Sort((l, r) =>
            {
                if (l == null)
                    return -1;
                if (r == null)
                    return 1;

                return (int)((l.MinutesPerPoint - r.MinutesPerPoint) * 100000);
            });
            for (int i = 0, count = 0; count < CountPerSection; i++)
            {
                if (results[i].NintiethPercentileAchievements >= 3 && results[i].NumPlayers >= 3)
                {
                    file.WriteLine(String.Format("{0,6:F3} {1,4:D} {2,4:D} {3}", results[i].MinutesPerPoint, results[i].NintiethPercentilePoints, results[i].NumPlayers, results[i].GameName));
                    count++;
                }
            }
            file.WriteLine("```");
            file.WriteLine();

            file.WriteLine("Points requiring the most effort: MAX(MinutesPerPoint)|NintiethPercentilePoints|Players [>= 10 points earned by 90% of players, Players >= 3]");
            file.WriteLine("```");
            for (int i = results.Count - 1, count = 0; count < CountPerSection; i--)
            {
                if (results[i].NintiethPercentilePoints >= 10 && results[i].NumPlayers >= 3)
                {
                    file.WriteLine(String.Format("{0,6:F3} {1,4:D} {2,4:D} {3}", results[i].MinutesPerPoint, results[i].NintiethPercentilePoints, results[i].NumPlayers, results[i].GameName));
                    count++;
                }
            }
            file.WriteLine("```");
            file.WriteLine();

            file.WriteLine("Easiest sets: MAX(NintiethPercentilePoints/Points)|Players [Players >= 10, Points >= 50]");
            file.WriteLine("```");
            results.Sort((l, r) =>
            {
                if (l == null)
                    return -1;
                if (r == null)
                    return 1;
                if (l.Points == 0 || r.Points == 0)
                    return l.Points - r.Points;
                return ((l.NintiethPercentilePoints * 10000) / l.Points) - ((r.NintiethPercentilePoints * 10000) / r.Points);
            });
            for (int i = results.Count - 1, count = 0; count < CountPerSection; i--)
            {
                if (results[i].NumPlayers >= 10 && results[i].Points >= 50)
                {
                    file.WriteLine(String.Format("{0,4:D}/{1,4:D} {2,4:D} {3}",
                        results[i].NintiethPercentilePoints, results[i].Points, results[i].NumPlayers,
                        results[i].GameName));
                    count++;
                }
            }
            file.WriteLine("```");
            file.WriteLine();

            file.WriteLine("Easiest sets: MAX(NintiethPercentilePoints/Points)|Players [Players >= 10, Points >= 400]");
            file.WriteLine("```");
            for (int i = results.Count - 1, count = 0; count < CountPerSection; i--)
            {
                if (results[i].NumPlayers >= 10 && results[i].Points >= 400)
                {
                    file.WriteLine(String.Format("{0,4:D}/{1,4:D} {2,4:D} {3}",
                        results[i].NintiethPercentilePoints, results[i].Points, results[i].NumPlayers,
                        results[i].GameName));
                    count++;
                }
            }
            file.WriteLine("```");
            file.WriteLine();

            file.WriteLine("Hardest sets: MIN(TwentyFifthPercentilePoints/Points)|Players [Players >= 10, TwentyFifthPercentilePoints > 0]");
            file.WriteLine("```");
            results.Sort((l, r) =>
            {
                if (l == null)
                    return -1;
                if (r == null)
                    return 1;
                if (l.Points == 0 || r.Points == 0)
                    return l.Points - r.Points;
                return (l.TwentyFifthPercentilePoints * 10000) / l.Points - (r.TwentyFifthPercentilePoints * 10000) / r.Points;
            });
            for (int i = 0, count = 0; count < CountPerSection; i++)
            {
                if (results[i].NumPlayers >= 10 && results[i].TwentyFifthPercentilePoints > 0)
                {
                    file.WriteLine(String.Format("{0,4:D}/{1,4:D} {2,4:D} {3}",
                        results[i].TwentyFifthPercentilePoints, results[i].Points, results[i].NumPlayers,
                        results[i].GameName));
                    count++;
                }
            }
            file.WriteLine("```");
            file.WriteLine();

            file.WriteLine("Most Earned Achievements: MAX(Players)|Achievement (Game)");
            file.WriteLine("```");
            foreach (var achievement in _mostAwardedAchievements)
            {
                file.WriteLine(String.Format("{0,5:D} {1} ({2})",
                    achievement.EarnedBy, achievement.Title, achievement.Description));
            }
            file.WriteLine("```");
            file.WriteLine();
        }

        private void WritePossibleCheaters(StreamWriter file, List<CheaterInfo> cheaters)
        {
            Progress.Label = "Identifying potential cheaters...";
            Progress.Reset(cheaters.Count);

            file.WriteLine("Possible cheaters: TimeToMaster < 10% of median Time/Median/StdDev|LinkToComparePage [Masters >= 8, Points >= 50, TimeToMaster more than 90% from median or more than 2 stddevs from median]");
            file.WriteLine();

            cheaters.Sort((l, r) => String.Compare(l.UserName, r.UserName));

            foreach (var cheater in cheaters)
            {
                ++Progress.Current;

                foreach (var kvp in cheater.Results)
                {
                    var result = kvp.Key;
                    var userMastery = new UserMasteriesViewModel.Result
                    {
                        GameId = result.GameId,
                        GameName = result.GameName,
                        ConsoleName = result.ConsoleName,
                        NumMasters = result.HardcoreMasteredUserCount,
                        MasteryRank = kvp.Value.MasteryRank,
                        MasteryMinutes = (int)kvp.Value.GameTime.TotalMinutes,
                        TimeToMasterMean = (int)result.MeanTimeToMaster,
                        TimeToMasterStdDev = (int)result.StdDevTimeToMaster,
                        Unlocks = kvp.Value,
                    };

                    WritePotentialCheaterInformation(file, userMastery);
                }
            }
            file.WriteLine();
        }

        internal static void WritePotentialCheaterInformation(StreamWriter file, UserMasteriesViewModel.Result masteryInfo)
        {
            var user = masteryInfo.Unlocks.User;

            file.WriteLine("* {0} mastered {1} ({2}) ({3})", user, masteryInfo.GameName, masteryInfo.ConsoleName, masteryInfo.GameId);
            file.WriteLine("  https://retroachievements.org/gamecompare.php?ID={0}&f={1}", masteryInfo.GameId, user);

            bool dumpTimes = true;

            var notified = CheaterNotified(user, masteryInfo.GameId);
            if (!String.IsNullOrEmpty(notified))
                file.WriteLine("  - notified {0}", notified);

            if (IsUntracked(user))
            {
                file.WriteLine("  - currently Untracked");
                dumpTimes = false;
            }

            var performance = 1.0 - (masteryInfo.Unlocks.GameTime.TotalMinutes / masteryInfo.TimeToMasterMean);
            file.WriteLine("  Time to Master: {0:F2} ({1:F2}% faster than median {2:F2}, std dev={3:F2}) (rank:{4}/{5})",
                masteryInfo.Unlocks.GameTime.TotalMinutes, performance * 100,
                masteryInfo.TimeToMasterMean, masteryInfo.TimeToMasterStdDev,
                masteryInfo.MasteryRank, masteryInfo.NumMasters);

            if (dumpTimes)
            {
                var achievements = new List<AchievementTime>();

                foreach (var achievement in masteryInfo.Unlocks.Achievements)
                    achievements.Add(new AchievementTime { Id = achievement.Key, When = achievement.Value });
                achievements.Sort((l, r) => DateTime.Compare(l.When, r.When));

                // TODO: better way to get achievement names?
                var gameStats = new GameStatsViewModel() { GameId = masteryInfo.GameId };
                gameStats.LoadGame();

                foreach (var achievement in achievements)
                {
                    file.Write("  {0:D4}-{1:D2}-{2:D2} {3:D2}:{4:D2}:{5:D2} ", achievement.When.Year, achievement.When.Month, achievement.When.Day,
                        achievement.When.Hour, achievement.When.Minute, achievement.When.Second);

                    var achDef = gameStats.Achievements.FirstOrDefault(a => a.Id == achievement.Id);
                    if (achDef != null)
                        file.WriteLine("{0,6:D} {1}", achDef.Id, achDef.Title);
                    else
                        file.WriteLine("{0,6:D} ??????", achievement.Id);
                }
            }
            file.WriteLine();
        }

        private static string CheaterNotified(string user, int gameId)
        {
            const string Jan21 = "https://discord.com/channels/310192285306454017/704124849211179048/794741358463287306";

            switch (user)
            {
                case "AceOfSpaces":
                    // https://discord.com/channels/310192285306454017/564271682731245603/794617780376830042
                    if (gameId == 759)
                        return Jan21;
                    break;

                case "AngryPotato":
                    // https://discord.com/channels/310192285306454017/564271682731245603/794618358356115478
                    // admitted to using a speed hack: https://discord.com/channels/310192285306454017/399671428733206528/794858362478788628
                    // achievements were reset
                    // if (gameId == 2114)
                    //     return Jan21;
                    break;

                case "Augustine":
                    // https://discord.com/channels/310192285306454017/564271682731245603/794617927021756416
                    if (gameId == 510)
                        return Jan21;
                    break;

                case "fepnascimento":
                    // https://discord.com/channels/310192285306454017/564271682731245603/794618214688096268
                    // achievements were reset
                    // if (gameId == 2592)
                    //    return Jan21;
                    break;

                case "Francis64":
                    // https://discord.com/channels/310192285306454017/564271682731245603/794618244635951134
                    if (gameId == 510)
                        return Jan21;
                    break;

                case "joker1000":
                    // https://discord.com/channels/310192285306454017/388903437640794112/904712391303000076
                    if (gameId == 767 || gameId == 2592)
                        return "https://discord.com/channels/310192285306454017/388903437640794112/904712391303000076";
                    break;

                case "MegaLuis33":
                    // https://discord.com/channels/310192285306454017/564271682731245603/794618468212539412
                    if (gameId == 558)
                        return Jan21;
                    break;

                case "Otamegane":
                    // https://discord.com/channels/310192285306454017/564271682731245603/794617688764055612
                    if (gameId == 510 || gameId == 745)
                        return Jan21;
                    break;

                case "RetroAchiever":
                    // https://discord.com/channels/310192285306454017/564271682731245603/794617746897895424
                    if (gameId == 754 || gameId == 821)
                        return Jan21;
                    break;

                case "Rewsifer":
                    // https://discord.com/channels/310192285306454017/564271682731245603/794618559728320522
                    if (gameId == 558)
                        return Jan21;
                    break;

                case "twinphex":
                    // https://discord.com/channels/310192285306454017/564271682731245603/794618670474854440
                    if (gameId == 3362)
                        return Jan21;
                    break;

                case "YamiXIII":
                    // https://discord.com/channels/310192285306454017/564271682731245603/794618728020181002
                    // achievements were reset
                    // if (gameId == 3658)
                    //     return Jan21;
                    break;
            }

            return String.Empty;
        }

        private static bool IgnoreCheater(GameStatsViewModel.UserStats user, int gameId)
        {
            switch (user.User)
            {
                case "AgentRibinski":
                    // seems like a bad estimation caused by playing over many days
                    return (gameId == 669);

                case "Agrahnax":
                    // seems like a bad estimation caused by playing over many days
                    return (gameId == 676);

                case "amine456":
                    // televandalist seems to think the times seem reasonable
                    // https://discord.com/channels/310192285306454017/564271682731245603/794743956948647936
                    return (gameId == 535);

                case "Baobabastass":
                    // KickMeElmo indicates the game has bugs which allow endgame equipment early
                    // https://discord.com/channels/310192285306454017/564271682731245603/794680166306676766
                    return (gameId == 762);

                case "boxmeister":
                    // suspicious achievements were all added after the bulk of the achievements were unlocked.
                    // suspect he completed the game normally, then unlocked the new achievements with his existing save
                    return (gameId == 802);

                case "chro":
                    // two rapid sessions with a big gap in the middle
                    // https://discord.com/channels/310192285306454017/564271682731245603/794635369555034142
                    return (gameId == 788);

                case "joker1000":
                    // golden sun was broken
                    // https://discord.com/channels/310192285306454017/564271682731245603/826985751379050566
                    if (gameId == 2592)
                        return true;
                    break;

                case "Nevermond12":
                    // he's just that good
                    // https://discord.com/channels/310192285306454017/564271682731245603/826985306074120202
                    return (gameId == 10173);

                case "Riger":
                    // Salsa manually unlocked a bunch of stuff for Riger on 4/14/2018: https://discord.com/channels/310192285306454017/360584144281010178/434742993728307201
                    if (user.Achievements.First().Value.Year == 2018)
                        return true;
                    break;

                case "Valenstein":
                    // seems like a bad estimation caused by playing over many days
                    return (gameId == 782);

                case "VICTORKRATOS":
                    // likely offline/reconnect unlocks
                    // https://discord.com/channels/310192285306454017/564271682731245603/794620459374477362
                    return (gameId == 624 || gameId == 1485);

            }

            return false;
        }

        private static bool IsUntracked(string user)
        {
            var userJson = RAWebCache.Instance.GetUserRankAndScore(user);
            return userJson.GetField("Rank").IntegerValue == null;
        }

        private class CheaterInfo
        {
            public string UserName;
            public List<KeyValuePair<MasteryStats, GameStatsViewModel.UserStats>> Results;
        }

        private class CheatedGameInfo
        {
            public MasteryStats Game;
            public List<GameStatsViewModel.UserStats> Users;
        }

        private class AchievementTime
        {
            public DateTime When;
            public int Id;
        }
    }
}
