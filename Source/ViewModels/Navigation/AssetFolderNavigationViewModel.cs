namespace RATools.ViewModels.Navigation
{
    internal class AssetFolderNavigationViewModel : NavigationViewModelBase
    {
        public AssetFolderNavigationViewModel(string label)
        {
            ImageName = "folder";
            ImageTooltip = "Folder";
            Label = label;

            InitChildren();
        }

        protected override string GetModificationMessage(GeneratedCompareState state)
        {
            switch (state)
            {
                case GeneratedCompareState.GeneratedOnly: // ○
                    return "Generated only";
                case GeneratedCompareState.LocalDiffers: // ●
                    return "Generated assets differ from unpublished";
                case GeneratedCompareState.PublishedDiffers: // ◐
                    return "Generated assets differ from published";
                case GeneratedCompareState.NotGenerated: // ◖
                    return "Published assets are not generated";
                default:
                    return null;
            }
        }
    }
}
