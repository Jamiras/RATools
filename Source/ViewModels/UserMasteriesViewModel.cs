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
    public class UserMasteriesViewModel : DialogViewModelBase
    {
        public UserMasteriesViewModel()
            : this(ServiceRepository.Instance.FindService<IBackgroundWorkerService>(),
                   ServiceRepository.Instance.FindService<IFileSystemService>(),
                   ServiceRepository.Instance.FindService<ISettings>())
        {
        }

        public UserMasteriesViewModel(IBackgroundWorkerService backgroundWorkerService, IFileSystemService fileSystem, ISettings settings)
        {
            _backgroundWorkerService = backgroundWorkerService;
            _fileSystem = fileSystem;
            _settings = settings;

            Progress = new ProgressFieldViewModel { Label = String.Empty };
            DialogTitle = "User Mastery Analyzer";
            CanClose = true;

            Results = new ObservableCollection<Result>();

            SearchCommand = new DelegateCommand(Search);
            ExportCommand = new DelegateCommand(Export);

            OpenGameStatsCommand = new DelegateCommand<Result>(OpenGameStats);

            AddPropertyChangedHandler(DialogResultProperty, OnDialogResultPropertyChanged);
        }

        private readonly IBackgroundWorkerService _backgroundWorkerService;
        private readonly IFileSystemService _fileSystem;
        private readonly ISettings _settings;

        public ProgressFieldViewModel Progress { get; private set; }

        public static readonly ModelProperty UserNameProperty = ModelProperty.Register(typeof(UserMasteriesViewModel), "UserName", typeof(string), "");

        public string UserName
        {
            get { return (string)GetValue(UserNameProperty); }
            set { SetValue(UserNameProperty, value); }
        }

        private void OnDialogResultPropertyChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            // stop any ongoing operations
            Progress.IsEnabled = false;
        }

        public class Result
        {
            public int GameId { get; set; }
            public string GameName { get; set; }
            public string ConsoleName { get; set; }
            public int NumAchievements { get; set; }
            public int NumMasters { get; set; }
            public int MasteryRank { get; set; }
            public int MasteryMinutes { get; set; }
            public int TimeToMasterMean { get; set; }
            public int TimeToMasterStdDev { get; set; }

            internal GameStatsViewModel.UserStats Unlocks;
        }

        public ObservableCollection<Result> Results { get; private set; }

        public CommandBase<Result> OpenGameStatsCommand { get; private set; }
        private void OpenGameStats(Result result)
        {
            var gameStats = new GameStatsViewModel(_backgroundWorkerService, _fileSystem, _settings);
            gameStats.GameId = result.GameId;
            gameStats.LoadGame();
            gameStats.ShowDialog();
        }

        public CommandBase SearchCommand { get; private set; }
        private void Search()
        {
            if (String.IsNullOrEmpty(UserName))
            {
                MessageBoxViewModel.ShowMessage("No username provided");
                return;
            }

            Results.Clear();

            Progress.Label = "Fetching User " + UserName;
            Progress.IsEnabled = true;
            _backgroundWorkerService.RunAsync(() =>
            {
                LoadUser();
            });
        }

        private void LoadUser()
        {
            var results = new List<Result>();
            var totalAchievements = 0;

            var masteriesJson = RAWebCache.Instance.GetUserMasteriesJson(UserName);
            foreach (var gameInfo in masteriesJson.GetField("items").ObjectArrayValue)
            {
                if (gameInfo.GetField("HardcoreMode").IntegerValue.GetValueOrDefault() != 1)
                    continue;

                var result = new Result
                {
                    GameId = gameInfo.GetField("GameID").IntegerValue.GetValueOrDefault(),
                    GameName = gameInfo.GetField("Title").StringValue,
                    ConsoleName = gameInfo.GetField("ConsoleName").StringValue,
                    NumAchievements = gameInfo.GetField("MaxPossible").IntegerValue.GetValueOrDefault(),
                };

                var numAwarded = gameInfo.GetField("NumAwarded").IntegerValue.GetValueOrDefault();
                if (numAwarded != result.NumAchievements)
                    continue;

                totalAchievements += result.NumAchievements;
                results.Add(result);
            }

            // sort by game ID for progress sanity
            results.Sort((Result l, Result r) =>
            {
                return l.GameId - r.GameId;
            });

            Progress.Reset(totalAchievements + 1);
            Progress.Current = 1;
            foreach (var result in results)
            {
                int progressStart = Progress.Current;

                Progress.Label = "Fetching game " + result.GameId;
                var gameStats = new GameStatsViewModel(_backgroundWorkerService, _fileSystem, _settings);
                gameStats.GameId = result.GameId;

                gameStats.Progress.PropertyChanged += (o, e) =>
                {
                    if (e.PropertyName == "Current")
                        Progress.Current = progressStart + gameStats.Progress.Current;
                };

                gameStats.LoadGame(true);

                if (!Progress.IsEnabled) // handle dialog closed
                    return;

                var times = new List<double>();
                foreach (var user in gameStats.TopUsers)
                {
                    if (user.PointsEarned == gameStats.TotalPoints && user.IsEstimateReliable)
                    {
                        result.NumMasters++;

                        if (String.Compare(user.User, UserName, StringComparison.InvariantCultureIgnoreCase) == 0)
                        {
                            result.MasteryRank = result.NumMasters;
                            result.MasteryMinutes = (int)user.GameTime.TotalMinutes;
                            result.Unlocks = user;
                        }

                        times.Add(user.GameTime.TotalMinutes);
                    }
                }

                if (result.MasteryRank == 0)
                {
                    // not found!?
                    var user = new GameStatsViewModel.UserStats { User = UserName };
                    var userList = new List<GameStatsViewModel.UserStats>();
                    userList.Add(user);
                    gameStats.RefreshUsers(userList);

                    user.UpdateGameTime(id =>
                    {
                        var achievement = gameStats.Achievements.FirstOrDefault(a => a.Id == id);
                        return (achievement != null) ? achievement.Points : 0;
                    }, null);

                    if (user.PointsEarned == gameStats.TotalPoints)
                    {
                        result.MasteryRank = 1;
                        foreach (var otherUser in gameStats.TopUsers)
                        {
                            if (otherUser.GameTime > user.GameTime)
                                break;

                            result.MasteryRank++;
                        }

                        result.MasteryMinutes = (int)user.GameTime.TotalMinutes;
                        times.Add(user.GameTime.TotalMinutes);
                        result.NumMasters++;
                    }
                }

                result.TimeToMasterMean = (int)times.Average();
                result.TimeToMasterStdDev = (int)StandardDeviation.Calculate(times);

                // if the user mastered the set more than 90% faster than median, or more than two standard deviations
                // faster than median, flag it as possibly cheated (by leaving the Unlocks field set).
                var threshold = Math.Max(result.TimeToMasterMean / 10, result.TimeToMasterMean - result.TimeToMasterStdDev * 2);
                if (result.MasteryMinutes >= threshold)
                    result.Unlocks = null;

                Progress.Current = progressStart + result.NumAchievements;
            }

            // sort by user's mastery rank asc, number of users desc.
            results.Sort((Result l, Result r) =>
            {
                if (l.MasteryRank != r.MasteryRank)
                    return (l.MasteryRank - r.MasteryRank);
                return (r.NumMasters - l.NumMasters);
            });

            _backgroundWorkerService.InvokeOnUiThread(() =>
            {
                foreach (var result in results)
                    Results.Add(result);
            });

            Progress.Label = String.Empty;
        }

        public CommandBase ExportCommand { get; private set; }
        private void Export()
        {
            if (Results.Count == 0)
            {
                MessageBoxViewModel.ShowMessage("No results to export");
                return;
            }

            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Export results";
            vm.Filters["TXT file"] = "*.txt";
            vm.FileNames = new[] { UserName + ".txt" };
            vm.OverwritePrompt = true;

            if (vm.ShowSaveFileDialog() == DialogResult.Ok)
            {
                using (var file = File.CreateText(vm.FileNames[0]))
                {
                    file.Write("Details for ");
                    file.WriteLine(UserName);
                    file.WriteLine("  rank  |   mastery   | achs gameid:name");
                    file.WriteLine(" ------ | ----------- | -----------------------------------------------------------------------");

                    bool hasUnlocks = false;
                    foreach (var result in Results)
                    {
                        file.WriteLine(String.Format("{0,3}/{1,3} | {2,4}m/{3,4}m | {4,3}x {5,6}:{6}",
                            result.MasteryRank, result.NumMasters, result.MasteryMinutes, result.TimeToMasterMean,
                            result.NumAchievements, result.GameId, result.GameName));

                        hasUnlocks |= (result.Unlocks != null);
                    }

                    if (hasUnlocks)
                    {
                        file.WriteLine();
                        file.WriteLine();

                        foreach (var result in Results)
                        {
                            if (result.Unlocks != null)
                                MasteryViewModel.WritePotentialCheaterInformation(file, result);
                        }
                    }
                }
            }
        }
    }
}
