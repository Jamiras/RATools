namespace RATools.ViewModels.Navigation
{
    internal class RichPresenceNavigationViewModel : EditorNavigationViewModelBase
    {
        public RichPresenceNavigationViewModel(RichPresenceViewModel richPresence)
        {
            ImageName = "rich_presence";
            ImageTooltip = "Rich Presence";

            Editor = richPresence;
            Label = "Rich Presence";
        }
    }
}
