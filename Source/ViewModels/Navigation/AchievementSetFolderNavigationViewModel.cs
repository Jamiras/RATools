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

        public override bool Equals(object obj)
        {
            var that = obj as AchievementSetFolderNavigationViewModel;
            return (that != null && this.AchievementSet.Id == that.AchievementSet.Id);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
