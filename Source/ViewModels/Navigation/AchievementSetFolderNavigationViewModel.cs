using RATools.Data;

namespace RATools.ViewModels.Navigation
{
    internal class AchievementSetFolderNavigationViewModel : AssetFolderNavigationViewModel
    {
        public AchievementSetFolderNavigationViewModel(AchievementSet achievementSet)
            : base(achievementSet.Title)
        {
            AchievementSet = achievementSet;
        }

        public AchievementSet AchievementSet { get; private set; }
    }
}
