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
        /// Gets the type classification of the achievement.
        /// </summary>
        public AchievementType Type {  get; internal set; }

        /// <summary>
        /// Converts a type string to an <see cref="AchievementType"/>.
        /// </summary>
        /// <param name="type">The type as a string.</param>
        /// <returns>The type as an <see cref="AchievementType"/>, None if not valid.</returns>
        public static AchievementType ParseType(string type)
        {
            switch (type.ToLower())
            {
                case "": return AchievementType.Standard;
                case "progression": return AchievementType.Progression;
                case "win_condition": return AchievementType.WinCondition;
                case "missable": return AchievementType.Missable;
                default: return AchievementType.None;
            }
        }

        public static string GetTypeString(AchievementType type)
        {
            switch (type)
            {
                case AchievementType.Standard: return "";
                case AchievementType.Progression: return "progression";
                case AchievementType.WinCondition: return "win_condition";
                case AchievementType.Missable: return "missable";
                default: return "unknown";
            }
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

    public enum AchievementType
    {
        None = 0,
        Standard,
        Missable,
        Progression,
        WinCondition,
    }
}