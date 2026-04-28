using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using Jamiras.ViewModels;
using RATools.Data;
using RATools.Parser;
using RATools.Services;
using RATools.ViewModels.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            Notes = new Dictionary<uint, CodeNote>();
            GoToSourceCommand = new DelegateCommand<int>(GoToSource);

            _logger = logger;
            _fileSystemService = fileSystemService;
            _editors = new List<ViewerViewModelBase>();

            _backNavigationStack = new FixedSizeStack<NavigationItem>(128);
            _forwardNavigationStack = new Stack<NavigationItem>(32);
        }

        private readonly ILogger _logger;
        protected readonly IFileSystemService _fileSystemService;
        protected PublishedAssets _publishedAssets;
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
        internal int ConsoleId { get; private set; }
        internal string RACacheDirectory { get; private set; }
        protected void SetRACacheDirectory(string value)
        {
            RACacheDirectory = value;
        }
        internal Dictionary<uint, CodeNote> Notes { get; private set; }
        internal SerializationContext SerializationContext { get; set; }

        public string LocalFilePath { get { return _localAssets?.Filename; } }

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
            ViewerViewModelBase editor = FindEditor(NavigationNodes, item);
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
            var item = new NavigationItem { EditorType = editor.ViewerType, EditorId = editor.ViewerId };

            var scriptEditor = editor as ScriptViewModel;
            if (scriptEditor != null)
                item.CursorPosition = new TextLocation(scriptEditor.Editor.CursorLine, scriptEditor.Editor.CursorColumn);

            return item;
        }

        private static ViewerViewModelBase FindEditor(IEnumerable<NavigationViewModelBase> nodes, NavigationItem item)
        {
            foreach (var node in nodes.OfType<IEditorNavigationViewModel>())
            {
                var editor = node.Editor;
                if (editor.ViewerType == item.EditorType && editor.ViewerId == item.EditorId)
                    return editor;
            }

            foreach (var node in nodes)
            {
                if (node.Children != null && node.Children.Count > 0)
                {
                    var child = FindEditor(node.Children, item);
                    if (child != null)
                        return child;
                }
            }

            return null;
        }

        internal void PopulateEditorList(AchievementScriptInterpreter interpreter)
        {
            if (interpreter != null)
            {
                SerializationContext = interpreter.SerializationContext;

                GeneratedAchievementCount = interpreter.Achievements.Count();
            }
            else
            {
                GeneratedAchievementCount = 0;
            }

            var navigation = new NavigationListViewModel(this, _publishedAssets, _localAssets, _editors);
            NavigationNodes = navigation.Merge(interpreter);

            UpdateSelectedNavigationNode(SelectedNavigationNode);
        }

        internal void UpdateSelectedNavigationNode(NavigationViewModelBase searchNode)
        {
            var newNode = searchNode != null
                ? FindNavigationNode(NavigationNodes, searchNode)
                : FindEditorNavigationNode(NavigationNodes, Script);
            if (!ReferenceEquals(searchNode, newNode))
            {
                SelectedNavigationNode = newNode;

                if (newNode != null)
                    newNode.IsSelected = true;

                if (searchNode != null)
                    searchNode.IsSelected = false;
            }
        }

        internal static NavigationViewModelBase FindNavigationNode(IEnumerable<NavigationViewModelBase> nodes, NavigationViewModelBase searchNode)
        {
            foreach (var node in nodes)
            {
                if (node == searchNode)
                    return node;
            }

            foreach (var node in nodes)
            {
                if (node.Children != null && node.Children.Count > 0)
                {
                    var child = FindNavigationNode(node.Children, searchNode);
                    if (child != null)
                        return child;
                }
            }

            return null;
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

        public static readonly ModelProperty NavigationNodesProperty = ModelProperty.Register(typeof(GameViewModel), "NavigationNodes", typeof(IEnumerable<NavigationViewModelBase>), new NavigationViewModelBase[0]);
        public IEnumerable<NavigationViewModelBase> NavigationNodes
        {
            get { return (IEnumerable<NavigationViewModelBase>)GetValue(NavigationNodesProperty); }
            private set { SetValue(NavigationNodesProperty, value); }
        }

        public static readonly ModelProperty SelectedNavigationNodeProperty = ModelProperty.Register(typeof(GameViewModel), "SelectedNavigationNode", typeof(NavigationViewModelBase), null, OnSelectedNavigationNodeChanged);
        public NavigationViewModelBase SelectedNavigationNode
        {
            get { return (NavigationViewModelBase)GetValue(SelectedNavigationNodeProperty); }
            set { SetValue(SelectedNavigationNodeProperty, value); }
        }

        private static void OnSelectedNavigationNodeChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            var editorNavigationViewModel = e.NewValue as IEditorNavigationViewModel;
            if (editorNavigationViewModel != null)
                ((GameViewModel)sender).SelectedEditor = editorNavigationViewModel.Editor;
        }

        public IEnumerable<ViewerViewModelBase> Editors
        {
            get { return _editors; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly List<ViewerViewModelBase> _editors;

        public static readonly ModelProperty SelectedEditorProperty = ModelProperty.Register(typeof(GameViewModel), "SelectedEditor", typeof(ViewerViewModelBase), null, OnSelectedEditorChanged);
        public ViewerViewModelBase SelectedEditor
        {
            get { return (ViewerViewModelBase)GetValue(SelectedEditorProperty); }
            set { SetValue(SelectedEditorProperty, value); }
        }

        private static void OnSelectedEditorChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            var gameViewModel = ((GameViewModel)sender);

            if (e.OldValue != null)
            {
                if (!gameViewModel._disableNavigationCapture)
                {
                    var item = CaptureNavigationItem((ViewerViewModelBase)e.OldValue);
                    gameViewModel._backNavigationStack.Push(item);
                    gameViewModel._forwardNavigationStack.Clear();
                }
            }

            var selectedNavigationNode = gameViewModel.SelectedNavigationNode as EditorNavigationViewModelBase;
            if (selectedNavigationNode == null || selectedNavigationNode.Editor != e.NewValue)
            {
                var editor = e.NewValue as ViewerViewModelBase;
                gameViewModel.SelectedNavigationNode = FindEditorNavigationNode(gameViewModel.NavigationNodes, editor);
            }
        }

        private static NavigationViewModelBase FindEditorNavigationNode(IEnumerable<NavigationViewModelBase> nodes, ViewerViewModelBase editor)
        {
            foreach (var node in nodes.OfType<IEditorNavigationViewModel>())
            {
                if (ReferenceEquals(node.Editor, editor))
                    return (NavigationViewModelBase)node;
            }

            foreach (var node in nodes)
            {
                if (node.Children != null && node.Children.Count > 0)
                {
                    var child = FindEditorNavigationNode(node.Children, editor);
                    if (child != null)
                        return child;
                }
            }

            return null;
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
                _localAssets.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName, warning,
                    SerializationContext, validateAll ? null : new List<AssetBase>() { achievement },
                    PublishedSets);
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
                _localAssets.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName,
                    warning, SerializationContext, 
                    validateAll ? null : new List<AssetBase>() { leaderboard },
                    PublishedSets);
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
                _localAssets.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName,
                    warning, SerializationContext, 
                    validateAll ? null : new List<AssetBase>() { _localAssets.RichPresence },
                    PublishedSets);
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
                _localAssets.Commit(ServiceRepository.Instance.FindService<ISettings>().UserName,
                    warning, SerializationContext, assetsToValidate, PublishedSets);

                LocalAchievementCount = _localAssets.Achievements.Count();
                LocalAchievementPoints = _localAssets.Achievements.Sum(a => a.Points);

                foreach (var node in NavigationNodes)
                {
                    if (node.Children != null)
                        PruneDeletedAssets(node.Children);
                }
            }
        }

        private static void PruneDeletedAssets(ObservableCollection<NavigationViewModelBase> nodes)
        {
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                var node = nodes[i];

                var editorNode = node as EditorNavigationViewModelBase;
                if (editorNode != null)
                {
                    var assetEditor = editorNode.Editor as AssetViewModelBase;
                    if (assetEditor != null)
                    {
                        if (assetEditor.Generated.Asset == null && assetEditor.Local.Asset == null && assetEditor.Published.Asset == null)
                        {
                            nodes.RemoveAt(i);
                            continue;
                        }
                    }
                }

                if (node.Children != null)
                    PruneDeletedAssets(node.Children);
            }
        }

        private void HandleLocalAssetChange(AssetBase asset, LocalAssets.LocalAssetChange change)
        {
            var richPresence = asset as RichPresence;
            if (richPresence != null)
            {
                var richPresenceViewModel = _editors.OfType<RichPresenceViewModel>().FirstOrDefault();
                if (richPresenceViewModel != null)
                    richPresenceViewModel.Local.Asset = richPresence;

                return;
            }

            switch (change)
            {
                case LocalAssets.LocalAssetChange.Added:
                case LocalAssets.LocalAssetChange.Modified:
                    var navigation = new NavigationListViewModel(this, _publishedAssets, _localAssets, _editors);
                    navigation.MergeAssets(new[] { asset }, (vm, a) =>
                    {
                        vm.Local.Asset = a;
                        vm.Refresh();
                    });
                    break;

                case LocalAssets.LocalAssetChange.Removed:
                    AssetViewModelBase editor = null;

                    var achievement = asset as Achievement;
                    if (achievement != null)
                        editor = _editors.OfType<AchievementViewModel>().FirstOrDefault(e => e.Id == achievement.Id);

                    var leaderboard = asset as Leaderboard;
                    if (leaderboard != null)
                        editor = _editors.OfType<LeaderboardViewModel>().FirstOrDefault(e => e.Id == leaderboard.Id);

                    if (editor != null)
                    {
                        if (editor.Published.Asset == null && !editor.IsGenerated)
                            _editors.Remove(editor);
                        else
                            editor.Local.Asset = null;

                        editor.Refresh();
                    }
                    break;
            }
        }

        public static readonly ModelProperty TitleProperty = ModelProperty.Register(typeof(GameViewModel), "Title", typeof(string), String.Empty);
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            private set { SetValue(TitleProperty, value); }
        }

        public static readonly ModelProperty BadgeNameProperty = ModelProperty.Register(typeof(GameViewModel), "BadgeName", typeof(string), String.Empty);
        public string BadgeName
        {
            get { return (string)GetValue(BadgeNameProperty); }
            set { SetValue(BadgeNameProperty, value); }
        }

        public IEnumerable<AchievementSet> PublishedSets
        {
            get
            {
                return _publishedAssets?.Sets ?? new [] { new AchievementSet { OwnerGameId = GameId, Title = Title } };
            }
        }

        public static readonly ModelProperty GeneratedAchievementCountProperty = ModelProperty.Register(typeof(GameViewModel), "GeneratedAchievementCount", typeof(int), 0);
        public int GeneratedAchievementCount
        {
            get { return (int)GetValue(GeneratedAchievementCountProperty); }
            private set { SetValue(GeneratedAchievementCountProperty, value); }
        }

        public static readonly ModelProperty PromotedAchievementCountProperty = ModelProperty.Register(typeof(GameViewModel), "PromotedAchievementCount", typeof(int), 0);
        public int PromotedAchievementCount
        {
            get { return (int)GetValue(PromotedAchievementCountProperty); }
            private set { SetValue(PromotedAchievementCountProperty, value); }
        }

        public static readonly ModelProperty PromotedAchievementPointsProperty = ModelProperty.Register(typeof(GameViewModel), "PromotedAchievementPoints", typeof(int), 0);
        public int PromotedAchievementPoints
        {
            get { return (int)GetValue(PromotedAchievementPointsProperty); }
            private set { SetValue(PromotedAchievementPointsProperty, value); }
        }

        public static readonly ModelProperty UnpromotedAchievementCountProperty = ModelProperty.Register(typeof(GameViewModel), "UnpromotedAchievementCount", typeof(int), 0);
        public int UnpromotedAchievementCount
        {
            get { return (int)GetValue(UnpromotedAchievementCountProperty); }
            private set { SetValue(UnpromotedAchievementCountProperty, value); }
        }

        public static readonly ModelProperty UnpromotedAchievementPointsProperty = ModelProperty.Register(typeof(GameViewModel), "UnpromotedAchievementPoints", typeof(int), 0);
        public int UnpromotedAchievementPoints
        {
            get { return (int)GetValue(UnpromotedAchievementPointsProperty); }
            private set { SetValue(UnpromotedAchievementPointsProperty, value); }
        }

        public static readonly ModelProperty LocalAchievementCountProperty = ModelProperty.Register(typeof(GameViewModel), "LocalAchievementCount", typeof(int), 0);
        public int LocalAchievementCount
        {
            get { return (int)GetValue(LocalAchievementCountProperty); }
            private set { SetValue(LocalAchievementCountProperty, value); }
        }

        public static readonly ModelProperty LocalAchievementPointsProperty = ModelProperty.Register(typeof(GameViewModel), "LocalAchievementPoints", typeof(int), 0);
        public int LocalAchievementPoints
        {
            get { return (int)GetValue(LocalAchievementPointsProperty); }
            private set { SetValue(LocalAchievementPointsProperty, value); }
        }

        public void AssociateRACacheDirectory(string raCacheDirectory)
        {
            RACacheDirectory = raCacheDirectory;

            ReadPublished();

            var fileName = Path.Combine(RACacheDirectory, GameId + "-User.txt");
            _localAssets = new LocalAssets(fileName, _fileSystemService);

            if (String.IsNullOrEmpty(_localAssets.Title))
                _localAssets.Title = Title;

            foreach (var kvp in _localAssets.Notes)
                Notes[kvp.Key] = new CodeNote(kvp.Key, kvp.Value);

            LocalAchievementCount = _localAssets.Achievements.Count();
            LocalAchievementPoints = _localAssets.Achievements.Sum(a => a.Points);
            _logger.WriteVerbose(String.Format("Read {0} local achievements ({1} points)", LocalAchievementCount, LocalAchievementPoints));
        }

        private void ReadPublished()
        {
            var fileName = Path.Combine(RACacheDirectory, GameId + ".json");
            var publishedAssets = new PublishedAssets(fileName, _fileSystemService);

            var promotedCount = 0;
            var promotedPoints = 0;
            var unpromotedCount = 0;
            var unpromotedPoints = 0;
            foreach (var achievement in publishedAssets.Achievements)
            {
                if (achievement.Category == 3)
                {
                    promotedCount++;
                    promotedPoints += achievement.Points;
                }
                else
                {
                    unpromotedCount++;
                    unpromotedPoints += achievement.Points;
                }
            }

            PromotedAchievementCount = promotedCount;
            PromotedAchievementPoints = promotedPoints;
            UnpromotedAchievementCount = unpromotedCount;
            UnpromotedAchievementPoints = unpromotedPoints;

            Title = publishedAssets.Title;
            ConsoleId = publishedAssets.ConsoleId;

            _logger.WriteVerbose(String.Format("Identified {0} promoted achievements ({1} points)", promotedCount, promotedPoints));
            _logger.WriteVerbose(String.Format("Identified {0} unpromoted achievements ({1} points)", unpromotedCount, unpromotedPoints));

            publishedAssets.LoadNotes();
            Notes = publishedAssets.Notes;
            _logger.WriteVerbose("Read " + Notes.Count + " code notes");

            _publishedAssets = publishedAssets;
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
