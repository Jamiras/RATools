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
                  ServiceRepository.Instance.FindService<IFileSystemService>(),
                  ServiceRepository.Instance.FindService<ISettings>())
        {
            InitializeForUI();
        }

        internal GameViewModel(int gameId, string title,
            ILogger logger, IFileSystemService fileSystemService, ISettings settings)
        {
            /* unit tests call this constructor directly and will provide their own Script object and don't need Resources */
            GameId = gameId;
            Title = title;
            Notes = new Dictionary<uint, CodeNote>();
            GoToSourceCommand = new DelegateCommand<int>(GoToSource);

            _fileSystemService = fileSystemService;
            _settings = settings;

            _achievementSets = new List<AchievementSetViewModel>();
            _achievementSets.Add(new AchievementSetViewModel(new AchievementSet
            {
                OwnerGameId = gameId,
                Title = title,
                Type = AchievementSetType.Core,
            }, logger, fileSystemService));

            _editors = new List<ViewerViewModelBase>();

            _backNavigationStack = new FixedSizeStack<NavigationItem>(128);
            _forwardNavigationStack = new Stack<NavigationItem>(32);
        }

        protected readonly IFileSystemService _fileSystemService;
        protected readonly ISettings _settings;
        protected readonly List<AchievementSetViewModel> _achievementSets;

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
        public string LocalFilePath { get { return _achievementSets.FirstOrDefault()?.LocalAssets?.Filename; } }

        internal Dictionary<uint, CodeNote> Notes { get; private set; }
        internal SerializationContext SerializationContext { get; set; }

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
                var coreSet = _achievementSets.FirstOrDefault(s => s.AchievementSet.Type == AchievementSetType.Core);

                foreach (var set in interpreter.Sets)
                {
                    if (_achievementSets.Any(s => ReferenceEquals(s.AchievementSet, set)))
                        continue;

                    var existingSet = _achievementSets.FirstOrDefault(s => s.Id == set.OwnerSetId);
                    if (existingSet != null)
                    {
                        existingSet.AchievementSet = set;
                    }
                    else if (coreSet != null && set.Type.CanLoadWithBaseSet())
                    {
                        _achievementSets.Add(new AchievementSetViewModel(set, coreSet));
                    }
                    else
                    {
                        var fileName = Path.Combine(RACacheDirectory, set.OwnerGameId + ".json");
                        if (_fileSystemService.FileExists(fileName))
                        {
                            var newSet = new AchievementSetViewModel(set);
                            newSet.AssociateRACacheDirectory(RACacheDirectory);
                            _achievementSets.Add(newSet);
                        }
                        else if (coreSet != null)
                        {
                            _achievementSets.Add(new AchievementSetViewModel(set, coreSet));
                        }
                    }
                }
            }

            var navigation = new NavigationListViewModel(this, _achievementSets, _editors);
            NavigationNodes = navigation.Merge(interpreter);

            UpdateSelectedNavigationNode(SelectedNavigationNode);
            UpdateStatusBarText();
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

        public static readonly ModelProperty StatusBarTextProperty = ModelProperty.Register(typeof(GameViewModel), "StatusBarText", typeof(string), "");
        public string StatusBarText
        {
            get { return (string)GetValue(StatusBarTextProperty); }
            private set { SetValue(StatusBarTextProperty, value); }
        }

        private void UpdateStatusBarText()
        {
            var promotedAchievementCount = 0;
            var promotedAchievementPoints = 0;
            var unpromotedAchievementCount = 0;
            var unpromotedAchievementPoints = 0;
            var localAchievementCount = 0;
            var localAchievementPoints = 0;

            foreach (var achievementSet in _achievementSets)
            {
                if (achievementSet.PublishedAssets != null)
                {
                    foreach (var achievement in achievementSet.PublishedAssets.Achievements)
                    {
                        if (achievement.IsUnpromoted)
                        {
                            unpromotedAchievementCount++;
                            unpromotedAchievementPoints += achievement.Points;
                        }
                        else
                        {
                            promotedAchievementCount++;
                            promotedAchievementPoints += achievement.Points;
                        }
                    }
                }

                if (achievementSet.LocalAssets != null)
                {
                    foreach (var achievement in achievementSet.LocalAssets.Achievements)
                    {
                        localAchievementCount++;
                        localAchievementPoints += achievement.Points;
                    }
                }
            }

            var builder = new StringBuilder();
            if (promotedAchievementCount > 0)
                builder.AppendFormat("Promoted({0}): {1}pts", promotedAchievementCount, promotedAchievementPoints);

            if (unpromotedAchievementCount > 0)
            {
                if (builder.Length > 0)
                    builder.Append("  ");

                builder.AppendFormat("Unpromoted({0}): {1}pts", unpromotedAchievementCount, unpromotedAchievementPoints);
            }

            if (localAchievementCount > 0 || (promotedAchievementCount == 0 && unpromotedAchievementCount == 0))
            {
                if (builder.Length > 0)
                    builder.Append("  ");

                builder.AppendFormat("Local({0}): {1}pts", localAchievementCount, localAchievementPoints);
            }

            StatusBarText = builder.ToString();
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

        /// <summary>
        /// Replaces an existing local achievement with a new value.
        /// </summary>
        /// <param name="achievement">The new achievement, or <c>null</c> to delete the achievement.</param>
        /// <param name="localAchievement">The existing local achievemetn, or <c>null</c> for a new achievement.</param>
        /// <param name="warning">A buffer to capture serialization errors.</param>
        /// <param name="validateAll"><c>false</c> to write the other assets without validating them.</param>
        internal void UpdateLocal(Achievement achievement, Achievement localAchievement, StringBuilder warning, bool validateAll)
        {
            bool refresh = (_localAchievementCommitSuspendCount == 0);

            foreach (var achievementSet in _achievementSets)
            {
                if (achievementSet.UpdateLocal(achievement, localAchievement, HandleLocalAssetChange, refresh))
                    break;
            }

            if (_localAchievementCommitSuspendCount == 0)
            {
                var username = _settings.UserName;
                foreach (var achievementSet in _achievementSets)
                {
                    if (!achievementSet.AchievementSet.Type.CanLoadWithBaseSet())
                    {
                        achievementSet.LocalAssets.Commit(username, warning,
                            SerializationContext, validateAll ? null : new List<AssetBase>() { achievement },
                            PublishedSets);
                    }
                }
            }
        }

        internal void UpdateLocal(Leaderboard leaderboard, Leaderboard localLeaderboard, StringBuilder warning, bool validateAll)
        {
            bool refresh = (_localAchievementCommitSuspendCount == 0);

            foreach (var achievementSet in _achievementSets)
            {
                if (achievementSet.UpdateLocal(leaderboard, localLeaderboard, HandleLocalAssetChange, refresh))
                    break;
            }

            if (_localAchievementCommitSuspendCount == 0)
            {
                var username = _settings.UserName;
                foreach (var achievementSet in _achievementSets)
                {
                    if (!achievementSet.AchievementSet.Type.CanLoadWithBaseSet())
                    {
                        achievementSet.LocalAssets.Commit(username,
                            warning, SerializationContext,
                            validateAll ? null : new List<AssetBase>() { leaderboard },
                            PublishedSets);
                    }
                }
            }
        }

        internal void UpdateLocal(RichPresence richPresence, RichPresence localRichPresence, StringBuilder warning, bool validateAll)
        {
            var coreSet = _achievementSets.FirstOrDefault(s => s.AchievementSet.Type == AchievementSetType.Core);
            if (coreSet == null)
                return;

            bool refresh = (_localAchievementCommitSuspendCount == 0);
            coreSet.UpdateLocal(richPresence, localRichPresence, HandleLocalAssetChange, refresh);

            if (_localAchievementCommitSuspendCount == 0)
            {
                coreSet.LocalAssets.Commit(_settings.UserName,
                    warning, SerializationContext,
                    validateAll ? null : new List<AssetBase>() { coreSet.LocalAssets.RichPresence },
                    PublishedSets);
            }
        }

        private int _localAchievementCommitSuspendCount = 0;
        internal void SuspendCommitLocalAchievements()
        {
            if (_localAchievementCommitSuspendCount == 0)
            {
                foreach (var achievementSet in _achievementSets)
                    achievementSet.MergeExternalChanges(HandleLocalAssetChange);
            }

            ++_localAchievementCommitSuspendCount;
        }

        internal void ResumeCommitLocalAchievements(StringBuilder warning, List<AssetBase> assetsToValidate)
        {
            if (_localAchievementCommitSuspendCount > 0 && --_localAchievementCommitSuspendCount == 0)
            {
                var username = _settings.UserName;
                foreach (var achievementSet in _achievementSets)
                {
                    achievementSet.LocalAssets.Commit(username,
                        warning, SerializationContext, assetsToValidate, PublishedSets);
                }

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
                    var navigation = new NavigationListViewModel(this, _achievementSets, _editors);
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


        public IEnumerable<AchievementSet> PublishedSets
        {
            get
            {
                foreach (var achievementSet in _achievementSets)
                    yield return achievementSet.AchievementSet;
            }
        }

        public void AssociateRACacheDirectory(string raCacheDirectory)
        {
            RACacheDirectory = raCacheDirectory;

            var coreSet = _achievementSets.First();
            coreSet.AssociateRACacheDirectory(raCacheDirectory, _achievementSets);
            Title = coreSet.Title;

            foreach (var kvp in coreSet.PublishedAssets.Notes)
                Notes[kvp.Key] = kvp.Value;

            foreach (var kvp in coreSet.LocalAssets.Notes)
                Notes[kvp.Key] = new CodeNote(kvp.Key, kvp.Value);

            UpdateStatusBarText();
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
