using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.IO;
using Jamiras.Services;
using Jamiras.ViewModels;
using RATools.Data;
using RATools.Parser;
using System.Windows.Media;
using System;
using System.Windows.Media.Imaging;
using Jamiras.IO.Serialization;

namespace RATools.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            ExitCommand = new DelegateCommand(Exit);
            CompileAchievementsCommand = new DelegateCommand(CompileAchievements);
            UpdateLocalCommand = new DelegateCommand<Achievement>(UpdateLocal);
        }

        public bool Initialize()
        {
            var file = new IniFile("RATools.ini");
            try
            {
                var values = file.Read();
                RACacheDirectory = values["RACacheDirectory"];
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        private string RACacheDirectory;

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
            {
                using (var stream = File.OpenRead(vm.FileNames[0]))
                {
                    var parser = new AchievementScriptInterpreter();
                    if (!parser.Run(Tokenizer.CreateTokenizer(stream), RACacheDirectory))
                    {
                        MessageBoxViewModel.ShowMessage(parser.ErrorMessage);
                    }
                    else
                    {
                        GameTitle = parser.GameTitle;
                        PublishedAchievementCount = parser.PublishedAchievementCount;
                        PublishedAchievementPoints = parser.PublishedAchievementPoints;

                        var achievements = new List<Achievement>(parser.Achievements);

                        _notes = new TinyDictionary<int, string>();
                        using (var notesStream = File.OpenRead(Path.Combine(RACacheDirectory, parser.GameId + "-Notes2.txt")))
                        {
                            var notes = new JsonObject(notesStream);
                            foreach (var note in notes.GetField("CodeNotes").ObjectArrayValue)
                            {
                                var address = Int32.Parse(note.GetField("Address").StringValue.Substring(2), System.Globalization.NumberStyles.HexNumber);
                                var text = note.GetField("Note").StringValue;
                                _notes[address] = text;
                            }
                        }

                        _richPresence = parser.RichPresence;
                        var richPresence = new Achievement { Title = "Rich Presence" };
                        _richFile = Path.Combine(RACacheDirectory, parser.GameId + "-Rich.txt");
                        if (File.Exists(_richFile))
                        {
                            var richLocal = File.ReadAllText(_richFile);
                            if (String.IsNullOrEmpty(_richPresence))
                                _richPresence = richLocal;
                            else if (_richPresence != richLocal)
                                richPresence.IsDifferentThanLocal = true;
                        }

                        if (!String.IsNullOrEmpty(_richPresence) && _richPresence.Length > 8)
                            achievements.Add(richPresence);

                        Achievements = achievements;
                        _localAchievements = parser.LocalAchievements;
                        LocalAchievementCount = Achievements.Count();
                        LocalAchievementPoints = Achievements.Sum(a => a.Points);
                    }
                }
            }
        }

        private LocalAchievements _localAchievements;
        private TinyDictionary<int, string> _notes;
        private string _richFile, _richPresence;

        public static readonly ModelProperty AchievementsProperty = ModelProperty.Register(typeof(MainWindowViewModel), "Achievements", typeof(IEnumerable<Achievement>), null);
        public IEnumerable<Achievement> Achievements
        {
            get { return (IEnumerable<Achievement>)GetValue(AchievementsProperty); }
            private set { SetValue(AchievementsProperty, value); }
        }

        public static readonly ModelProperty SelectedAchievementProperty = ModelProperty.Register(typeof(MainWindowViewModel), "SelectedAchievement", typeof(Achievement), null, OnSelectedAchievementChanged);
        public Achievement SelectedAchievement
        {
            get { return (Achievement)GetValue(SelectedAchievementProperty); }
            set { SetValue(SelectedAchievementProperty, value); }
        }

        private static void OnSelectedAchievementChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            var vm = (MainWindowViewModel)sender;
            vm.OnPropertyChanged(() => vm.SelectedAchievementRequirementGroups);
        }

        public static readonly ModelProperty SelectedAchievementBadgeProperty = ModelProperty.RegisterDependant(typeof(MainWindowViewModel), "SelectedAchievementBadge", typeof(ImageSource), new[] { SelectedAchievementProperty }, GetBadge);
        public ImageSource SelectedAchievementBadge
        {
            get { return (ImageSource)GetValue(SelectedAchievementBadgeProperty); }
        }

        private static ImageSource GetBadge(ModelBase model)
        {
            var vm = (MainWindowViewModel)model;
            var achievement = vm.SelectedAchievement;
            if (achievement != null && !String.IsNullOrEmpty(achievement.BadgeName))
            {
                var path = Path.Combine(Path.Combine(vm.RACacheDirectory, "../Badge"), achievement.BadgeName + ".png");
                if (File.Exists(path))
                    return new BitmapImage(new Uri(path));
            }

            return null;
        }

        public CommandBase<Achievement> UpdateLocalCommand { get; private set; }
        private void UpdateLocal(Achievement achievement)
        {
            if (achievement.Title == "Rich Presence")
            {
                File.WriteAllText(_richFile, _richPresence);
            }
            else
            {
                var list = (List<Achievement>)_localAchievements.Achievements;
                int index = 0;
                while (index < list.Count && list[index].Title != achievement.Title)
                    index++;

                if (index == list.Count)
                    list.Add(achievement);
                else
                    list[index] = achievement;

                _localAchievements.Commit();
            }

            achievement.IsDifferentThanLocal = false;
        }

        public static readonly ModelProperty GameTitleProperty = ModelProperty.Register(typeof(MainWindowViewModel), "GameTitle", typeof(string), "No Game Loaded");
        public string GameTitle
        {
            get { return (string)GetValue(GameTitleProperty); }
            private set { SetValue(GameTitleProperty, value); }
        }

        public static readonly ModelProperty PublishedAchievementCountProperty = ModelProperty.Register(typeof(MainWindowViewModel), "PublishedAchievementCount", typeof(int), 0);
        public int PublishedAchievementCount
        {
            get { return (int)GetValue(PublishedAchievementCountProperty); }
            private set { SetValue(PublishedAchievementCountProperty, value); }
        }

        public static readonly ModelProperty PublishedAchievementPointsProperty = ModelProperty.Register(typeof(MainWindowViewModel), "PublishedAchievementPoints", typeof(int), 0);
        public int PublishedAchievementPoints
        {
            get { return (int)GetValue(PublishedAchievementPointsProperty); }
            private set { SetValue(PublishedAchievementPointsProperty, value); }
        }

        public static readonly ModelProperty LocalAchievementCountProperty = ModelProperty.Register(typeof(MainWindowViewModel), "LocalAchievementCount", typeof(int), 0);
        public int LocalAchievementCount
        {
            get { return (int)GetValue(LocalAchievementCountProperty); }
            private set { SetValue(LocalAchievementCountProperty, value); }
        }

        public static readonly ModelProperty LocalAchievementPointsProperty = ModelProperty.Register(typeof(MainWindowViewModel), "LocalAchievementPoints", typeof(int), 0);
        public int LocalAchievementPoints
        {
            get { return (int)GetValue(LocalAchievementPointsProperty); }
            private set { SetValue(LocalAchievementPointsProperty, value); }
        }

        public IEnumerable<RequirementGroupViewModel> SelectedAchievementRequirementGroups
        {
            get
            {
                if (SelectedAchievement != null)
                {
                    if (SelectedAchievement.Title == "Rich Presence")
                    {
                        var group = new RequirementGroupViewModel("Rich Presence", new Requirement[0], _notes);
                        ((IList<RequirementViewModel>)group.Requirements).Add(new RequirementViewModel(_richPresence, String.Empty));
                        yield return group;
                    }
                    else
                    {
                        yield return new RequirementGroupViewModel("Core", SelectedAchievement.CoreRequirements, _notes);
                        int i = 0;
                        foreach (var alt in SelectedAchievement.AlternateRequirements)
                        {
                            i++;
                            yield return new RequirementGroupViewModel("Alt " + i, alt, _notes);
                        }
                    }
                }
            }
        }
    }
}
