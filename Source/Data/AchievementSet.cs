using System.Diagnostics;

namespace RATools.Data
{
    [DebuggerDisplay("{Title} ({OwnerSetId})")]
    public class AchievementSet : AssetBase
    {
        /// <summary>
        /// Gets or sets the type classification of the achievement set.
        /// </summary>
        public AchievementSetType Type { get; set; }

        /// <summary>
        /// Gets the name of the badge for the achievement set.
        /// </summary>
        public string BadgeName { get; set; }
    }
}
