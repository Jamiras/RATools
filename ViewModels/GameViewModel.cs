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
        {
            GameId = gameId;
            Title = title;
            Script = new ScriptViewModel(this);
            Resources = new ResourceContainer();
            SelectedEditor = Script;
            Notes = new TinyDictionary<int, string>();
            GoToSourceCommand = new DelegateCommand<int>(GoToSource);

            _logger = ServiceRepository.Instance.FindService<ILogService>().GetLogger("RATools");
        }

        private void ParseNotes(IEnumerable<JsonObject> notes)
        {
            foreach (var note in notes)
            {
                var address = Int32.Parse(note.GetField("Address").StringValue.Substring(2), System.Globalization.NumberStyles.HexNumber);
                var text = note.GetField("Note").StringValue;
                if (text.Length > 0 && text != "''") // a long time ago notes were "deleted" by setting their text to ''
                    Notes[address] = text;
            }
        }

        public GameViewModel(int gameId, string title, string raCacheDirectory)
            : this(gameId, title)
        {
            RACacheDirectory = raCacheDirectory;

            var filename = Path.Combine(raCacheDirectory, gameId + "-Notes.json");
            if (!File.Exists(filename))
                filename = Path.Combine(raCacheDirectory, gameId + "-Notes2.txt");

            using (var notesStream = File.OpenRead(filename))
            {
                var reader = new StreamReader(notesStream);
                var firstChar = reader.Peek();
                notesStream.Seek(0, SeekOrigin.Begin);

                if (firstChar == '{')
                {
                    _isN64 = false;

                    // full JSON response
                    var notes = new JsonObject(notesStream);
                    ParseNotes(notes.GetField("CodeNotes").ObjectArrayValue);
                }
                else if (firstChar == '[')
                {
                    _isN64 = false;

                    // just notes subobject.
                    var notes = new JsonObject(notesStream);
                    ParseNotes(notes.GetField("items").ObjectArrayValue);
                }
                else
                {
                    _isN64 = true;

                    // N64 unique format
                    var tokenizer = Tokenizer.CreateTokenizer(notesStream);
                    do
                    {
                        var unused = tokenizer.ReadTo(':');
                        if (tokenizer.NextChar == '\0')
                            break;
                        tokenizer.Advance();

                        int address;
                        if (tokenizer.Match("0x"))
                            address = Int32.Parse(tokenizer.ReadTo(':').ToString(), System.Globalization.NumberStyles.HexNumber);
                        else
                            address = Int32.Parse(tokenizer.ReadTo(':').ToString());
                        tokenizer.Advance();

                        var text = tokenizer.ReadTo('#');
                        tokenizer.Advance();

                        Notes[address] = text.ToString();
                    } while (true);
                }
            }

            _logger.WriteVerbose("Read " + Notes.Count + " code notes");
        }

        public ScriptViewModel Script { get; private set; }

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

            if (!String.IsNullOrEmpty(RACacheDirectory))
            {
                if (_isN64)
                    MergePublishedN64(GameId, editors);
                else
                    MergePublished(GameId, editors);

                MergeLocal(GameId, editors);
            }

            int nextLocalId = 111000001;
            foreach (var achievement in editors.OfType<GeneratedAchievementViewModel>())
            {
                if (achievement.Local != null && achievement.Local.Id >= nextLocalId)
                    nextLocalId = achievement.Local.Id + 1;
            }

            foreach (var achievement in editors.OfType<GeneratedAchievementViewModel>())
            {
                if (achievement.Local != null && achievement.Local.Id == 0)
                    achievement.Local.Id = nextLocalId++;

                achievement.UpdateCommonProperties(this);
            }

            Editors = editors;
        }      

        private LocalAchievements _localAchievements;
        private readonly bool _isN64;
        private readonly ILogger _logger;

        internal int GameId { get; private set; }
        internal string RACacheDirectory { get; private set; }
        internal TinyDictionary<int, string> Notes { get; private set; }

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

            _localAchievements.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName, warning, validateAll ? null : achievement);
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

        private void MergePublished(int gameId, List<GeneratedItemViewModelBase> achievements)
        {
            var fileName = Path.Combine(RACacheDirectory, gameId + ".json");
            if (!File.Exists(fileName))
            {
                fileName = Path.Combine(RACacheDirectory, gameId + ".txt");
                if (!File.Exists(fileName))
                    return;
            }

            using (var stream = File.OpenRead(fileName))
            {
                var publishedData = new JsonObject(stream);
                Title = publishedData.GetField("Title").StringValue;

                var publishedAchievements = publishedData.GetField("Achievements");
                var coreCount = 0;
                var corePoints = 0;
                var unofficialCount = 0;
                var unofficialPoints = 0;
                foreach (var publishedAchievement in publishedAchievements.ObjectArrayValue)
                {
                    var points = publishedAchievement.GetField("Points").IntegerValue.GetValueOrDefault();
                    var title = publishedAchievement.GetField("Title").StringValue;

                    var id = publishedAchievement.GetField("ID").IntegerValue.GetValueOrDefault();
                    var achievement = achievements.OfType<GeneratedAchievementViewModel>().FirstOrDefault(a => a.Generated.Id == id);
                    if (achievement == null)
                    {
                        achievement = achievements.OfType<GeneratedAchievementViewModel>().FirstOrDefault(a => String.Compare(a.Generated.Title.Text, title, StringComparison.CurrentCultureIgnoreCase) == 0);
                        if (achievement == null)
                        {
                            achievement = new GeneratedAchievementViewModel(this, null);
                            achievements.Add(achievement);
                        }
                    }

                    var builder = new AchievementBuilder();
                    builder.Id = id;
                    builder.Title = title;
                    builder.Description = publishedAchievement.GetField("Description").StringValue;
                    builder.Points = publishedAchievement.GetField("Points").IntegerValue.GetValueOrDefault();
                    builder.BadgeName = publishedAchievement.GetField("BadgeName").StringValue;
                    builder.ParseRequirements(Tokenizer.CreateTokenizer(publishedAchievement.GetField("MemAddr").StringValue));

                    var builtAchievement = builder.ToAchievement();
                    builtAchievement.Published = UnixEpoch.AddSeconds(publishedAchievement.GetField("Created").IntegerValue.GetValueOrDefault());
                    builtAchievement.LastModified = UnixEpoch.AddSeconds(publishedAchievement.GetField("Modified").IntegerValue.GetValueOrDefault());

                    var flags = publishedAchievement.GetField("Flags").IntegerValue;
                    if (flags == 5)
                    {
                        achievement.Unofficial.LoadAchievement(builtAchievement);
                        unofficialCount++;
                        unofficialPoints += points;
                    }
                    else if (flags == 3)
                    {
                        achievement.Core.LoadAchievement(builtAchievement);
                        coreCount++;
                        corePoints += points;
                    }
                }

                CoreAchievementCount = coreCount;
                CoreAchievementPoints = corePoints;
                UnofficialAchievementCount = unofficialCount;
                UnofficialAchievementPoints = unofficialPoints;

                _logger.WriteVerbose(String.Format("Merged {0} core achievements ({1} points)", coreCount, corePoints));
                _logger.WriteVerbose(String.Format("Merged {0} unofficial achievements ({1} points)", unofficialCount, unofficialPoints));
            }
        }

        private void MergePublishedN64(int gameId, List<GeneratedItemViewModelBase> achievements)
        {
            var fileName = Path.Combine(RACacheDirectory, gameId + ".json");
            if (!File.Exists(fileName))
            {
                fileName = Path.Combine(RACacheDirectory, gameId + ".txt");
                if (!File.Exists(fileName))
                    return;
            }

            var count = 0;
            var points = 0;

            var officialAchievements = new LocalAchievements(fileName);
            foreach (var publishedAchievement in officialAchievements.Achievements)
            {
                var achievement = achievements.OfType<GeneratedAchievementViewModel>().FirstOrDefault(a => String.Compare(a.Generated.Title.Text, publishedAchievement.Title, StringComparison.CurrentCultureIgnoreCase) == 0);
                if (achievement == null)
                {
                    achievement = new GeneratedAchievementViewModel(this, null);
                    achievements.Add(achievement);
                }

                achievement.Core.LoadAchievement(publishedAchievement);
                count++;
                points += publishedAchievement.Points;
            }

            fileName = Path.Combine(RACacheDirectory, gameId + "-Unofficial.txt");
            if (File.Exists(fileName))
            {
                var unofficialAchievements = new LocalAchievements(fileName);
                foreach (var publishedAchievement in unofficialAchievements.Achievements)
                {
                    var achievement = achievements.OfType<GeneratedAchievementViewModel>().FirstOrDefault(a => String.Compare(a.Generated.Title.Text, publishedAchievement.Title, StringComparison.CurrentCultureIgnoreCase) == 0);
                    if (achievement == null)
                    {
                        achievement = new GeneratedAchievementViewModel(this, null);
                        achievements.Add(achievement);
                    }

                    achievement.Unofficial.LoadAchievement(publishedAchievement);
                    count++;
                    points += publishedAchievement.Points;
                }
            }

            CoreAchievementCount = count;
            CoreAchievementPoints = points;

            _logger.WriteVerbose(String.Format("Merged {0} published achievements ({1} points)", count, points));
        }

        private void MergeLocal(int gameId, List<GeneratedItemViewModelBase> achievements)
        {
            var fileName = Path.Combine(RACacheDirectory, gameId + "-User.txt");
            _localAchievements = new LocalAchievements(fileName);

            if (String.IsNullOrEmpty(_localAchievements.Title))
                _localAchievements.Title = Title;

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

                Theme.ColorChanged += Theme_ColorChanged;
            }

            private void Theme_ColorChanged(object sender, Theme.ColorChangedEventArgs e)
            {
                switch (e.Color)
                {
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

            public Brush DiffAddedBrush { get; private set; }
            public Brush DiffRemovedBrush { get; private set; }
        }

        public ResourceContainer Resources { get; private set; }
    }
}
