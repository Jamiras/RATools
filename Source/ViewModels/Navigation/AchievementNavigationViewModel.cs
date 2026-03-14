using RATools.Data;
using System.ComponentModel;

namespace RATools.ViewModels.Navigation
{
    internal class AchievementNavigationViewModel : EditorNavigationViewModelBase
    {
        public AchievementNavigationViewModel(AchievementViewModel achievement)
        {
            ImageName = "achievement";
            ImageTooltip = "Achievement";
            Editor = achievement;
        }

        public AchievementType AchievementType
        {
            get { return ((AchievementViewModel)Editor).AchievementType; }
        }

        public string TypeImage
        {
            get
            {
                switch (((AchievementViewModel)Editor).AchievementType)
                {
                    default:
                    case AchievementType.None: return null;
                    case AchievementType.Missable: return "/RATools;component/Resources/missable.png";
                    case AchievementType.Progression: return "/RATools;component/Resources/progression.png";
                    case AchievementType.WinCondition: return "/RATools;component/Resources/win-condition.png";
                }
            }
        }

        public int Points
        {
            get { return ((AchievementViewModel)Editor).Points; }
        }

        protected override void OnEditorPropertyChanged(PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "AchievementType")
            {
                OnPropertyChanged(() => AchievementType);
                OnPropertyChanged(() => TypeImage);
            }
            else if (e.PropertyName == "Points")
            {
                OnPropertyChanged(() => Points);
            }

            base.OnEditorPropertyChanged(e);
        }
    }
}
