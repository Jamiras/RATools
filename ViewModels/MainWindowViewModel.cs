using System;
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
            DumpPublishedCommand = new DelegateCommand(DumpPublished);
            GameStatsCommand = new DelegateCommand(GameStats);
            OpenTicketsCommand = new DelegateCommand(OpenTickets);

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

        public CommandBase<string> OpenRecentCommand { get; private set; }
        private void OpenFile(string filename)
        {
            var parser = new AchievementScriptInterpreter();

            using (var stream = File.OpenRead(filename))
            {
                AddRecentFile(filename);

                if (parser.Run(Tokenizer.CreateTokenizer(stream)))
                {
                    CurrentFile = filename;

                    foreach (var directory in Tokenizer.Split(RACacheDirectory, ';'))
                    {
                        var notesFile = Path.Combine(directory.ToString(), parser.GameId + "-Notes2.txt");
                        if (File.Exists(notesFile))
                        {
                            Game = new GameViewModel(parser, directory.ToString());
                            return;
                        }
                    }

                    MessageBoxViewModel.ShowMessage("Could not locate notes file for game " + parser.GameId);
                    return;
                }
            }

            MessageBoxViewModel.ShowMessage(parser.ErrorMessage);
        }

        private void AddRecentFile(string newFile)
        {
            if (_recentFiles.First() == newFile)
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

        public CommandBase DumpPublishedCommand { get; private set; }
        private void DumpPublished()
        {
            if (Game == null)
            {
                MessageBoxViewModel.ShowMessage("No game loaded");
                return;
            }

            var vm = new FileDialogViewModel();
            vm.DialogTitle = "Select dump file";
            vm.Filters["Script file"] = "*.txt";
            vm.FileNames = new[] { Game.Title + ".txt" };

            if (vm.ShowSaveFileDialog() != DialogResult.Ok)
                return;

            using (var stream = File.CreateText(vm.FileNames[0]))
            {
                stream.Write("// ");
                stream.WriteLine(Game.Title);
                stream.Write("// #ID = ");
                stream.WriteLine(String.Format("{0}", Game.GameId));
                stream.WriteLine();

                foreach (var achievement in Game.Achievements)
                {
                    if (achievement.Achievement != null && achievement.Achievement.Id != 0)
                    {
                        stream.WriteLine("achievement(");

                        stream.Write("    title = \"");
                        stream.Write(achievement.Title.PublishedText);
                        stream.Write("\", description = \"");
                        stream.Write(achievement.Description.PublishedText);
                        stream.Write("\", points = ");
                        stream.Write(achievement.Points.PublishedText);
                        stream.WriteLine(",");

                        stream.Write("    id = ");
                        stream.Write(achievement.Achievement.Id);
                        stream.Write(", published = \"");
                        stream.Write(achievement.Achievement.Published);
                        stream.Write("\", modified = \"");
                        stream.Write(achievement.Achievement.LastModified);
                        stream.WriteLine("\",");

                        var groupEnumerator = achievement.RequirementGroups.GetEnumerator();
                        groupEnumerator.MoveNext();
                        stream.Write("    trigger = ");
                        DumpPublishedRequirements(stream, groupEnumerator.Current);
                        bool first = true;
                        while (groupEnumerator.MoveNext())
                        {
                            stream.WriteLine(first ? " && " : " || ");
                            first = false;

                            stream.Write("              (");
                            DumpPublishedRequirements(stream, groupEnumerator.Current);
                            stream.Write(")");
                        }
                        stream.WriteLine();

                        stream.WriteLine(")");
                        stream.WriteLine();
                    }
                }
            }
        }

        private void DumpPublishedRequirements(StreamWriter stream, RequirementGroupViewModel requirementGroupViewModel)
        {
            bool needsAmpersand = false;

            var requirementEnumerator = requirementGroupViewModel.Requirements.GetEnumerator();
            while (requirementEnumerator.MoveNext())
            {
                if (!String.IsNullOrEmpty(requirementEnumerator.Current.Definition.PublishedText))
                {
                    if (needsAmpersand)
                        stream.Write(" && ");
                    else
                        needsAmpersand = true;

                    stream.Write(requirementEnumerator.Current.Definition.PublishedText);
                }
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
    }
}
