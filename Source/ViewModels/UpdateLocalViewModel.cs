using Jamiras.Commands;
using Jamiras.DataModels;
using Jamiras.ViewModels;
using RATools.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace RATools.ViewModels
{
    public class UpdateLocalViewModel : DialogViewModelBase
    {
        public UpdateLocalViewModel(GameViewModel game)
        {
            _game = game;
            _assets = new ObservableCollection<UpdateAssetViewModel>();
            DialogTitle = "Update Local - " + game.Title;

            foreach (var asset in game.Editors.OfType<AssetViewModelBase>())
            {
                if (asset.IsGenerated || asset.Local.Asset != null)
                    _assets.Add(new UpdateAssetViewModel(asset));
            }

            var localFiles = new List<LocalFile>();
            foreach (var achievementSet in game.AchievementSets)
            {
                var filename = achievementSet.LocalAssets?.Filename;
                if (!string.IsNullOrEmpty(filename) && !localFiles.Any(f => f.FilePath == filename))
                    localFiles.Add(new LocalFile {  FilePath = filename });
            }
            LocalFiles = localFiles;

            GoToFileCommand = new DelegateCommand<string>(GoToFile);
        }

        private readonly GameViewModel _game;

        public class LocalFile
        {
            public string FilePath { get; set; }
            public string FileName { get { return Path.GetFileName(FilePath); } }
        }

        public IEnumerable<LocalFile> LocalFiles { get; private set; }

        public ICommand GoToFileCommand { get; private set; }

        private static void GoToFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                filePath = Path.GetDirectoryName(filePath);
                Process.Start("explorer.exe", "\"" + filePath + "\"");
            }
            else
            {
                Process.Start("explorer.exe", "/select,\"" + filePath + "\"");
            }
        }

        /// <summary>
        /// Gets the list of local and generated assets.
        /// </summary>
        public IEnumerable<UpdateAssetViewModel> Assets
        {
            get { return _assets; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ObservableCollection<UpdateAssetViewModel> _assets;

        protected override void ExecuteOkCommand()
        {
            var warnings = new StringBuilder();
            var assetsToValidate = new List<AssetBase>();

            _game.SuspendCommitLocalAchievements();

            foreach (var assetViewModel in _assets)
            {
                if (assetViewModel.IsUpdated)
                {
                    assetsToValidate.Add(assetViewModel.Asset.Generated.Asset);

                    var warning = new StringBuilder();
                    assetViewModel.Asset.UpdateLocal(warning, true);
                    if (warning.Length > 0)
                    {
                        warnings.AppendFormat("{0} \"{1}\": {2}", assetViewModel.Asset.ViewerType,
                            assetViewModel.Title, warning.ToString());
                        warnings.AppendLine();
                    }
                }
                else if (assetViewModel.IsDeleted)
                {
                    assetsToValidate.Add(assetViewModel.Asset.Local.Asset);

                    assetViewModel.Asset.DeleteLocalCommand.Execute();
                }
            }

            _game.ResumeCommitLocalAchievements(warnings, assetsToValidate);

            if (warnings.Length > 0)
                TaskDialogViewModel.ShowWarningMessage("Your achievements may not function as expected.", warnings.ToString());

            DialogResult = DialogResult.Ok;
        }

        public CommandBase ToggleSelectedForUpdateCommand
        {
            get { return new DelegateCommand(ExecuteToggleSelectedForUpdateCommand); }
        }

        private void ExecuteToggleSelectedForUpdateCommand()
        {
            if (_assets.Any(a => !a.IsUpdated && a.CanUpdate))
            {
                foreach (var a in _assets)
                {
                    if (a.CanUpdate)
                        a.IsUpdated = true;
                }
            }
            else
            {
                foreach (var a in _assets)
                {
                    if (a.CanUpdate)
                        a.IsUpdated = false;
                }
            }
        }

        public CommandBase ToggleSelectedForDeleteCommand
        {
            get { return new DelegateCommand(ExecuteToggleSelectedForDeleteCommand); }
        }

        private void ExecuteToggleSelectedForDeleteCommand()
        {
            if (_assets.Any(a => !a.IsDeleted && a.CanDelete))
            {
                foreach (var a in _assets)
                {
                    if (a.CanDelete)
                        a.IsDeleted = true;
                }
            }
            else
            {
                foreach (var a in _assets)
                {
                    if (a.CanDelete)
                        a.IsDeleted = false;
                }
            }
        }

        /// <summary>
        /// A single asset to update or delete
        /// </summary>
        public class UpdateAssetViewModel : ViewModelBase
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="UpdateAssetViewModel"/> class.
            /// </summary>
            public UpdateAssetViewModel(AssetViewModelBase asset)
            {
                Asset = asset;

                if (!asset.IsGenerated || asset.CompareState == GeneratedCompareState.Same)
                    IsUpdated = false;
            }

            public AssetViewModelBase Asset { get; private set; }

            public string Title
            {
                get { return Asset.Title; }
            }

            public bool CanUpdate
            {
                get { return Asset.IsGenerated && Asset.CompareState != GeneratedCompareState.Same; }
            }

            public bool CanDelete
            {
                get { return Asset.Local.Asset != null; }
            }

            public static readonly ModelProperty IsUpdatedProperty = ModelProperty.Register(typeof(UpdateLocalViewModel), "IsUpdated", typeof(bool), true, OnIsUpdatedChanged);
            public bool IsUpdated
            {
                get { return (bool)GetValue(IsUpdatedProperty); }
                set { SetValue(IsUpdatedProperty, value); }
            }

            private static void OnIsUpdatedChanged(object sender, ModelPropertyChangedEventArgs e)
            {
                var vm = (UpdateAssetViewModel)sender;
                if (vm.IsUpdated)
                    vm.IsDeleted = false;
            }

            public static readonly ModelProperty IsDeletedProperty = ModelProperty.Register(typeof(UpdateLocalViewModel), "IsDeleted", typeof(bool), false, OnIsDeletedChanged);
            public bool IsDeleted
            {
                get { return (bool)GetValue(IsDeletedProperty); }
                set { SetValue(IsDeletedProperty, value); }
            }

            private static void OnIsDeletedChanged(object sender, ModelPropertyChangedEventArgs e)
            {
                var vm = (UpdateAssetViewModel)sender;
                if (vm.IsDeleted)
                    vm.IsUpdated = false;
            }
        }
    }
}
