using System.Collections.Generic;
using System.Diagnostics;
using Jamiras.DataModels;
using RATools.Parser.Internal;

namespace RATools.Data
{
    [DebuggerDisplay("{Title} ({Points})")]
    public class Achievement : ModelBase
    {
        internal Achievement()
        {
            CoreRequirements = new Requirement[0];
            AlternateRequirements = new IEnumerable<Requirement>[0];
        }

        public string Title { get; internal set; }
        public string Description { get; internal set; }
        public int Points { get; internal set; }

        public int Id { get; internal set; }
        public string BadgeName { get; internal set; }

        public IEnumerable<Requirement> CoreRequirements { get; internal set; }
        public IEnumerable<IEnumerable<Requirement>> AlternateRequirements { get; internal set; }

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
