using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
using RATools.Parser.Internal;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RATools.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            ExitCommand = new DelegateCommand(Exit);
            CompileAchievementsCommand = new DelegateCommand(CompileAchievements);
            RefreshCurrentCommand = new DelegateCommand(RefreshCurrent);
            OpenRecentCommand = new DelegateCommand<string>(OpenFile);
            NewScriptCommand = new DelegateCommand(NewScript);
            UpdateLocalCommand = new DelegateCommand(UpdateLocal);
            GameStatsCommand = new DelegateCommand(GameStats);
            OpenTicketsCommand = new DelegateCommand(OpenTickets);
            AboutCommand = new DelegateCommand(About);

            _recentFiles = new RecencyBuffer<string>(8);
        }

        public bool Initialize()
        {
            var settings = new Settings();
            ServiceRepository.Instance.RegisterInstance<ISettings>(settings);
            ShowHexValues = settings.HexValues;

            var persistance = ServiceRepository.Instance.FindService<IPersistantDataRepository>();
            var recent = persistance.GetValue("RecentFiles");
            if (recent != null)
            {
                var list = new List<string>(recent.Split(';'));
                foreach (var item in list)
                    _recentFiles.Add(item);
                RecentFiles = list.ToArray();
            }

            var logService = ServiceRepository.Instance.FindService<ILogService>();
            FileLogger logger = null;
            for (int i = 1; i < 10; i++)
            {
                var fileName = (i == 1) ? "RATools.log" : "RATools" + i + ".log";
                try
                {
                    logger = new FileLogger(fileName);
                }
                catch (IOException)
                {
                    // assume "file in use" and create a new one
                    continue;
                }

                logService.Loggers.Add(logger);
                break;
            }
            logService.IsTimestampLogged = true;

            if (logger != null)
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                if (version.EndsWith(".0"))
                    version = version.Substring(0, version.Length - 2);
                logger.Write("RATools v" + version);
            }

            return true;
        }

        private RecencyBuffer<string> _recentFiles;

        public CommandBase ExitCommand { get; private set; }

        private void Exit()
        {
            ServiceRepository.Instance.FindService<IDialogService>().MainWindow.Close();
        }

        public CommandBase CompileAchievementsCommand { get; private set; }

        private void CompileAchievements()
        {
            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Select achievements script";
            vm.Filters["Script file"] = "*.rascript;*.txt";
            vm.CheckFileExists = true;

            if (vm.ShowOpenFileDialog() == DialogResult.Ok)
                OpenFile(vm.FileNames[0]);
        }

        public static readonly ModelProperty GameProperty = ModelProperty.Register(typeof(MainWindowViewModel), "Game", typeof(GameViewModel), null);
        public GameViewModel Game
        {
            get { return (GameViewModel)GetValue(GameProperty); }
            private set { SetValue(GameProperty, value); }
        }

        public static readonly ModelProperty RecentFilesProperty = ModelProperty.Register(typeof(MainWindowViewModel), "RecentFiles", typeof(IEnumerable<string>), null);
        public IEnumerable<string> RecentFiles
        {
            get { return (IEnumerable<string>)GetValue(RecentFilesProperty); }
            private set { SetValue(RecentFilesProperty, value); }
        }

        public CommandBase RefreshCurrentCommand { get; private set; }
        private void RefreshCurrent()
        {
            if (Game != null)
                OpenFile(Game.Script.Filename);
        }

        public CommandBase<string> OpenRecentCommand { get; private set; }
        private void OpenFile(string filename)
        {
            if (!File.Exists(filename))
            {
                MessageBoxViewModel.ShowMessage("Could not open " + filename);
                return;
            }

            var logger = ServiceRepository.Instance.FindService<ILogService>().GetLogger("RATools");
            logger.WriteVerbose("Opening " + filename);

            string content = File.ReadAllText(filename);
            var tokenizer = Tokenizer.CreateTokenizer(content);
            var expressionGroup = new AchievementScriptParser().Parse(tokenizer);

            int gameId = 0;
            var idComment = expressionGroup.Comments.FirstOrDefault(c => c.Value.Contains("#ID"));
            if (idComment != null)
            {
                var tokens = idComment.Value.Split('=');
                if (tokens.Length > 1)
                    Int32.TryParse(tokens[1].ToString(), out gameId);
            }

            if (gameId == 0)
            {
                logger.WriteVerbose("Could not find game ID");
                MessageBoxViewModel.ShowMessage("Could not find game id");
                return;
            }

            AddRecentFile(filename);
            logger.WriteVerbose("Game ID: " + gameId);

            var gameTitle = expressionGroup.Comments[0].Value.Substring(2).Trim();
            GameViewModel viewModel = null;

            foreach (var directory in ServiceRepository.Instance.FindService<ISettings>().DataDirectories)
            {
                var notesFile = Path.Combine(directory, gameId + "-Notes2.txt");
                if (File.Exists(notesFile))
                {
                    logger.WriteVerbose("Found code notes in " + directory);

                    viewModel = new GameViewModel(gameId, gameTitle, directory.ToString());
                }
            }

            if (viewModel == null)
            {
                logger.WriteVerbose("Could not find code notes");
                MessageBoxViewModel.ShowMessage("Could not locate notes file for game " + gameId);

                viewModel = new GameViewModel(gameId, gameTitle);
            }

            viewModel.Script.Content = content;
            Game = viewModel;
        }

        private void AddRecentFile(string newFile)
        {
            if (_recentFiles.FirstOrDefault() == newFile)
                return;

            if (_recentFiles.FindAndMakeRecent(str => str == newFile) == null)
                _recentFiles.Add(newFile);

            var builder = new StringBuilder();
            foreach (var file in _recentFiles)
            {
                builder.Append(file);
                builder.Append(';');
            }
            builder.Length--;

            var persistance = ServiceRepository.Instance.FindService<IPersistantDataRepository>();
            persistance.SetValue("RecentFiles", builder.ToString());

            RecentFiles = _recentFiles.ToArray();
        }

        public CommandBase NewScriptCommand { get; private set; }
        private void NewScript()
        {
            var dialog = new NewScriptDialogViewModel();
            dialog.ShowDialog();
        }

        public CommandBase UpdateLocalCommand { get; private set; }
        private void UpdateLocal()
        {
            var game = Game;
            if (game == null)
            {
                MessageBoxViewModel.ShowMessage("No game loaded");
                return;
            }

            var dialog = new UpdateLocalViewModel(game);
            dialog.ShowDialog();
        }

        public static readonly ModelProperty ShowHexValuesProperty = ModelProperty.Register(typeof(MainWindowViewModel), "ShowHexValues", typeof(bool), false, OnShowHexValuesChanged);
        public bool ShowHexValues
        {
            get { return (bool)GetValue(ShowHexValuesProperty); }
            set { SetValue(ShowHexValuesProperty, value); }
        }

        private static void OnShowHexValuesChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            ServiceRepository.Instance.FindService<ISettings>().HexValues = (bool)e.NewValue;
            var vm = (MainWindowViewModel)sender;
            var game = vm.Game;
            if (game != null)
            {
                foreach (var achievement in game.Editors)
                    achievement.OnShowHexValuesChanged(e);
            }
        }

        public CommandBase GameStatsCommand { get; private set; }
        private void GameStats()
        {
            var vm = new GameStatsViewModel();
            vm.ShowDialog();
        }

        public CommandBase OpenTicketsCommand { get; private set; }
        private void OpenTickets()
        {
            var vm = new OpenTicketsViewModel();
            vm.ShowDialog();
        }

        public CommandBase AboutCommand { get; private set; }
        private void About()
        {
            var vm = new AboutDialogViewModel();
            vm.ShowDialog();
        }
    }
}
