using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.IO;
using Jamiras.Services;
using Jamiras.ViewModels;
using RATools.Parser;

namespace RATools.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            ExitCommand = new DelegateCommand(Exit);
            CompileAchievementsCommand = new DelegateCommand(CompileAchievements);
            OpenRecentCommand = new DelegateCommand<string>(OpenFile);

            _recentFiles = new RecencyBuffer<string>(8);
        }

        public bool Initialize()
        {
            var file = new IniFile("RATools.ini");
            try
            {
                var values = file.Read();
                RACacheDirectory = values["RACacheDirectory"];
            }
            catch (FileNotFoundException)
            {
                return false;
            }

            var persistance = ServiceRepository.Instance.FindService<IPersistantDataRepository>();
            var recent = persistance.GetValue("RecentFiles");
            if (recent != null)
            {
                var list = new List<string>(recent.Split(';'));
                list.Reverse();
                foreach (var item in list)
                    _recentFiles.Add(item);
                RecentFiles = list.ToArray();
            }

            return true;
        }

        private string RACacheDirectory;
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
            vm.Filters["Script file"] = "*.txt";
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

        public CommandBase<string> OpenRecentCommand { get; private set; }
        private void OpenFile(string filename)
        {
            using (var stream = File.OpenRead(filename))
            {
                AddRecentFile(filename);

                var parser = new AchievementScriptInterpreter();
                if (!parser.Run(Tokenizer.CreateTokenizer(stream)))
                {
                    MessageBoxViewModel.ShowMessage(parser.ErrorMessage);
                }
                else
                {
                    Game = new GameViewModel(parser, RACacheDirectory);
                }
            }
        }

        private void AddRecentFile(string newFile)
        {
            if (_recentFiles.First() == newFile)
                return;
            
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
    }
}
