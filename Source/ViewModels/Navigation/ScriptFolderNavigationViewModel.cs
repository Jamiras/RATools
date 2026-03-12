namespace RATools.ViewModels.Navigation
{
    internal class ScriptFolderNavigationViewModel : NavigationViewModelBase
    {
        public ScriptFolderNavigationViewModel()
        {
            ImageName = "folder";
            ImageTooltip = "Folder";
            Label = "Script";

            InitChildren();
        }

        protected override string GetModificationMessage(GeneratedCompareState state)
        {
            switch (state)
            {
                case GeneratedCompareState.LocalDiffers: // ●
                    return "Modified";
                default:
                    return null;
            }
        }
    }
}
