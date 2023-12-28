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
        public Achievement()
        {
            Trigger = new Trigger();
        }

        /// <summary>
        /// Gets the trigger for the achievement.
        /// </summary>
        public Trigger Trigger { get; internal set; }

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
        public IEnumerable<Requirement> CoreRequirements
        {
            get { return Trigger.Core.Requirements; }
        }

        /// <summary>
        /// Gets the alternate requirements for the achivement.
        /// </summary>
        public IEnumerable<IEnumerable<Requirement>> AlternateRequirements
        {
            get
            {
                foreach (var alt in Trigger.Alts)
                    yield return alt.Requirements;
            }
        }
    }
}