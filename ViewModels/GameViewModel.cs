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
        protected LocalAssets _localAssets;

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

            var selectedEditor = (SelectedEditor != null) ? SelectedEditor.Title : null;

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
                    var achievementViewModel = new AchievementViewModel(this, achievement);
                    editors.Add(achievementViewModel);
                }

                foreach (var leaderboard in interpreter.Leaderboards)
                {
                    var leaderboardViewModel = new LeaderboardViewModel(this, leaderboard);
                    editors.Add(leaderboardViewModel);
                }
            }
            else
            {
                GeneratedAchievementCount = 0;
            }

            if (_publishedAchievements.Count > 0)
                MergePublished(editors);

            if (_localAssets != null)
                MergeLocal(editors);

            UpdateTemporaryIds(editors);

            SelectedEditor = editors.FirstOrDefault(e => e.Title == selectedEditor);
            Editors = editors;
        }

        private void UpdateTemporaryIds(List<GeneratedItemViewModelBase> editors)
        {
            // find the maximum temporary id already assigned
            int nextLocalId = 111000001;
            foreach (var assetViewModel in editors.OfType<AssetViewModelBase>())
            {
                var id = assetViewModel.Local.Id;
                if (id == 0)
                    id = assetViewModel.Generated.Id;
                if (id >= nextLocalId)
                    nextLocalId = id + 1;
            }

            foreach (var assetViewModel in editors.OfType<AssetViewModelBase>())
            {
                if (assetViewModel.AllocateLocalId(nextLocalId))
                    nextLocalId++;

                assetViewModel.Refresh();
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

                var previous = _localAssets.Replace(localAchievement, null);
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

                var previous = _localAssets.Replace(localAchievement, achievement);
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
                _localAssets.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName, warning, validateAll ? null : achievement);
        }

        internal void UpdateLocal(Leaderboard leaderboard, Leaderboard localLeaderboard, StringBuilder warning, bool validateAll)
        {
            if (leaderboard == null)
            {
                _logger.WriteVerbose(String.Format("Deleting {0} from local achievements", localLeaderboard.Title));
                _localAssets.Replace(localLeaderboard, null);
            }
            else
            {
                if (localLeaderboard != null)
                    _logger.WriteVerbose(String.Format("Updating {0} in local achievements", leaderboard.Title));
                else
                    _logger.WriteVerbose(String.Format("Committing {0} to local achievements", leaderboard.Title));

                _localAssets.Replace(localLeaderboard, leaderboard);
            }

            if (_localAchievementCommitSuspendCount == 0)
                _localAssets.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName, warning, validateAll ? null : leaderboard);
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
                _localAssets.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName, warning, null);
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
            _localAssets = new LocalAssets(fileName, _fileSystemService);

            if (String.IsNullOrEmpty(_localAssets.Title))
                _localAssets.Title = Title;
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
            _publishedAchievements.Clear();

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

        private void MergeAchievements(List<GeneratedItemViewModelBase> editors, IEnumerable<AssetBase> assets,
            Action<AssetViewModelBase, AssetBase> assign)
        {
            var mergeAssets = new List<AssetBase>(assets);
            if (mergeAssets.Count == 0)
                return;

            var assetEditors = new List<AssetViewModelBase>();

            if (assets.First() is Achievement)
                assetEditors.AddRange(editors.OfType<AchievementViewModel>());
            else
                assetEditors.AddRange(editors.OfType<LeaderboardViewModel>());

            // first pass - look for ID matches
            for (int i = assetEditors.Count - 1; i >= 0; i--)
            {
                AssetBase mergeAsset = null;
                var assetEditor = assetEditors[i];

                if (assetEditor.Generated.Id > 0)
                    mergeAsset = mergeAssets.FirstOrDefault(a => a.Id == assetEditor.Generated.Id);
                if (mergeAsset == null && assetEditor.Published.Id > 0)
                    mergeAsset = mergeAssets.FirstOrDefault(a => a.Id == assetEditor.Published.Id);

                if (mergeAsset != null)
                {
                    assign(assetEditor, mergeAsset);

                    mergeAssets.Remove(mergeAsset);
                    assetEditors.RemoveAt(i);
                }
            }

            // second pass - look for title matches
            for (int i = mergeAssets.Count - 1; i >= 0; i--)
            {
                var mergeAsset = mergeAssets[i];
                var assetEditor = assetEditors.FirstOrDefault(a =>
                    (String.Compare(a.Generated.Title.Text, mergeAsset.Title, StringComparison.InvariantCultureIgnoreCase) == 0 ||
                     String.Compare(a.Published.Title.Text, mergeAsset.Title, StringComparison.InvariantCultureIgnoreCase) == 0)
                );

                if (assetEditor != null)
                {
                    assign(assetEditor, mergeAsset);

                    mergeAssets.RemoveAt(i);
                    assetEditors.Remove(assetEditor);
                }
            }

            // third pass - look for description matches
            for (int i = mergeAssets.Count - 1; i >= 0; i--)
            {
                var mergeAsset = mergeAssets[i];
                var assetEditor = assetEditors.FirstOrDefault(a =>
                    String.Compare(a.Generated.Description.Text, mergeAsset.Description, StringComparison.InvariantCultureIgnoreCase) == 0);

                if (assetEditor != null)
                {
                    assign(assetEditor, mergeAsset);

                    mergeAssets.RemoveAt(i);
                    assetEditors.Remove(assetEditor);
                }
            }

            // TODO: attempt to match requirements

            // create new entries for each remaining unmerged achievement
            foreach (var mergeAsset in mergeAssets)
            {
                AssetViewModelBase assetEditor;

                if (mergeAsset is Achievement)
                    assetEditor = new AchievementViewModel(this, null);
                else
                    assetEditor = new LeaderboardViewModel(this, null);

                assign(assetEditor, mergeAsset);
                editors.Add(assetEditor);
            }
        }

        private void MergePublished(List<GeneratedItemViewModelBase> assets)
        {
            MergeAchievements(assets, _publishedAchievements, (vm, a) => vm.Published.Asset = a);
        }

        private void MergeLocal(List<GeneratedItemViewModelBase> assets)
        {
            MergeAchievements(assets, _localAssets.Achievements, (vm, a) => vm.Local.Asset = a);
            MergeAchievements(assets, _localAssets.Leaderboards, (vm, a) => vm.Local.Asset = a);

            LocalAchievementCount = _localAssets.Achievements.Count();
            LocalAchievementPoints = _localAssets.Achievements.Sum(a => a.Points);

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
