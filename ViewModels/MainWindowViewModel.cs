using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
using RATools.Parser;
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

            //Editor = new EditorViewModel();
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

        public static readonly ModelProperty EditorProperty = ModelProperty.Register(typeof(MainWindowViewModel), "Editor", typeof(ViewModelBase), null);
        public ViewModelBase Editor
        {
            get { return (ViewModelBase)GetValue(EditorProperty); }
            private set { SetValue(EditorProperty, value); }
        }

        public static readonly ModelProperty CurrentFileProperty = ModelProperty.Register(typeof(MainWindowViewModel), "CurrentFile", typeof(string), null);
        public string CurrentFile
        {
            get { return (string)GetValue(CurrentFileProperty); }
            private set { SetValue(CurrentFileProperty, value); }
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
            OpenFile(CurrentFile);
        }

        public CommandBase<string> OpenRecentCommand { get; private set; }
        private void OpenFile(string filename)
        {
            if (!File.Exists(filename))
            {
                MessageBoxViewModel.ShowMessage("Could not open " + filename);
                return;
            }

            var editor = Editor as EditorViewModel;
            if (editor != null)
            {
                editor.Content = File.ReadAllText(filename);
                return;
            }

            var logger = ServiceRepository.Instance.FindService<ILogService>().GetLogger("RATools");
            logger.WriteVerbose("Opening " + filename);

            var parser = new AchievementScriptInterpreter();

            using (var stream = File.OpenRead(filename))
            {
                AddRecentFile(filename);

                if (parser.Run(Tokenizer.CreateTokenizer(stream)))
                {
                    logger.WriteVerbose("Game ID: " + parser.GameId);
                    logger.WriteVerbose("Generated " + parser.Achievements.Count() + " achievements");
                    if (!String.IsNullOrEmpty(parser.RichPresence))
                        logger.WriteVerbose("Generated Rich Presence");
                    if (parser.Leaderboards.Count() > 0)
                        logger.WriteVerbose("Generated " + parser.Leaderboards.Count() + " leaderboards");

                    CurrentFile = filename;

                    foreach (var directory in ServiceRepository.Instance.FindService<ISettings>().DataDirectories)
                    {
                        var notesFile = Path.Combine(directory, parser.GameId + "-Notes2.txt");
                        if (File.Exists(notesFile))
                        {
                            logger.WriteVerbose("Found code notes in " + directory);
                            Editor = new GameViewModel(parser, directory.ToString());
                            return;
                        }
                    }

                    logger.WriteVerbose("Could not find code notes");
                    MessageBoxViewModel.ShowMessage("Could not locate notes file for game " + parser.GameId);
                    return;
                }
                else if (parser.GameId != 0)
                {
                    logger.WriteVerbose("Game ID: " + parser.GameId);
                    CurrentFile = filename;

                    if (!String.IsNullOrEmpty(parser.GameTitle))
                        Editor = new GameViewModel(parser.GameId, parser.GameTitle);
                    else
                        Editor = null;
                }
            }

            logger.WriteVerbose("Parse error: " + parser.ErrorMessage);
            MessageBoxViewModel.ShowMessage(parser.ErrorMessage);
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
            var game = Editor as GameViewModel;
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
            var game = vm.Editor as GameViewModel;
            if (game != null)
            {
                foreach (var achievement in game.Achievements)
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
