namespace RATools.Data
{
    public enum AchievementType
    {
        /// <summary>
        /// Unspecified
        /// </summary>
        None = 0,

        /// <summary>
        /// Unflagged
        /// </summary>
        Standard,
        
        /// <summary>
        /// Becomes unearnable after some point in the game, and would require many hours to get back to.
        /// </summary>
        Missable,

        /// <summary>
        /// Required to beat the game.
        /// </summary>
        Progression,

        /// <summary>
        /// Indicates the game was beaten.
        /// </summary>
        WinCondition,
    }
}
