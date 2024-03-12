using Jamiras.Components;
using RATools.Data;
using System;
using System.Diagnostics;

namespace RATools.Parser
{
    [DebuggerDisplay("{Title} ({Points}) Core:{_core.Count} Alts:{_alts.Count}")]
    public class AchievementBuilder : TriggerBuilder
    {
        public AchievementBuilder()
            : base()
        {
            Title = String.Empty;
            Description = String.Empty;
            BadgeName = String.Empty;
            Type = AchievementType.Standard;
        }

        public AchievementBuilder(Achievement source)
            : base(source.CoreRequirements, source.AlternateRequirements)
        {
            Title = source.Title;
            Description = source.Description;
            Points = source.Points;
            Id = source.Id;
            BadgeName = source.BadgeName;
            Type = source.Type;
        }

        /// <summary>
        /// Gets or sets the achievement title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the achievement description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the number of points that the achievement is worth.
        /// </summary>
        public int Points { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the achievement.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the category of the achievement.
        /// </summary>
        public int Category { get; set; }

        /// <summary>
        /// Gets or sets the name of the badge for the achievement.
        /// </summary>
        public string BadgeName { get; set; }

        /// <summary>
        /// Gets or sets the type of achievement type classification.
        /// </summary>
        public AchievementType Type { get; set; }

        /// <summary>
        /// Constructs an <see cref="Achievement"/> from the current state of the builder.
        /// </summary>
        public Achievement ToAchievement()
        {
            var trigger = ToTrigger();

            return new Achievement 
            { 
                Title = Title,
                Description = Description,
                Points = Points,
                Trigger = trigger,
                Category = Category,
                Id = Id,
                BadgeName = BadgeName,
                Type = Type,
            };
        }

        public static SoftwareVersion GetMinimumVersion(Achievement achievement)
        {
            var minimumVersion = achievement.Trigger.MinimumVersion();

            if (achievement.Type != AchievementType.Standard)
                minimumVersion = minimumVersion.OrNewer(Data.Version._1_3);

            return minimumVersion;
        }

        /// <summary>
        /// Creates a serialized requirements string from the core and alt groups of a provided <see cref="Achievement"/>.
        /// </summary>
        public static string SerializeRequirements(Achievement achievement, SerializationContext serializationContext)
        {
            return achievement.Trigger.Serialize(serializationContext);
        }

        /// <summary>
        /// Determines if two achievements have the same requirements.
        /// </summary>
        /// <returns><c>true</c> if the requirements match, <c>false</c> if not.</returns>
        public static bool AreRequirementsSame(Achievement left, Achievement right)
        {
            var builder1 = new AchievementBuilder(left);
            builder1.Optimize();
            var builder2 = new AchievementBuilder(right);
            builder2.Optimize();

            return builder1.AreRequirementsSame(builder2);
        }
    }
}
