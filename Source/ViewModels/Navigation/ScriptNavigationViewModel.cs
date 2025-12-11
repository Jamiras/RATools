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
    }
}
