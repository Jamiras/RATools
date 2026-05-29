using RATools.Data;
using System.ComponentModel;
using System.Linq;

namespace RATools.ViewModels.Navigation
{
    internal class AchievementsFolderNavigationViewModel : AssetFolderNavigationViewModel, IEditorNavigationViewModel
    {
        public AchievementsFolderNavigationViewModel(GameViewModel game, AchievementSet achievementSet)
            : base("Achievements")
        {
            _game = game;
            _achievementSet = achievementSet;
        }

        private readonly GameViewModel _game;
        private readonly AchievementSet _achievementSet;
        private AchievementsListViewModel _editor;

        public ViewerViewModelBase Editor
        {
            get
            {
                _editor ??= new AchievementsListViewModel(_game, _achievementSet);
                return _editor;
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsSelected" && IsSelected)
            {
                if (_editor != null)
                    _editor.Refresh(_achievementSet, Children.OfType<AchievementNavigationViewModel>());
            }

            base.OnPropertyChanged(e);
        }
    }
}
