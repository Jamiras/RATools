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
    }
}
