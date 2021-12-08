using RATools.Parser;
using System.Collections.Generic;
using System.Diagnostics;

namespace RATools.Data
{
    /// <summary>
    /// Defines an achievement.
    /// </summary>
    [DebuggerDisplay("{Title} ({Points})")]
    public class Achievement : AssetBase
    {
        internal Achievement()
        {
            CoreRequirements = new Requirement[0];
            AlternateRequirements = new IEnumerable<Requirement>[0];
        }

        /// <summary>
        /// Gets the achievement category (3=Core, 5=Unofficial).
        /// </summary>
        public int Category { get; internal set; }

        /// <summary>
        /// Gets whether or not the achievement is Unofficial.
        /// </summary>
        public override bool IsUnofficial
        {
            get { return Category == 5; }
        }

        /// <summary>
        /// Gets the core requirements for the achievement.
        /// </summary>
        public IEnumerable<Requirement> CoreRequirements { get; internal set; }

        /// <summary>
        /// Gets the alternate requirements for the achivement.
        /// </summary>
        public IEnumerable<IEnumerable<Requirement>> AlternateRequirements { get; internal set; }

        /// <summary>
        /// Determines if the achievement's requirements match a second achievement.
        /// </summary>
        /// <returns><c>true</c> if the requirements match, <c>false</c> if not.</returns>
        public bool AreRequirementsSame(Achievement achievement)
        {
            var builder1 = new AchievementBuilder(this);
            builder1.Optimize();
            var builder2 = new AchievementBuilder(achievement);
            builder2.Optimize();

            return builder1.AreRequirementsSame(builder2);
        }
    }
}
