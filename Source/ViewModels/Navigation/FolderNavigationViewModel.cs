namespace RATools.ViewModels.Navigation
{
    internal class FolderNavigationViewModel : NavigationViewModelBase
    {
        public FolderNavigationViewModel(string label)
        {
            ImageName = "folder";
            ImageTooltip = "Folder";
            Label = label;

            InitChildren();
        }
    }
}
