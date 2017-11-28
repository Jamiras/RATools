using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RATools.Data
{
    /// <summary>
    /// Defines an achievement.
    /// </summary>
    [DebuggerDisplay("{Title} ({Points})")]
    public class Achievement
    {
        internal Achievement()
        {
            CoreRequirements = new Requirement[0];
            AlternateRequirements = new IEnumerable<Requirement>[0];
        }

        /// <summary>
        /// Gets the unique identifier of the achievement.
        /// </summary>
        public int Id { get; internal set; }

        /// <summary>
        /// Gets the title of the achievement.
        /// </summary>
        public string Title { get; internal set; }

        /// <summary>
        /// Gets the description of the achievement.
        /// </summary>
        public string Description { get; internal set; }

        /// <summary>
        /// Gets the number of points the achievment is worth.
        /// </summary>
        public int Points { get; internal set; }

        /// <summary>
        /// Gets the name of the badge for the achievement.
        /// </summary>
        public string BadgeName { get; internal set; }

        /// <summary>
        /// Gets the date/time the achievement was first published.
        /// </summary>
        public DateTime Published { get; internal set; }

        /// <summary>
        /// Gets the date/time the achievement was last modified.
        /// </summary>
        public DateTime LastModified { get; internal set; }

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
