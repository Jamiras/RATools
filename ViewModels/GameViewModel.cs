using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.IO.Serialization;
using Jamiras.Services;
using Jamiras.ViewModels;
using RATools.Data;
using RATools.Parser;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Title}")]
    public class GameViewModel : ViewModelBase
    {
        public GameViewModel(int gameId, string title)
            : this(gameId, title,
                  ServiceRepository.Instance.FindService<ILogService>().GetLogger("RATools"),
                  ServiceRepository.Instance.FindService<IFileSystemService>())
        {
            Resources = new ResourceContainer();

            Script = new ScriptViewModel(this);
            SelectedEditor = Script;
        }

        protected GameViewModel(int gameId, string title,
            ILogger logger, IFileSystemService fileSystemService)
        {
            /* unit tests call this constructor directly and will provide their own Script object and don't need Resources */
            GameId = gameId;
            Title = title;
            Notes = new TinyDictionary<int, string>();
            GoToSourceCommand = new DelegateCommand<int>(GoToSource);

            _publishedAchievements = new List<Achievement>();

            _logger = logger;
            _fileSystemService = fileSystemService;
        }

        private readonly ILogger _logger;
        protected readonly IFileSystemService _fileSystemService;
        protected readonly List<Achievement> _publishedAchievements;
        protected LocalAchievements _localAchievements;

        internal int GameId { get; private set; }
        internal string RACacheDirectory { get; private set; }
        internal TinyDictionary<int, string> Notes { get; private set; }

        public ScriptViewModel Script { get; protected set; }

        public CommandBase<int> GoToSourceCommand { get; private set; }
        private void GoToSource(int line)
        {
            SelectedEditor = Script;
            Script.Editor.GotoLine(line);
            Script.Editor.IsFocusRequested = true;
        }

        internal void PopulateEditorList(AchievementScriptInterpreter interpreter)
        { 
            var editors = new List<GeneratedItemViewModelBase>();
            if (Script != null)
                editors.Add(Script);

            if (interpreter != null)
            {
                GeneratedAchievementCount = interpreter.Achievements.Count();
                editors.Capacity += GeneratedAchievementCount;

                if (!String.IsNullOrEmpty(interpreter.RichPresence))
                {
                    var richPresenceViewModel = new RichPresenceViewModel(this, interpreter.RichPresence);
                    if (richPresenceViewModel.Lines.Any())
                    {
                        richPresenceViewModel.SourceLine = interpreter.RichPresenceLine;
                        editors.Add(richPresenceViewModel);
                    }
                }

                foreach (var achievement in interpreter.Achievements)
                {
                    var achievementViewModel = new GeneratedAchievementViewModel(this, achievement);
                    editors.Add(achievementViewModel);
                }

                foreach (var leaderboard in interpreter.Leaderboards)
                    editors.Add(new LeaderboardViewModel(this, leaderboard));
            }
            else
            {
                GeneratedAchievementCount = 0;
            }

            if (_publishedAchievements.Count > 0)
                MergePublished(editors);

            if (_localAchievements != null)
                MergeLocal(editors);

            UpdateTemporaryIds(editors);

            Editors = editors;
        }

        private void UpdateTemporaryIds(List<GeneratedItemViewModelBase> editors)
        {
            // find the maximum temporary id already assigned
            int nextLocalId = 111000001;
            foreach (var achievement in editors.OfType<GeneratedAchievementViewModel>())
            {
                if (achievement.Local != null && achievement.Local.Id >= nextLocalId)
                    nextLocalId = achievement.Local.Id + 1;
                else if (achievement.Generated != null && achievement.Generated.Id >= nextLocalId)
                    nextLocalId = achievement.Generated.Id + 1;
            }

            foreach (var achievement in editors.OfType<GeneratedAchievementViewModel>())
            {
                // don't attempt to assign a temporary ID to a published achievement
                if (achievement.Published == null || achievement.Published.Achievement == null)
                {
                    if (achievement.Local != null && achievement.Local.Achievement != null)
                    {
                        // if it's in the local file, generate a temporary ID if one was not previously generated
                        if (achievement.Local.Achievement.Id == 0)
                        {
                            achievement.Local.Achievement.Id = nextLocalId++;
                            achievement.Local.LoadAchievement(achievement.Local.Achievement); // refresh the viewmodel's ID property
                        }
                    }
                    else if (achievement.Generated != null && achievement.Generated.Achievement != null)
                    {
                        // if it's not in the local file, generate a temporary ID if one was not provided by the code
                        if (achievement.Generated.Achievement.Id == 0)
                        {
                            achievement.Generated.Achievement.Id = nextLocalId++;
                            achievement.Generated.LoadAchievement(achievement.Generated.Achievement); // refresh the viewmodel's ID property
                        }
                    }
                }

                achievement.UpdateCommonProperties(this);
            }
        }

        public int CompileProgress { get; internal set; }
        public int CompileProgressLine { get; internal set; }

        internal void UpdateCompileProgress(int progress, int line)
        {
            if (CompileProgress != progress)
            {
                CompileProgress = progress;
                OnPropertyChanged(() => CompileProgress);
            }

            if (CompileProgressLine != line)
            {
                CompileProgressLine = line;
                OnPropertyChanged(() => CompileProgressLine);
            }
        }

        public static readonly ModelProperty EditorsProperty = ModelProperty.Register(typeof(GameViewModel), "Editors", typeof(IEnumerable<GeneratedItemViewModelBase>), new GeneratedItemViewModelBase[0]);
        public IEnumerable<GeneratedItemViewModelBase> Editors
        {
            get { return (IEnumerable<GeneratedItemViewModelBase>)GetValue(EditorsProperty); }
            private set { SetValue(EditorsProperty, value); }
        }

        public static readonly ModelProperty SelectedEditorProperty = ModelProperty.Register(typeof(GameViewModel), "SelectedEditor", typeof(GeneratedItemViewModelBase), null);
        public GeneratedItemViewModelBase SelectedEditor
        {
            get { return (GeneratedItemViewModelBase)GetValue(SelectedEditorProperty); }
            set { SetValue(SelectedEditorProperty, value); }
        }

        internal void UpdateLocal(Achievement achievement, Achievement localAchievement, StringBuilder warning, bool validateAll)
        {
            if (achievement == null)
            {
                _logger.WriteVerbose(String.Format("Deleting {0} from local achievements", localAchievement.Title));

                var previous = _localAchievements.Replace(localAchievement, null);
                if (previous != null && previous.Points != 0)
                    LocalAchievementPoints -= previous.Points;

                LocalAchievementCount--;
            }
            else
            {
                if (localAchievement != null)
                    _logger.WriteVerbose(String.Format("Updating {0} in local achievements", achievement.Title));
                else
                    _logger.WriteVerbose(String.Format("Committing {0} to local achievements", achievement.Title));

                var previous = _localAchievements.Replace(localAchievement, achievement);
                if (previous != null)
                {
                    var diff = achievement.Points - previous.Points;
                    if (diff != 0)
                        LocalAchievementPoints += diff;
                }
                else
                {
                    LocalAchievementCount++;
                    LocalAchievementPoints += achievement.Points;
                }
            }

            if (_localAchievementCommitSuspendCount == 0)
                _localAchievements.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName, warning, validateAll ? null : achievement);
        }

        private int _localAchievementCommitSuspendCount = 0;
        internal void SuspendCommitLocalAchievements()
        {
            ++_localAchievementCommitSuspendCount;
        }

        internal void ResumeCommitLocalAchievements()
        {
            if (_localAchievementCommitSuspendCount > 0 && --_localAchievementCommitSuspendCount == 0)
            {
                var warning = new StringBuilder();
                _localAchievements.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName, warning, null);
            }
        }


        public static readonly ModelProperty TitleProperty = ModelProperty.Register(typeof(MainWindowViewModel), "Title", typeof(string), String.Empty);
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            private set { SetValue(TitleProperty, value); }
        }

        public static readonly ModelProperty GeneratedAchievementCountProperty = ModelProperty.Register(typeof(MainWindowViewModel), "GeneratedAchievementCount", typeof(int), 0);
        public int GeneratedAchievementCount
        {
            get { return (int)GetValue(GeneratedAchievementCountProperty); }
            private set { SetValue(GeneratedAchievementCountProperty, value); }
        }

        public static readonly ModelProperty CoreAchievementCountProperty = ModelProperty.Register(typeof(MainWindowViewModel), "CoreAchievementCount", typeof(int), 0);
        public int CoreAchievementCount
        {
            get { return (int)GetValue(CoreAchievementCountProperty); }
            private set { SetValue(CoreAchievementCountProperty, value); }
        }

        public static readonly ModelProperty CoreAchievementPointsProperty = ModelProperty.Register(typeof(MainWindowViewModel), "CoreAchievementPoints", typeof(int), 0);
        public int CoreAchievementPoints
        {
            get { return (int)GetValue(CoreAchievementPointsProperty); }
            private set { SetValue(CoreAchievementPointsProperty, value); }
        }

        public static readonly ModelProperty UnofficialAchievementCountProperty = ModelProperty.Register(typeof(MainWindowViewModel), "UnofficialAchievementCount", typeof(int), 0);
        public int UnofficialAchievementCount
        {
            get { return (int)GetValue(UnofficialAchievementCountProperty); }
            private set { SetValue(UnofficialAchievementCountProperty, value); }
        }

        public static readonly ModelProperty UnofficialAchievementPointsProperty = ModelProperty.Register(typeof(MainWindowViewModel), "UnofficialAchievementPoints", typeof(int), 0);
        public int UnofficialAchievementPoints
        {
            get { return (int)GetValue(UnofficialAchievementPointsProperty); }
            private set { SetValue(UnofficialAchievementPointsProperty, value); }
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

        private static DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public void AssociateRACacheDirectory(string raCacheDirectory)
        {
            RACacheDirectory = raCacheDirectory;

            ReadCodeNotes();
            ReadPublished();

            var fileName = Path.Combine(RACacheDirectory, GameId + "-User.txt");
            _localAchievements = new LocalAchievements(fileName, _fileSystemService);

            if (String.IsNullOrEmpty(_localAchievements.Title))
                _localAchievements.Title = Title;
        }

        private void ReadCodeNotes()
        {
            var filename = Path.Combine(RACacheDirectory, GameId + "-Notes.json");
            using (var notesStream = _fileSystemService.OpenFile(filename, OpenFileMode.Read))
            {
                if (notesStream != null)
                {
                    var notes = new JsonObject(notesStream).GetField("items");
                    if (notes.Type == JsonFieldType.ObjectArray)
                    {
                        foreach (var note in notes.ObjectArrayValue)
                        {
                            var address = Int32.Parse(note.GetField("Address").StringValue.Substring(2), System.Globalization.NumberStyles.HexNumber);
                            var text = note.GetField("Note").StringValue;
                            if (text.Length > 0 && text != "''") // a long time ago notes were "deleted" by setting their text to ''
                                Notes[address] = text;
                        }
                    }
                }
            }

            _logger.WriteVerbose("Read " + Notes.Count + " code notes");
        }

        private void ReadPublished()
        {
            var fileName = Path.Combine(RACacheDirectory, GameId + ".json");
            using (var stream = _fileSystemService.OpenFile(fileName, OpenFileMode.Read))
            {
                if (stream == null)
                    return;

                var publishedData = new JsonObject(stream);
                Title = publishedData.GetField("Title").StringValue;

                var publishedAchievements = publishedData.GetField("Achievements");
                var coreCount = 0;
                var corePoints = 0;
                var unofficialCount = 0;
                var unofficialPoints = 0;
                foreach (var publishedAchievement in publishedAchievements.ObjectArrayValue)
                {
                    var builder = new AchievementBuilder();
                    builder.Id = publishedAchievement.GetField("ID").IntegerValue.GetValueOrDefault();
                    builder.Title = publishedAchievement.GetField("Title").StringValue;
                    builder.Description = publishedAchievement.GetField("Description").StringValue;
                    builder.Points = publishedAchievement.GetField("Points").IntegerValue.GetValueOrDefault();
                    builder.BadgeName = publishedAchievement.GetField("BadgeName").StringValue;
                    builder.ParseRequirements(Tokenizer.CreateTokenizer(publishedAchievement.GetField("MemAddr").StringValue));

                    var builtAchievement = builder.ToAchievement();
                    builtAchievement.Published = UnixEpoch.AddSeconds(publishedAchievement.GetField("Created").IntegerValue.GetValueOrDefault());
                    builtAchievement.LastModified = UnixEpoch.AddSeconds(publishedAchievement.GetField("Modified").IntegerValue.GetValueOrDefault());

                    builtAchievement.Category = publishedAchievement.GetField("Flags").IntegerValue.GetValueOrDefault();
                    if (builtAchievement.Category == 5)
                    {
                        _publishedAchievements.Add(builtAchievement);
                        unofficialCount++;
                        unofficialPoints += builtAchievement.Points;
                    }
                    else if (builtAchievement.Category == 3)
                    {
                        _publishedAchievements.Add(builtAchievement);
                        coreCount++;
                        corePoints += builtAchievement.Points;
                    }
                }

                CoreAchievementCount = coreCount;
                CoreAchievementPoints = corePoints;
                UnofficialAchievementCount = unofficialCount;
                UnofficialAchievementPoints = unofficialPoints;

                _logger.WriteVerbose(String.Format("Identified {0} core achievements ({1} points)", coreCount, corePoints));
                _logger.WriteVerbose(String.Format("Identified {0} unofficial achievements ({1} points)", unofficialCount, unofficialPoints));
            }
        }

        private void MergePublished(List<GeneratedItemViewModelBase> achievements)
        {
            foreach (var publishedAchievement in _publishedAchievements)
            {
                var achievement = achievements.OfType<GeneratedAchievementViewModel>().FirstOrDefault(a => a.Generated.Id == publishedAchievement.Id);
                if (achievement == null)
                {
                    achievement = achievements.OfType<GeneratedAchievementViewModel>().FirstOrDefault(a => String.Compare(a.Generated.Title.Text, publishedAchievement.Title, StringComparison.CurrentCultureIgnoreCase) == 0);
                    if (achievement == null)
                    {
                        achievement = new GeneratedAchievementViewModel(this, null);
                        achievements.Add(achievement);
                    }
                }

                achievement.Published.LoadAchievement(publishedAchievement);
            }
        }

        private void MergeLocal(List<GeneratedItemViewModelBase> achievements)
        {
            var localAchievements = new List<Achievement>(_localAchievements.Achievements);

            foreach (var achievement in achievements.OfType<GeneratedAchievementViewModel>())
            {
                Achievement localAchievement = null;
                if (achievement.Id > 0)
                    localAchievement = localAchievements.FirstOrDefault(a => a.Id == achievement.Id);

                if (localAchievement == null)
                {
                    localAchievement = localAchievements.FirstOrDefault(a => String.Compare(a.Title, achievement.Generated.Title.Text, StringComparison.CurrentCultureIgnoreCase) == 0);
                    if (localAchievement == null)
                    {
                        if (!String.IsNullOrEmpty(achievement.Generated.Description.Text))
                            localAchievement = localAchievements.FirstOrDefault(a => a.Description == achievement.Generated.Description.Text);

                        if (localAchievement == null)
                        {
                            // TODO: attempt to match achievements by requirements                        
                            continue;
                        }
                    }
                }

                localAchievements.Remove(localAchievement);

                achievement.Local.LoadAchievement(localAchievement);
            }

            foreach (var localAchievement in localAchievements)
            {
                var vm = new GeneratedAchievementViewModel(this, null);
                vm.Local.LoadAchievement(localAchievement);
                achievements.Add(vm);
            }

            LocalAchievementCount = _localAchievements.Achievements.Count();
            LocalAchievementPoints = _localAchievements.Achievements.Sum(a => a.Points);

            _logger.WriteVerbose(String.Format("Merged {0} local achievements ({1} points)", LocalAchievementCount, LocalAchievementPoints));
        }

        public class ResourceContainer : PropertyChangedObject
        {
            public ResourceContainer()
            {
                DiffAddedBrush = CreateBrush(Theme.Color.DiffAdded);
                DiffRemovedBrush = CreateBrush(Theme.Color.DiffRemoved);
                ScrollBarBackgroundBrush = CreateBrush(Theme.Color.ScrollBarBackground);
                ScrollBarForegroundBrush = CreateBrush(Theme.Color.ScrollBarForeground);

                Theme.ColorChanged += Theme_ColorChanged;
            }

            private void Theme_ColorChanged(object sender, Theme.ColorChangedEventArgs e)
            {
                switch (e.Color)
                {
                    case Theme.Color.ScrollBarBackground:
                        ScrollBarBackgroundBrush = CreateBrush(Theme.Color.ScrollBarBackground);
                        OnPropertyChanged(() => ScrollBarBackgroundBrush);
                        break;

                    case Theme.Color.ScrollBarForeground:
                        ScrollBarForegroundBrush = CreateBrush(Theme.Color.ScrollBarForeground);
                        OnPropertyChanged(() => ScrollBarForegroundBrush);
                        break;

                    case Theme.Color.DiffAdded:
                        DiffAddedBrush = CreateBrush(Theme.Color.DiffAdded);
                        OnPropertyChanged(() => DiffAddedBrush);
                        break;

                    case Theme.Color.DiffRemoved:
                        DiffRemovedBrush = CreateBrush(Theme.Color.DiffRemoved);
                        OnPropertyChanged(() => DiffRemovedBrush);
                        break;
                }
            }

            private static Brush CreateBrush(Theme.Color color)
            {
                var brush = new SolidColorBrush(Theme.GetColor(color));
                brush.Freeze();
                return brush;
            }

            public Brush ScrollBarBackgroundBrush { get; private set; }
            public Brush ScrollBarForegroundBrush { get; private set; }
            public Brush DiffAddedBrush { get; private set; }
            public Brush DiffRemovedBrush { get; private set; }
        }

        public ResourceContainer Resources { get; private set; }
    }
}
