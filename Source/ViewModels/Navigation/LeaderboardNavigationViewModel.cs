namespace RATools.ViewModels.Navigation
{
    internal class LeaderboardNavigationViewModel : EditorNavigationViewModelBase
    {
        public LeaderboardNavigationViewModel(LeaderboardViewModel leaderboard)
        {
            ImageName = "leaderboard";
            ImageTooltip = "Leaderboard";
            Editor = leaderboard;
        }
    }
}
