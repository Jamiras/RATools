using Jamiras.Components;
using Jamiras.Database;
using Jamiras.Services;
using RATools.Data;
using RATools.Parser;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows.Media.Media3D;

namespace RATools.ViewModels.Navigation
{
    internal class NavigationListViewModel
    {
        public NavigationListViewModel(GameViewModel gameViewModel, PublishedAssets publishedAssets, LocalAssets localAssets, List<ViewerViewModelBase> editors)
            : this(gameViewModel, publishedAssets, localAssets, editors, ServiceRepository.Instance.FindService<IBackgroundWorkerService>())
        { 
        }
        public NavigationListViewModel(GameViewModel gameViewModel, PublishedAssets publishedAssets, LocalAssets localAssets, List<ViewerViewModelBase> editors, IBackgroundWorkerService backgroundWorkerService)
        {
            _gameViewModel = gameViewModel;
            _publishedAssets = publishedAssets;
            _localAssets = localAssets;
            _editors = editors;
            _backgroundWorkerService = backgroundWorkerService;
        }

        private readonly GameViewModel _gameViewModel;
        private readonly PublishedAssets _publishedAssets;
        private readonly LocalAssets _localAssets;
        private readonly List<ViewerViewModelBase> _editors;
        private readonly IBackgroundWorkerService _backgroundWorkerService;

        private void MergeScript()
        {
            if (_gameViewModel.Script != null)
            {
                if (!_editors.Contains(_gameViewModel.Script))
                    _editors.Add(_gameViewModel.Script);

                var scriptFolder = _gameViewModel.NavigationNodes.OfType<FolderNavigationViewModel>().First(n => n.Label == "Script");
                var scriptNode = scriptFolder.Children.OfType<ScriptNavigationViewModel>().FirstOrDefault();
                if (scriptNode == null)
                {
                    scriptNode = new ScriptNavigationViewModel(_gameViewModel.Script);
                    scriptFolder.AddChild(scriptNode);
                }
                else
                {
                    scriptNode.Editor = _gameViewModel.Script;
                }
            }
        }

        private void MergeGenerated(AchievementScriptInterpreter interpreter)
        {
            if (!String.IsNullOrEmpty(interpreter.RichPresence))
            {
                var richPresenceViewModel = _editors.OfType<RichPresenceViewModel>().FirstOrDefault();
                if (richPresenceViewModel == null)
                {
                    richPresenceViewModel = new RichPresenceViewModel(_gameViewModel);
                    _editors.Add(richPresenceViewModel);
                }

                richPresenceViewModel.Generated.Asset = new RichPresence
                {
                    Script = interpreter.RichPresence
                };
                richPresenceViewModel.SourceLine = interpreter.RichPresenceLine;
            }

            MergeAndPruneAssets<AchievementViewModel>(interpreter.Achievements, 10000, (vm, a) =>
            {
                vm.Generated.Asset = a;
                if (a != null)
                    vm.SourceLine = interpreter.GetSourceLine((Achievement)a);
            }, (vm) => vm.Generated.Asset);

            MergeAndPruneAssets<LeaderboardViewModel>(interpreter.Leaderboards, 10000, (vm, a) =>
            {
                vm.Generated.Asset = a;
                if (a != null)
                    vm.SourceLine = interpreter.GetSourceLine((Leaderboard)a);
            }, (vm) => vm.Generated.Asset);
        }

        private void UpdateTemporaryIds()
        {
            // find the maximum temporary id already assigned
            int nextLocalId = AssetBase.FirstLocalId;
            foreach (var assetViewModel in _editors.OfType<AssetViewModelBase>())
            {
                var id = assetViewModel.Local.Id;
                if (id == 0)
                    id = assetViewModel.Generated.Id;
                if (id >= nextLocalId)
                    nextLocalId = id + 1;
            }

            foreach (var assetViewModel in _editors.OfType<AssetViewModelBase>())
            {
                if (assetViewModel.AllocateLocalId(nextLocalId))
                    nextLocalId++;

                assetViewModel.Refresh();
            }
        }

        public void MergeAssets(IEnumerable<AssetBase> assets,
            Action<AssetViewModelBase, AssetBase> assign)
        {
            var mergeAssets = new List<AssetBase>(assets);
            if (mergeAssets.Count == 0)
                return;

            var assetEditors = new List<AssetViewModelBase>();

            var achievements = new List<Achievement>();
            var leaderboards = new List<Leaderboard>();
            if (assets.First() is Achievement)
                assetEditors.AddRange(_editors.OfType<AchievementViewModel>());
            else
                assetEditors.AddRange(_editors.OfType<LeaderboardViewModel>());

            // first pass - match by ID
            int j = 0;
            while (j < assetEditors.Count)
            {
                var editor = assetEditors[j];
                AssetBase mergeAsset = null;

                var id = editor.Published.Asset?.Id;
                if (id > 0)
                    mergeAsset = mergeAssets.FirstOrDefault(a => a.Id == id && a.GetType() == editor.Published.Asset.GetType());

                if (mergeAsset == null)
                {
                    id = editor.Generated.Asset?.Id;
                    if (id > 0)
                        mergeAsset = mergeAssets.FirstOrDefault(a => a.Id == id && a.GetType() == editor.Generated.Asset.GetType());

                    if (mergeAsset == null)
                    {
                        id = editor.Local.Asset?.Id;
                        if (id > 0)
                            mergeAsset = mergeAssets.FirstOrDefault(a => a.Id == id && a.GetType() == editor.Local.Asset.GetType());
                    }
                }

                if (mergeAsset != null)
                {
                    assign(editor, mergeAsset);
                    assetEditors.RemoveAt(j);
                    
                    mergeAssets.Remove(mergeAsset);
                    if (!mergeAssets.Any())
                        break;

                    continue;
                }

                var achievement = editor.Published.Asset as Achievement;
                if (achievement != null)
                    achievements.Add(achievement);
                achievement = editor.Generated.Asset as Achievement;
                if (achievement != null)
                    achievements.Add(achievement);
                achievement = editor.Local.Asset as Achievement;
                if (achievement != null)
                    achievements.Add(achievement);

                var leaderboard = editor.Published.Asset as Leaderboard;
                if (leaderboard != null)
                    leaderboards.Add(leaderboard);
                leaderboard = editor.Generated.Asset as Leaderboard;
                if (leaderboard != null)
                    leaderboards.Add(leaderboard);
                leaderboard = editor.Local.Asset as Leaderboard;
                if (leaderboard != null)
                    leaderboards.Add(leaderboard);

                ++j;
            }

            // second pass - match by title/description
            j = 0;
            while (j < mergeAssets.Count && assetEditors.Count > 0)
            {
                AssetViewModelBase editor = null;

                var mergeAsset = mergeAssets[j];
                var achievement = mergeAsset as Achievement;
                if (achievement != null)
                {
                    var match = Achievement.FindMergeAchievement(achievements, achievement);
                    editor = (match != null) ? assetEditors.FirstOrDefault(e => ReferenceEquals(e.Published.Asset, match) || ReferenceEquals(e.Generated.Asset, match) || ReferenceEquals(e.Local.Asset, match)) : null;
                    if (editor == null)
                    {
                        j++;
                        continue;
                    }

                    achievement = editor.Published.Asset as Achievement;
                    if (achievement != null)
                        achievements.Remove(achievement);
                    achievement = editor.Generated.Asset as Achievement;
                    if (achievement != null)
                        achievements.Remove(achievement);
                    achievement = editor.Local.Asset as Achievement;
                    if (achievement != null)
                        achievements.Remove(achievement);
                }
                else
                {
                    var leaderboard = mergeAsset as Leaderboard;
                    if (leaderboard != null)
                    {
                        var match = Leaderboard.FindMergeLeaderboard(leaderboards, leaderboard);
                        editor = (match != null) ? assetEditors.FirstOrDefault(e => ReferenceEquals(e.Published.Asset, match) || ReferenceEquals(e.Generated.Asset, match) || ReferenceEquals(e.Local.Asset, match)) : null;
                        if (editor == null)
                        {
                            j++;
                            continue;
                        }

                        leaderboard = editor.Published.Asset as Leaderboard;
                        if (leaderboard != null)
                            leaderboards.Remove(leaderboard);
                        leaderboard = editor.Generated.Asset as Leaderboard;
                        if (leaderboard != null)
                            leaderboards.Remove(leaderboard);
                        leaderboard = editor.Local.Asset as Leaderboard;
                        if (leaderboard != null)
                            leaderboards.Remove(leaderboard);
                    }
                }

                if (editor != null)
                {
                    assign(editor, mergeAsset);
                    mergeAssets.Remove(mergeAsset);
                    assetEditors.Remove(editor);
                }
            }

            // create new entries for each remaining unmerged achievement
            foreach (var mergeAsset in mergeAssets)
            {
                AssetViewModelBase assetEditor;

                if (mergeAsset is Achievement)
                    assetEditor = new AchievementViewModel(_gameViewModel);
                else
                    assetEditor = new LeaderboardViewModel(_gameViewModel);

                assign(assetEditor, mergeAsset);
                _editors.Add(assetEditor);
            }
        }

        private void MergeAndPruneAssets<T>(IEnumerable<AssetBase> assets, int sortOrder,
            Action<AssetViewModelBase, AssetBase> assign,
            Func<AssetViewModelBase, AssetBase> getAsset)
            where T : AssetViewModelBase
        {
            MergeAssets(assets, assign);
            
            // clear out any items not in the new list
            for (int i = _editors.Count - 1; i >= 0; i--)
            {
                var editor = _editors[i] as T;
                if (editor != null)
                {
                    var asset = getAsset(editor);
                    if (asset != null && !assets.Contains(asset))
                        assign(editor, null);
                }
            }

            // update the sort orders 
            foreach (var asset in assets)
            {
                var editor = _editors.OfType<T>().FirstOrDefault(e => ReferenceEquals(getAsset(e), asset));
                if (editor != null && (editor.SortOrder == 0 || editor.SortOrder > sortOrder))
                    editor.SortOrder = sortOrder++;
            }
        }

        private void MergePublished()
        {
            MergeAndPruneAssets<AchievementViewModel>(_publishedAssets.Achievements, 30000, (vm, a) => vm.Published.Asset = a, (vm) => vm.Published.Asset);
            MergeAndPruneAssets<LeaderboardViewModel>(_publishedAssets.Leaderboards, 30000, (vm, a) => vm.Published.Asset = a, (vm) => vm.Published.Asset);

            if (_publishedAssets.RichPresence != null)
            {
                var richPresenceViewModel = _editors.OfType<RichPresenceViewModel>().FirstOrDefault();
                if (richPresenceViewModel == null)
                {
                    richPresenceViewModel = new RichPresenceViewModel(_gameViewModel);
                    _editors.Add(richPresenceViewModel);
                }

                richPresenceViewModel.Published.Asset = _publishedAssets.RichPresence;
            }
        }

        private void MergeLocal()
        {
            MergeAndPruneAssets<AchievementViewModel>(_localAssets.Achievements, 20000, (vm, a) => vm.Local.Asset = a, (vm) => vm.Local.Asset);
            MergeAndPruneAssets<LeaderboardViewModel>(_localAssets.Leaderboards, 20000, (vm, a) => vm.Local.Asset = a, (vm) => vm.Local.Asset);

            if (_localAssets.RichPresence != null)
            {
                var richPresenceViewModel = _editors.OfType<RichPresenceViewModel>().FirstOrDefault();
                if (richPresenceViewModel == null)
                {
                    richPresenceViewModel = new RichPresenceViewModel(_gameViewModel);
                    _editors.Add(richPresenceViewModel);
                }

                richPresenceViewModel.Local.Asset = _localAssets.RichPresence;
            }
        }

        private void UpdateNavigationNodes()
        {
            MergeScript();

            var richPresence = _editors.OfType<RichPresenceViewModel>().FirstOrDefault();
            if (richPresence != null)
            {
                var richPresenceNode = _gameViewModel.NavigationNodes.OfType<RichPresenceNavigationViewModel>().First();
                richPresenceNode.Editor = richPresence;
            }

            var achievementsFolder = _gameViewModel.NavigationNodes.OfType<FolderNavigationViewModel>().First(n => n.Label == "Achievements");
            var achievementNodes = achievementsFolder.Children.OfType<AchievementNavigationViewModel>().ToList();
            foreach (var achievement in _editors.OfType<AchievementViewModel>())
            {
                var node = achievementNodes.FirstOrDefault(n => n.IsNodeFor(achievement));
                if (node != null)
                {
                    if (!ReferenceEquals(node.Editor, achievement))
                        node.Editor = achievement;

                    achievementNodes.Remove(node);
                }
                else
                {
                    node = new AchievementNavigationViewModel(achievement);
                    achievementsFolder.AddChild(node);
                }
            }
            foreach (var achievementNode in achievementNodes)
                achievementsFolder.Children.Remove(achievementNode);

            var leaderboardsFolder = _gameViewModel.NavigationNodes.OfType<FolderNavigationViewModel>().First(n => n.Label == "Leaderboards");
            var leaderboardNodes = leaderboardsFolder.Children.OfType<LeaderboardNavigationViewModel>().ToList();
            foreach (var leaderboard in _editors.OfType<LeaderboardViewModel>())
            {
                var node = leaderboardNodes.FirstOrDefault(n => n.IsNodeFor(leaderboard));
                if (node != null)
                {
                    if (!ReferenceEquals(node.Editor, leaderboard))
                        node.Editor = leaderboard;

                    leaderboardNodes.Remove(node);
                }
                else
                {
                    node = new LeaderboardNavigationViewModel(leaderboard);
                    leaderboardsFolder.AddChild(node);
                }
            }
            foreach (var leaderboardNode in leaderboardNodes)
                leaderboardsFolder.Children.Remove(leaderboardNode);
        }

        private static void ApplySort(ObservableCollection<NavigationViewModelBase> nodes)
        {
            if (nodes == null || nodes.Count < 2)
                return;

            var order = new List<int>();

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                var editorViewModel = nodes[i] as EditorNavigationViewModelBase;
                if (editorViewModel != null)
                {
                    if (editorViewModel.Editor.SortOrder == -1)
                        nodes.RemoveAt(i);
                    else
                        order.Add(editorViewModel.Editor.SortOrder);
                }
            }
            order.Sort();

            int j = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                var editorViewModel = nodes[i] as EditorNavigationViewModelBase;
                if (editorViewModel != null)
                {
                    if (editorViewModel.Editor.SortOrder != order[j])
                    {
                        for (int k = i + 1; k < nodes.Count; k++)
                        {
                            editorViewModel = nodes[k] as EditorNavigationViewModelBase;
                            if (editorViewModel != null && editorViewModel.Editor.SortOrder == order[j])
                            {
                                nodes.Move(k, i);
                                break;
                            }
                        }
                    }

                    j++;
                }
            }
        }

        public void Merge(AchievementScriptInterpreter interpreter)
        {
            foreach (var editor in _editors.OfType<AssetViewModelBase>())
                editor.SortOrder = 0;

            if (_publishedAssets != null)
            {
                if (_publishedAssets.Achievements.Any() || _publishedAssets.Leaderboards.Any() || _publishedAssets.RichPresence != null)
                    MergePublished();
            }

            if (_localAssets != null)
                MergeLocal();

            if (interpreter != null)
                MergeGenerated(interpreter);

            UpdateTemporaryIds();

            _backgroundWorkerService.InvokeOnUiThread(() =>
            {
                UpdateNavigationNodes();

                foreach (var node in _gameViewModel.NavigationNodes)
                    ApplySort(node.Children);
            });
        }
    }
}
