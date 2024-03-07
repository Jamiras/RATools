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
            InitializeForUI();
        }

        internal GameViewModel(int gameId, string title,
            ILogger logger, IFileSystemService fileSystemService)
        {
            /* unit tests call this constructor directly and will provide their own Script object and don't need Resources */
            GameId = gameId;
            Title = title;
            Notes = new Dictionary<int, string>();
            GoToSourceCommand = new DelegateCommand<int>(GoToSource);

            _publishedAchievements = new List<Achievement>();
            _publishedLeaderboards = new List<Leaderboard>();
            _publishedRichPresence = null;

            _logger = logger;
            _fileSystemService = fileSystemService;

            _backNavigationStack = new FixedSizeStack<NavigationItem>(128);
            _forwardNavigationStack = new Stack<NavigationItem>(32);
        }

        private readonly ILogger _logger;
        protected readonly IFileSystemService _fileSystemService;
        protected readonly List<Achievement> _publishedAchievements;
        protected readonly List<Leaderboard> _publishedLeaderboards;
        protected RichPresence _publishedRichPresence;
        protected LocalAssets _localAssets;

        public void InitializeForUI()
        {
            Resources = new ResourceContainer();

            Script = new ScriptViewModel(this);
            SelectedEditor = Script;
        }

        private class NavigationItem
        {
            public string EditorType { get; set; }
            public int EditorId { get; set; }
            public TextLocation CursorPosition { get; set; }
        }

        private readonly FixedSizeStack<NavigationItem> _backNavigationStack;
        private readonly Stack<NavigationItem> _forwardNavigationStack;
        private bool _disableNavigationCapture = false;

        internal int GameId { get; private set; }
        internal string RACacheDirectory { get; private set; }
        internal Dictionary<int, string> Notes { get; private set; }

        public string LocalFilePath { get { return _localAssets.Filename; } }

        public ScriptViewModel Script { get; protected set; }

        public CommandBase<int> GoToSourceCommand { get; private set; }
        private void GoToSource(int line)
        {
            if (SelectedEditor == Script)
            {
                // script editor already selected, just go to the specified line
                // allow the default navigation logic to capture the old location
                Script.Editor.GotoLine(line);
            }
            else
            {
                // changing to the script editor. allow OnSelectedEditorChanged to
                // capture the old location, then disable capture while moving the cursor
                SelectedEditor = Script;

                _disableNavigationCapture = true;
                try
                {
                    Script.Editor.GotoLine(line);
                }
                finally
                {
                    _disableNavigationCapture = false;
                }
            }

            Script.Editor.IsFocusRequested = true;
        }

        public void NavigateForward()
        {
            if (_forwardNavigationStack.Count > 0)
            {
                _backNavigationStack.Push(CaptureNavigationItem(SelectedEditor));
                var item = _forwardNavigationStack.Pop();
                NavigateTo(item);
            }
        }

        public void NavigateBack()
        {
            if (_backNavigationStack.Count > 0)
            {
                _forwardNavigationStack.Push(CaptureNavigationItem(SelectedEditor));
                var item = _backNavigationStack.Pop();
                NavigateTo(item);
            }
        }

        private void NavigateTo(NavigationItem item)
        {
            ViewerViewModelBase editor = null;
            foreach (var scan in Editors)
            {
                if (scan.ViewerType != item.EditorType)
                    continue;

                var assetEditor = scan as AssetViewModelBase;
                if (assetEditor != null && assetEditor.Id != item.EditorId)
                    continue;

                editor = scan;
                break;
            }

            if (editor == null)
                return;

            _disableNavigationCapture = true;
            try
            {
                SelectedEditor = editor;

                var scriptEditor = editor as ScriptViewModel;
                if (scriptEditor != null)
                {
                    // scroll the line into view
                    scriptEditor.Editor.GotoLine(item.CursorPosition.Line);

                    // then go to the correct column
                    if (item.CursorPosition.Column != scriptEditor.Editor.CursorColumn)
                        scriptEditor.Editor.MoveCursorTo(item.CursorPosition.Line, item.CursorPosition.Column, Jamiras.ViewModels.CodeEditor.CodeEditorViewModel.MoveCursorFlags.None);

                    scriptEditor.Editor.IsFocusRequested = true;
                }
            }
            finally
            {
                _disableNavigationCapture = false;
            }
        }

        public void CaptureNavigationLocation()
        {
            if (_disableNavigationCapture)
                return;

            var editor = SelectedEditor;
            if (editor == null)
                return;

            var item = CaptureNavigationItem(editor);
            if (_backNavigationStack.Count > 0)
            {
                // cursor didn't move. don't duplicate the capture
                var last = _backNavigationStack.Peek();
                if (last.EditorId == item.EditorId && last.EditorType == item.EditorType && last.CursorPosition == item.CursorPosition)
                    return;
            }

            _backNavigationStack.Push(item);
            _forwardNavigationStack.Clear();
        }

        private static NavigationItem CaptureNavigationItem(ViewerViewModelBase editor)
        {
            var item = new NavigationItem { EditorType = editor.ViewerType };
            var assetEditor = editor as AssetViewModelBase;
            if (assetEditor != null)
                item.EditorId = assetEditor.Id;

            var scriptEditor = editor as ScriptViewModel;
            if (scriptEditor != null)
                item.CursorPosition = new TextLocation(scriptEditor.Editor.CursorLine, scriptEditor.Editor.CursorColumn);

            return item;
        }

        internal void PopulateEditorList(AchievementScriptInterpreter interpreter)
        { 
            var editors = new List<ViewerViewModelBase>();
            if (Script != null)
                editors.Add(Script);

            var selectedEditor = (SelectedEditor != null) ? SelectedEditor.Title : null;

            if (interpreter != null)
            {
                GeneratedAchievementCount = interpreter.Achievements.Count();
                editors.Capacity += GeneratedAchievementCount;

                if (!String.IsNullOrEmpty(interpreter.RichPresence))
                {
                    var richPresenceViewModel = new RichPresenceViewModel(this);
                    richPresenceViewModel.Generated.Asset = new RichPresence
                    {
                        Script = interpreter.RichPresence
                    };
                    richPresenceViewModel.SourceLine = interpreter.RichPresenceLine;
                    editors.Add(richPresenceViewModel);
                }

                foreach (var achievement in interpreter.Achievements)
                {
                    var achievementViewModel = new AchievementViewModel(this);
                    achievementViewModel.Generated.Asset = achievement;
                    achievementViewModel.SourceLine = interpreter.GetSourceLine(achievement);
                    editors.Add(achievementViewModel);
                }

                foreach (var leaderboard in interpreter.Leaderboards)
                {
                    var leaderboardViewModel = new LeaderboardViewModel(this);
                    leaderboardViewModel.Generated.Asset = leaderboard;
                    leaderboardViewModel.SourceLine = interpreter.GetSourceLine(leaderboard);
                    editors.Add(leaderboardViewModel);
                }
            }
            else
            {
                GeneratedAchievementCount = 0;
            }

            if (_publishedAchievements.Count > 0 || _publishedLeaderboards.Count > 0 || _publishedRichPresence != null)
                MergePublished(editors);

            if (_localAssets != null)
                MergeLocal(editors);

            UpdateTemporaryIds(editors);

            SelectedEditor = editors.FirstOrDefault(e => e.Title == selectedEditor);
            Editors = editors;
        }

        private void UpdateTemporaryIds(List<ViewerViewModelBase> editors)
        {
            // find the maximum temporary id already assigned
            int nextLocalId = AssetBase.FirstLocalId;
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

        public static readonly ModelProperty EditorsProperty = ModelProperty.Register(typeof(GameViewModel), "Editors", typeof(IEnumerable<ViewerViewModelBase>), new ViewerViewModelBase[0]);
        public IEnumerable<ViewerViewModelBase> Editors
        {
            get { return (IEnumerable<ViewerViewModelBase>)GetValue(EditorsProperty); }
            private set { SetValue(EditorsProperty, value); }
        }

        public static readonly ModelProperty SelectedEditorProperty = ModelProperty.Register(typeof(GameViewModel), "SelectedEditor", typeof(ViewerViewModelBase), null, OnSelectedEditorChanged);
        public ViewerViewModelBase SelectedEditor
        {
            get { return (ViewerViewModelBase)GetValue(SelectedEditorProperty); }
            set { SetValue(SelectedEditorProperty, value); }
        }

        private static void OnSelectedEditorChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            if (e.OldValue != null)
            {
                var gameViewModel = ((GameViewModel)sender);
                if (!gameViewModel._disableNavigationCapture)
                {
                    var item = CaptureNavigationItem((ViewerViewModelBase)e.OldValue);
                    gameViewModel._backNavigationStack.Push(item);
                    gameViewModel._forwardNavigationStack.Clear();
                }
            }
        }

        internal void UpdateLocal(Achievement achievement, Achievement localAchievement, StringBuilder warning, bool validateAll)
        {
            if (_localAchievementCommitSuspendCount == 0)
            {
                _localAssets.MergeExternalChanges((asset, change) =>
                {
                    var modifiedAchievement = asset as Achievement;
                    if (modifiedAchievement != null && modifiedAchievement.Id == achievement.Id)
                        localAchievement = (change == LocalAssets.LocalAssetChange.Removed) ? null : modifiedAchievement;
                    else
                        HandleLocalAssetChange(asset, change);
                });
            }

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
            {
                _localAssets.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName, warning, validateAll ? null : new List<AssetBase>() { achievement });
                LocalAchievementCount = _localAssets.Achievements.Count();
                LocalAchievementPoints = _localAssets.Achievements.Sum(a => a.Points);
            }
        }

        internal void UpdateLocal(Leaderboard leaderboard, Leaderboard localLeaderboard, StringBuilder warning, bool validateAll)
        {
            if (_localAchievementCommitSuspendCount == 0)
            {
                _localAssets.MergeExternalChanges((asset, change) =>
                {
                    var modifiedLeaderboard = asset as Leaderboard;
                    if (modifiedLeaderboard != null && modifiedLeaderboard.Id == leaderboard.Id)
                        localLeaderboard = (change == LocalAssets.LocalAssetChange.Removed) ? null : modifiedLeaderboard;
                    else
                        HandleLocalAssetChange(asset, change);
                });
            }

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
                _localAssets.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName, warning, validateAll ? null : new List<AssetBase>() { leaderboard });
        }

        internal void UpdateLocal(RichPresence richPresence, RichPresence localRichPresence, StringBuilder warning, bool validateAll)
        {
            if (_localAchievementCommitSuspendCount == 0)
            {
                _localAssets.MergeExternalChanges((asset, change) =>
                {
                    var modifiedRichPresence = asset as RichPresence;
                    if (modifiedRichPresence != null)
                        localRichPresence = (change == LocalAssets.LocalAssetChange.Removed) ? null : modifiedRichPresence;
                    else
                        HandleLocalAssetChange(asset, change);
                });
            }

            if (richPresence == null)
            {
                _logger.WriteVerbose("Deleting local rich presence");
                _localAssets.Replace(localRichPresence, null);
            }
            else
            {
                if (localRichPresence != null)
                    _logger.WriteVerbose("Updating rich presence");
                else
                    _logger.WriteVerbose("Committing rich presence");

                _localAssets.Replace(localRichPresence, richPresence);
            }

            if (_localAchievementCommitSuspendCount == 0)
                _localAssets.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName, warning, validateAll ? null : new List<AssetBase>() { _localAssets.RichPresence });
        }

        private int _localAchievementCommitSuspendCount = 0;
        internal void SuspendCommitLocalAchievements()
        {
            if (_localAchievementCommitSuspendCount == 0)
                _localAssets.MergeExternalChanges(HandleLocalAssetChange);

            ++_localAchievementCommitSuspendCount;
        }

        internal void ResumeCommitLocalAchievements(StringBuilder warning, List<AssetBase> assetsToValidate)
        {
            if (_localAchievementCommitSuspendCount > 0 && --_localAchievementCommitSuspendCount == 0)
            {
                _localAssets.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName, warning, assetsToValidate);

                LocalAchievementCount = _localAssets.Achievements.Count();
                LocalAchievementPoints = _localAssets.Achievements.Sum(a => a.Points);
            }
        }

        private void HandleLocalAssetChange(AssetBase asset, LocalAssets.LocalAssetChange change)
        {
            var editors = (List<ViewerViewModelBase>)Editors;

            var richPresence = asset as RichPresence;
            if (richPresence != null)
            {
                var richPresenceViewModel = editors.OfType<RichPresenceViewModel>().FirstOrDefault();
                if (richPresenceViewModel != null)
                    richPresenceViewModel.Local.Asset = richPresence;

                return;
            }

            switch (change)
            {
                case LocalAssets.LocalAssetChange.Added:
                case LocalAssets.LocalAssetChange.Modified:
                    MergeAchievements(editors, new[] { asset }, (vm, a) =>
                    {
                        vm.Local.Asset = a;
                        vm.Refresh();
                    });
                    break;

                case LocalAssets.LocalAssetChange.Removed:
                    AssetViewModelBase editor = null;

                    var achievement = asset as Achievement;
                    if (achievement != null)
                        editor = editors.OfType<AchievementViewModel>().FirstOrDefault(e => e.Id == achievement.Id);

                    var leaderboard = asset as Leaderboard;
                    if (leaderboard != null)
                        editor = editors.OfType<LeaderboardViewModel>().FirstOrDefault(e => e.Id == leaderboard.Id);

                    if (editor != null)
                    {
                        if (editor.Published.Asset == null && !editor.IsGenerated)
                            editors.Remove(editor);
                        else
                            editor.Local.Asset = null;

                        editor.Refresh();
                    }
                    break;
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

            foreach (var kvp in _localAssets.Notes)
                Notes[kvp.Key] = kvp.Value;
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
            _publishedLeaderboards.Clear();
            _publishedRichPresence = null;

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
                if (publishedAchievements.Type == JsonFieldType.ObjectArray)
                {
                    foreach (var publishedAchievement in publishedAchievements.ObjectArrayValue)
                    {
                        var builder = new AchievementBuilder();
                        builder.Id = publishedAchievement.GetField("ID").IntegerValue.GetValueOrDefault();
                        builder.Title = publishedAchievement.GetField("Title").StringValue;
                        builder.Description = publishedAchievement.GetField("Description").StringValue;
                        builder.Points = publishedAchievement.GetField("Points").IntegerValue.GetValueOrDefault();
                        builder.BadgeName = publishedAchievement.GetField("BadgeName").StringValue;
                        builder.ParseRequirements(Tokenizer.CreateTokenizer(publishedAchievement.GetField("MemAddr").StringValue));
                        builder.Category = publishedAchievement.GetField("Flags").IntegerValue.GetValueOrDefault();

                        var typeField = publishedAchievement.GetField("Type");
                        if (!String.IsNullOrEmpty(typeField.StringValue))
                            builder.Type = Achievement.ParseType(typeField.StringValue);

                        var builtAchievement = builder.ToAchievement();
                        builtAchievement.Published = UnixEpoch.AddSeconds(publishedAchievement.GetField("Created").IntegerValue.GetValueOrDefault());
                        builtAchievement.LastModified = UnixEpoch.AddSeconds(publishedAchievement.GetField("Modified").IntegerValue.GetValueOrDefault());

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
                }

                var publishedLeaderboards = publishedData.GetField("Leaderboards");
                if (publishedLeaderboards.Type == JsonFieldType.ObjectArray)
                {
                    foreach (var publishedLeaderboard in publishedLeaderboards.ObjectArrayValue)
                    {
                        var leaderboard = new Leaderboard();
                        leaderboard.Id = publishedLeaderboard.GetField("ID").IntegerValue.GetValueOrDefault();
                        leaderboard.Title = publishedLeaderboard.GetField("Title").StringValue;
                        leaderboard.Description = publishedLeaderboard.GetField("Description").StringValue;
                        leaderboard.Format = Leaderboard.ParseFormat(publishedLeaderboard.GetField("Format").StringValue);
                        leaderboard.LowerIsBetter = publishedLeaderboard.GetField("LowerIsBetter").BooleanValue;

                        var mem = publishedLeaderboard.GetField("Mem").StringValue;
                        var tokenizer = Tokenizer.CreateTokenizer(mem);
                        while (tokenizer.NextChar != '\0')
                        {
                            var part = tokenizer.ReadTo("::");
                            if (part.StartsWith("STA:"))
                                leaderboard.Start = part.Substring(4);
                            else if (part.StartsWith("CAN:"))
                                leaderboard.Cancel = part.Substring(4);
                            else if (part.StartsWith("SUB:"))
                                leaderboard.Submit = part.Substring(4);
                            else if (part.StartsWith("VAL:"))
                                leaderboard.Value = part.Substring(4);

                            tokenizer.Advance(2);
                        }

                        if (String.IsNullOrEmpty(leaderboard.Start))
                            leaderboard.Start = "1=1";
                        if (String.IsNullOrEmpty(leaderboard.Cancel))
                            leaderboard.Cancel = "1=1";
                        if (String.IsNullOrEmpty(leaderboard.Submit))
                            leaderboard.Submit = "1=1";
                        if (String.IsNullOrEmpty(leaderboard.Value))
                            leaderboard.Value = "0";

                        _publishedLeaderboards.Add(leaderboard);
                    }

                    var publishedRichPresence = publishedData.GetField("RichPresencePatch");
                    if (publishedRichPresence.Type == JsonFieldType.String)
                        _publishedRichPresence = new RichPresence { Script = publishedRichPresence.StringValue };
                }

                CoreAchievementCount = coreCount;
                CoreAchievementPoints = corePoints;
                UnofficialAchievementCount = unofficialCount;
                UnofficialAchievementPoints = unofficialPoints;

                _logger.WriteVerbose(String.Format("Identified {0} core achievements ({1} points)", coreCount, corePoints));
                _logger.WriteVerbose(String.Format("Identified {0} unofficial achievements ({1} points)", unofficialCount, unofficialPoints));
            }
        }

        private void MergeAchievements(List<ViewerViewModelBase> editors, IEnumerable<AssetBase> assets,
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

            var achievements = new List<Achievement>();
            var leaderboards = new List<Leaderboard>();
            foreach (var editor in assetEditors)
            {
                if (editor is AchievementViewModel)
                {
                    var achievement = editor.Published.Asset as Achievement;
                    if (achievement != null)
                        achievements.Add(achievement);
                    achievement = editor.Generated.Asset as Achievement;
                    if (achievement != null)
                        achievements.Add(achievement);
                }
                else if (editor is LeaderboardViewModel)
                {
                    var leaderboard = editor.Published.Asset as Leaderboard;
                    if (leaderboard != null)
                        leaderboards.Add(leaderboard);
                    leaderboard = editor.Generated.Asset as Leaderboard;
                    if (leaderboard != null)
                        leaderboards.Add(leaderboard);
                }
            }

            int j = 0;
            while (j < mergeAssets.Count && assetEditors.Count > 0)
            {
                var mergeAsset = mergeAssets[j];

                AssetBase match = null;
                var achievement = mergeAsset as Achievement;
                if (achievement != null)
                    match = Achievement.FindMergeAchievement(achievements, achievement);

                var leaderboard = mergeAsset as Leaderboard;
                if (leaderboard != null)
                    match = Leaderboard.FindMergeLeaderboard(leaderboards, leaderboard);

                if (match == null)
                {
                    j++;
                    continue;
                }

                for (int i = assetEditors.Count - 1; i >= 0; i--)
                {
                    var assetEditor = assetEditors[i];
                    if (ReferenceEquals(assetEditor.Published.Asset, match))
                    {
                        achievement = assetEditor.Published.Asset as Achievement;
                        leaderboard = assetEditor.Published.Asset as Leaderboard;
                    }
                    else if (ReferenceEquals(assetEditor.Generated.Asset, match))
                    {
                        achievement = assetEditor.Generated.Asset as Achievement;
                        leaderboard = assetEditor.Generated.Asset as Leaderboard;
                    }
                    else
                    {
                        continue;
                    }

                    assign(assetEditor, mergeAsset);

                    if (achievement != null)
                        achievements.Remove(achievement);
                    else if (leaderboard != null)
                        leaderboards.Remove(leaderboard);

                    mergeAssets.Remove(mergeAsset);
                    assetEditors.RemoveAt(i);
                    break;
                }
            }

            // create new entries for each remaining unmerged achievement
            foreach (var mergeAsset in mergeAssets)
            {
                AssetViewModelBase assetEditor;

                if (mergeAsset is Achievement)
                    assetEditor = new AchievementViewModel(this);
                else
                    assetEditor = new LeaderboardViewModel(this);

                assign(assetEditor, mergeAsset);
                editors.Add(assetEditor);
            }
        }

        private void MergePublished(List<ViewerViewModelBase> assets)
        {
            MergeAchievements(assets, _publishedAchievements, (vm, a) => vm.Published.Asset = a);
            MergeAchievements(assets, _publishedLeaderboards, (vm, a) => vm.Published.Asset = a);

            if (_publishedRichPresence != null)
            {
                var richPresenceViewModel = assets.OfType<RichPresenceViewModel>().FirstOrDefault();
                if (richPresenceViewModel == null)
                {
                    richPresenceViewModel = new RichPresenceViewModel(this);
                    assets.Add(richPresenceViewModel);
                }

                richPresenceViewModel.Published.Asset = _publishedRichPresence;
            }
        }

        private void MergeLocal(List<ViewerViewModelBase> assets)
        {
            MergeAchievements(assets, _localAssets.Achievements, (vm, a) => vm.Local.Asset = a);
            MergeAchievements(assets, _localAssets.Leaderboards, (vm, a) => vm.Local.Asset = a);

            if (_localAssets.RichPresence != null)
            {
                var richPresenceViewModel = assets.OfType<RichPresenceViewModel>().FirstOrDefault();
                if (richPresenceViewModel == null)
                {
                    richPresenceViewModel = new RichPresenceViewModel(this);
                    if (Script == null) // New Script
                        assets.Add(richPresenceViewModel);
                    else
                        assets.Insert(1, richPresenceViewModel);
                }

                richPresenceViewModel.Local.Asset = _localAssets.RichPresence;
            }

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
