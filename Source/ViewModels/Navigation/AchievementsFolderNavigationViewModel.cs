using RATools.Data;

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
    }
}
