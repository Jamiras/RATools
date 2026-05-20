namespace RATools.Data
{
    /// <summary>
    /// Defines which other achievement sets will be loaded with an achievement set
    /// </summary>
    public enum AchievementSetType
    {
        /// <summary>
        /// Unspecified.
        /// </summary>
        None = 0,

        /// <summary>
        /// The main beatable achievement set.
        /// </summary>
        Core,

        /// <summary>
        /// Additional challenges deemed too difficult for the core set.
        /// </summary>
        /// <remarks>
        /// Core and Bonus sets will always be loaded together.
        /// </remarks>
        Bonus,

        /// <summary>
        /// A unique way to play the game.
        /// </summary>
        /// <remarks>
        /// Allows loading the core set and potentially bonus sets.
        /// Must be explicitly opted-in by the player.
        /// </remarks>
        Challenge,

        /// <summary>
        /// A unique way to play the game.
        /// </summary>
        /// <remarks>
        /// Still requires a unique hash that targets the specialty set.
        /// Allows loading the core set and potentially bonus sets.
        /// </remarks>
        Specialty,

        /// <summary>
        /// An alternate core set
        /// </summary>
        /// <remarks>
        /// Requires a unique hash that targets the excluside set.
        /// Does not allow loading the core set or bonus sets.
        /// </remarks>
        Exclusive,
    }

    public static class AchievementSetTypeExtension
    {
        /// <summary>
        /// Gets whether or not the achievement set supports progression markings.
        /// </summary>
        public static bool SupportsProgression(this AchievementSetType type)
        {
            switch (type)
            {
                case AchievementSetType.Core:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets whether or not the achievement set loads with the base set.
        /// </summary>
        /// <remarks>If <c>false</c>, the achievement set has its own XXX.json and XXX-User.txt</c></remarks>
        public static bool CanLoadWithBaseSet(this AchievementSetType type)
        {
            switch (type)
            {
                case AchievementSetType.Bonus:
                case AchievementSetType.Challenge:
                    return true;

                default:
                    return false;
            }
        }
    }
}
