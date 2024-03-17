using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
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
            NewScriptCommand = new DelegateCommand(NewScript);
            OpenScriptCommand = new DelegateCommand(OpenFile);
            SaveScriptCommand = DisabledCommand.Instance;
            SaveScriptAsCommand = DisabledCommand.Instance;
            RefreshScriptCommand = DisabledCommand.Instance;
            OpenRecentCommand = new DelegateCommand<string>(OpenFile);
            SettingsCommand = new DelegateCommand(OpenSettings);
            ExitCommand = new DelegateCommand(Exit);

            DragDropScriptCommand = new DelegateCommand<string[]>(DragDropFile, CanDragDropFile);
            UpdateLocalCommand = DisabledCommand.Instance;
            ViewAchievementsCommand = DisabledCommand.Instance;

            GameStatsCommand = new DelegateCommand(GameStats);
            OpenTicketsCommand = new DelegateCommand(OpenTickets);
            ConditionsAnalyzerCommand = new DelegateCommand(ConditionsAnalyzer);
            MasteryCommand = new DelegateCommand(MasteryStats);
            UserMasteriesCommand = new DelegateCommand(UserMasteries);
            UnlockDistanceCommand = new DelegateCommand(UnlockDistance);

            AboutCommand = new DelegateCommand(About);

            _recentFiles = new RecencyBuffer<string>(8);
        }

        public bool Initialize()
        {
            var settings = new Settings();
            ServiceRepository.Instance.RegisterInstance<ISettings>(settings);
            ShowHexValues = settings.HexValues;
            Theme.Deserialize(settings.Colors);

            HasDumpDirectory = !String.IsNullOrEmpty(settings.DumpDirectory) &&
                !String.IsNullOrEmpty(settings.DoRequestToken);

            var persistance = ServiceRepository.Instance.FindService<IPersistantDataRepository>();
            var recent = persistance.GetValue("RecentFiles");
            if (recent != null)
            {
                var list = new List<string>(recent.Split(';'));
                for (int i = list.Count - 1; i >= 0; i--) // add in reverse order so most recent is added last
                    _recentFiles.Add(list[i]);
                RecentFiles = list.ToArray();
            }

            var basePath = AppDomain.CurrentDomain.BaseDirectory;

            var logService = ServiceRepository.Instance.FindService<ILogService>();
            FileLogger logger = null;
            for (int i = 1; i < 10; i++)
            {
                var fileName = Path.Combine(basePath, (i == 1) ? "RATools.log" : "RATools" + i + ".log");
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

        private readonly RecencyBuffer<string> _recentFiles;

        public bool HasDumpDirectory { get; private set; }

        public CommandBase ExitCommand { get; private set; }

        private void Exit()
        {
            ServiceRepository.Instance.FindService<IDialogService>().MainWindow.Close();
        }

        public CommandBase OpenScriptCommand { get; private set; }
        private void OpenFile()
        {
            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Select achievements script";
            vm.Filters["Script file"] = "*.rascript;*.txt";
            vm.CheckFileExists = true;

            if (vm.ShowOpenFileDialog() == DialogResult.Ok)
                OpenFile(vm.FileNames[0]);
        }

        public CommandBase<string[]> DragDropScriptCommand { get; private set; }
        private bool CanDragDropFile(string[] files)
        {
            if (files.Length != 1)
                return false;

            var ext = Path.GetExtension(files[0]);
            return (String.Compare(ext, ".rascript", StringComparison.OrdinalIgnoreCase) == 0) ||
                (String.Compare(ext, ".txt", StringComparison.OrdinalIgnoreCase) == 0);
        }
        private void DragDropFile(string[] files)
        {
            OpenFile(files[0]);
        }

        public CommandBase SaveScriptCommand { get; private set; }
        public CommandBase SaveScriptAsCommand { get; private set; }

        public static readonly ModelProperty GameProperty = ModelProperty.Register(typeof(MainWindowViewModel), "Game", typeof(GameViewModel), null, OnGameChanged);
        public GameViewModel Game
        {
            get { return (GameViewModel)GetValue(GameProperty); }
            private set { SetValue(GameProperty, value); }
        }

        private static void OnGameChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            if (e.OldValue == null)
            {
                var vm = (MainWindowViewModel)sender;
                vm.SaveScriptCommand = new DelegateCommand(() => vm.SaveScript());
                vm.SaveScriptAsCommand = new DelegateCommand(() => vm.SaveScriptAs());
                vm.RefreshScriptCommand = new DelegateCommand(vm.RefreshScript);
                vm.UpdateLocalCommand = new DelegateCommand(vm.UpdateLocal);
                vm.ViewAchievementsCommand = new DelegateCommand(vm.ViewAchievements);
                vm.OnPropertyChanged(() => vm.SaveScriptCommand);
                vm.OnPropertyChanged(() => vm.SaveScriptAsCommand);
                vm.OnPropertyChanged(() => vm.RefreshScriptCommand);
                vm.OnPropertyChanged(() => vm.UpdateLocalCommand);
                vm.OnPropertyChanged(() => vm.ViewAchievementsCommand);
            }
        }

        public static readonly ModelProperty RecentFilesProperty = ModelProperty.Register(typeof(MainWindowViewModel), "RecentFiles", typeof(IEnumerable<string>), null);
        public IEnumerable<string> RecentFiles
        {
            get { return (IEnumerable<string>)GetValue(RecentFilesProperty); }
            private set { SetValue(RecentFilesProperty, value); }
        }

        public CommandBase RefreshScriptCommand { get; private set; }
        private void RefreshScript()
        {
            if (Game != null)
            {
                Game.Script.DeleteBackup();
                OpenFile(Game.Script.Filename);
            }
        }

        public bool CloseEditor()
        {
            if (Game != null)
                Game.Script.OnBeforeClose();

            if (Game == null || Game.Script.CompareState != GeneratedCompareState.LocalDiffers)
                return true;

            switch (TaskDialogViewModel.ShowWarningPrompt("Save changes to " + Game.Script.Title + "?", "", TaskDialogViewModel.Buttons.YesNoCancel))
            {
                case DialogResult.Yes:
                    return SaveScript();

                case DialogResult.No:
                    Game.Script.DeleteBackup();
                    return true;

                default:
                case DialogResult.Cancel:
                    return false;
            }
        }

        public CommandBase<string> OpenRecentCommand { get; private set; }
        private void OpenFile(string filename)
        {
            if (!File.Exists(filename))
            {
                TaskDialogViewModel.ShowErrorMessage("Could not open " + Path.GetFileName(filename), filename + " was not found");
                return;
            }

            int line = 1;
            int column = 1;
            string selectedEditor = null;
            if (Game != null && Game.Script.Filename == filename)
            {
                if (Game.Script.CompareState == GeneratedCompareState.LocalDiffers)
                {
                    if (TaskDialogViewModel.ShowWarningPrompt("Revert to the last saved state?", "Your changes will be lost.") == DialogResult.No)
                        return;
                }

                // capture current location so we can restore it after refreshing
                line = Game.Script.Editor.CursorLine;
                column = Game.Script.Editor.CursorColumn;
                if (Game.SelectedEditor != null)
                    selectedEditor = Game.SelectedEditor.Title;
            }
            else if (!CloseEditor())
            {
                return;
            }

            var backupFilename = ScriptViewModel.GetBackupFilename(filename);
            bool usingBackup = false;
            if (File.Exists(backupFilename))
            {
                switch (TaskDialogViewModel.ShowWarningPrompt("Open autosave file?", 
                    "An autosave file from " + File.GetLastWriteTime(backupFilename) + " was found for " + Path.GetFileName(filename) + ".", 
                    TaskDialogViewModel.Buttons.YesNoCancel))
                {
                    case DialogResult.Cancel:
                        return;

                    case DialogResult.Yes:
                        filename = backupFilename;
                        usingBackup = true;
                        break;

                    default:
                        break;
                }
            }

            var logger = ServiceRepository.Instance.FindService<ILogService>().GetLogger("RATools");
            logger.WriteVerbose("Opening " + filename);

            string content;
            try
            {
                content = File.ReadAllText(filename);
            }
            catch (IOException ex)
            {
                TaskDialogViewModel.ShowErrorMessage("Unable to read " + Path.GetFileName(filename), ex.Message);
                return;
            }

            int gameId = 0;
            string gameTitle = null;

            var tokenizer = Tokenizer.CreateTokenizer(content);
            tokenizer.SkipWhitespace();
            while (tokenizer.Match("//"))
            {
                var comment = tokenizer.ReadTo('\n');
                if (comment.Contains("#ID"))
                {
                    var tokens = comment.Split('=');
                    if (tokens.Length > 1)
                        Int32.TryParse(tokens[1].ToString(), out gameId);

                    break;
                }
                else if (gameTitle == null)
                {
                    gameTitle = comment.Trim().ToString();
                }

                tokenizer.SkipWhitespace();
            }

            if (gameId == 0)
            {
                logger.WriteVerbose("Could not find game ID");
                TaskDialogViewModel.ShowWarningMessage("Could not find game ID", "The loaded file did not contain an #ID comment indicating which game the script is associated to.");
            }

            if (gameTitle == null)
                gameTitle = Path.GetFileNameWithoutExtension(filename);

            if (!usingBackup)
                AddRecentFile(filename);

            GameViewModel viewModel = null;

            if (gameId != 0)
            {
                logger.WriteVerbose("Game ID: " + gameId);

                foreach (var directory in ServiceRepository.Instance.FindService<ISettings>().EmulatorDirectories)
                {
                    var dataDirectory = Path.Combine(directory, "RACache", "Data");

                    var notesFile = Path.Combine(dataDirectory, gameId + "-Notes.json");
                    if (!File.Exists(notesFile))
                        notesFile = Path.Combine(dataDirectory, gameId + "-Notes2.txt");

                    if (File.Exists(notesFile))
                    {
                        logger.WriteVerbose("Found code notes in " + dataDirectory);
                        viewModel = new GameViewModel(gameId, gameTitle);
                        viewModel.AssociateRACacheDirectory(dataDirectory);
                        break;
                    }
                }

                if (viewModel == null)
                {
                    logger.WriteVerbose("Could not find code notes");
                    TaskDialogViewModel.ShowWarningMessage("Could not locate code notes for game " + gameId,
                        "The game does not appear to have been recently loaded in any of the emulators specified in the Settings dialog.");

                    viewModel = new GameViewModel(gameId, gameTitle);
                }
            }
            else
            {
                viewModel = new GameViewModel(gameId, gameTitle);
            }


            var existingViewModel = Game;

            // if we're just refreshing the current game script, only update the script content,
            // which will be reprocessed and update the editor list. If it's not the same script,
            // or notes have changed, use the new view model.
            if (existingViewModel != null && existingViewModel.GameId == viewModel.GameId &&
                existingViewModel.Script.Filename == filename &&
                existingViewModel.Notes.Count == viewModel.Notes.Count)
            {
                // refresh any data from the emulator (code notes, published/local achievement definitions)
                if (!String.IsNullOrEmpty(viewModel.RACacheDirectory))
                    existingViewModel.AssociateRACacheDirectory(viewModel.RACacheDirectory);

                existingViewModel.Script.SetContent(content);
                viewModel = existingViewModel;

                existingViewModel.SelectedEditor = existingViewModel.Editors.FirstOrDefault(e => e.Title == selectedEditor);
                existingViewModel.Script.Editor.MoveCursorTo(line, column, Jamiras.ViewModels.CodeEditor.CodeEditorViewModel.MoveCursorFlags.None);
            }
            else
            {
                if (usingBackup)
                {
                    viewModel.Script.Filename = Path.GetFileName(filename);
                    var title = viewModel.Title + " (from backup)";
                    viewModel.SetValue(GameViewModel.TitleProperty, title);
                }
                else
                {
                    viewModel.Script.Filename = filename;
                }

                viewModel.Script.SetContent(content);
                Game = viewModel;
            }

            if (viewModel.Script.Editor.ErrorsToolWindow.References.Count > 0)
                viewModel.Script.Editor.ErrorsToolWindow.IsVisible = true;
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

        private bool SaveScript()
        {
            // if there isn't a path, the user need to select a save location
            if (!Game.Script.Filename.Contains("\\"))
                return SaveScriptAs();

            Game.Script.Save();

            if (Game.Title.EndsWith(" (from backup)"))
                Game.SetValue(GameViewModel.TitleProperty, Game.Title.Substring(0, Game.Title.Length - 14));

            AddRecentFile(Game.Script.Filename);
            return true;
        }

        private bool SaveScriptAs()
        {
            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Save achievements script";
            vm.Filters["Script file"] = "*.rascript;*.txt";
            vm.FileNames = new[] { Game.Script.Filename };
            vm.OverwritePrompt = true;

            if (vm.ShowSaveFileDialog() == DialogResult.Ok)
            {
                Game.Script.Filename = vm.FileNames[0];
                SaveScript();
            }

            return false;
        }

        public CommandBase NewScriptCommand { get; private set; }
        private void NewScript()
        {
            if (!CloseEditor())
                return;

            var dialog = new NewScriptDialogViewModel();
            if (dialog.ShowDialog() == DialogResult.Ok)
                Game = dialog.Finalize();
        }

        public CommandBase SettingsCommand { get; private set; }
        private void OpenSettings()
        {
            var vm = new OptionsDialogViewModel();
            if (vm.ShowDialog() == DialogResult.Ok)
                vm.ApplyChanges();
        }

        public CommandBase UpdateLocalCommand { get; private set; }
        private void UpdateLocal()
        {
            var game = Game;
            if (game == null)
            {
                TaskDialogViewModel.ShowErrorMessage("No game loaded", "Local data cannot be written without an associated game.");
                return;
            }

            if (game.Script.Editor.ErrorsToolWindow.References.Count > 0)
            {
                game.Script.Editor.ErrorsToolWindow.IsVisible = true;
                TaskDialogViewModel.ShowErrorMessage("Errors exist in script", "Local data cannot be updated until errors are resolved.");
                return;
            }

            if (String.IsNullOrEmpty(game.RACacheDirectory))
            {
                TaskDialogViewModel.ShowErrorMessage("Could not identify emulator directory.", "Local data cannot be updated if the emulator directory for the game is not known.");
                return;
            }

            var dialog = new UpdateLocalViewModel(game);
            dialog.ShowDialog();
        }

        public CommandBase ViewAchievementsCommand { get; private set; }
        private void ViewAchievements()
        {
            var game = Game;
            if (game == null)
            {
                TaskDialogViewModel.ShowErrorMessage("No game loaded", "Cannot view achievements if game not loaded.");
                return;
            }

            var dialog = new ViewAchievementsViewModel(game);
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
                foreach (var achievement in game.Editors.OfType<AssetViewModelBase>())
                    achievement.OnShowHexValuesChanged(e);
            }
        }

        private bool ValidateForApi()
        {
            var settings = ServiceRepository.Instance.FindService<ISettings>();
            if (String.IsNullOrEmpty(settings.ApiKey))
            {
                MessageBoxViewModel.ShowMessage("This feature requires the Web Api Key value to be set.");
                return false;
            }

            if (String.IsNullOrEmpty(settings.UserName) || settings.UserName == "RATools")
            {
                MessageBoxViewModel.ShowMessage("This feature requires a User Name value to be set.");
                return false;
            }

            return true;
        }

        public CommandBase GameStatsCommand { get; private set; }
        private void GameStats()
        {
            if (!ValidateForApi())
                return;

            var vm = new GameStatsViewModel();
            vm.ShowDialog();
        }

        public CommandBase OpenTicketsCommand { get; private set; }
        private void OpenTickets()
        {
            if (!ValidateForApi())
                return;

            var vm = new OpenTicketsViewModel();
            vm.ShowDialog();
        }

        public CommandBase ConditionsAnalyzerCommand { get; private set; }
        private void ConditionsAnalyzer()
        {
            var vm = new ConditionsAnalyzerViewModel();
            vm.ShowDialog();
        }

        public CommandBase MasteryCommand { get; private set; }
        private void MasteryStats()
        {
            if (!ValidateForApi())
                return;

            var vm = new MasteryViewModel();
            vm.ShowDialog();
        }

        public CommandBase UserMasteriesCommand { get; private set; }
        private void UserMasteries()
        {
            if (!ValidateForApi())
                return;

            var vm = new UserMasteriesViewModel();
            vm.ShowDialog();
        }

        public CommandBase UnlockDistanceCommand { get; private set; }
        private void UnlockDistance()
        {
            if (!ValidateForApi())
                return;

            var vm = new UnlockDistanceViewModel();
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
