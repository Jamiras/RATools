namespace RATools.ViewModels.Navigation
{
    internal class ScriptNavigationViewModel : EditorNavigationViewModelBase
    {
        public ScriptNavigationViewModel(ScriptViewModel script)
        {
            ImageName = "script";
            ImageTooltip = "Script";
            Editor = script;

            ContextMenu = null;
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
