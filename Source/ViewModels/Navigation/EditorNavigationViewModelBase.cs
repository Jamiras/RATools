using Jamiras.Commands;
using Jamiras.ViewModels;
using RATools.Data;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RATools.ViewModels.Navigation
{
    public abstract class EditorNavigationViewModelBase : NavigationViewModelBase
    {
        public EditorNavigationViewModelBase()
        {
            var menu = new List<CommandViewModel>();
            menu.Add(new CommandViewModel("Update Local", CanUpdateLocal() ? _editor.UpdateLocalCommand : DisabledCommand.Instance));
            ContextMenu = menu;
        }

        public ViewerViewModelBase Editor
        {
            get { return _editor; }
            set
            {
                if (_editor != value)
                {
                    _editor = value;

                    if (_editor != null)
                        _editor.PropertyChanged += editor_PropertyChanged;

                    Label = _editor?.Title;
                    OnEditorCompareStateChanged();

                    OnPropertyChanged(() => Editor);
                }
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ViewerViewModelBase _editor;

        private void editor_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnEditorPropertyChanged(e);
        }

        protected virtual void OnEditorPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CompareState")
                OnEditorCompareStateChanged();
            else if (e.PropertyName == "Title")
                Label = _editor?.Title;
        }

        private void OnEditorCompareStateChanged()
        {
            CompareState = _editor?.CompareState ?? GeneratedCompareState.None;
            ModificationMessage = _editor?.ModificationMessage;

            var updateLocalMenuItem = ContextMenu?.FirstOrDefault(m => m.Label == "Update Local");
            if (updateLocalMenuItem != null)
                updateLocalMenuItem.Command = CanUpdateLocal() ? Editor.UpdateLocalCommand : DisabledCommand.Instance;
        }

        protected bool CanUpdateLocal()
        {
            if (Editor == null)
                return false;

            switch (Editor.CompareState)
            {
                case GeneratedCompareState.None:
                case GeneratedCompareState.Same:
                    return false;

                default:
                    return true;
            }
        }

        public bool IsNodeFor(AssetBase asset)
        {
            if (asset == null)
                return false;

            var assetViewModel = Editor as AssetViewModelBase;
            if (assetViewModel == null)
                return false;

            if (assetViewModel.Id == asset.Id)
                return true;
            if (ReferenceEquals(assetViewModel.Generated.Asset, asset))
                return true;
            if (ReferenceEquals(assetViewModel.Published.Asset, asset))
                return true;
            if (ReferenceEquals(assetViewModel.Local.Asset, asset))
                return true;

            return false;
        }

        public bool IsNodeFor(AssetViewModelBase editor)
        {
            if (ReferenceEquals(editor, Editor))
                return true;

            if (IsNodeFor(editor.Generated?.Asset))
                return true;
            if (IsNodeFor(editor.Published?.Asset))
                return true;
            if (IsNodeFor(editor.Local?.Asset))
                return true;

            return false;
        }

    }
}
