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

        public static readonly ModelProperty IsDifferentThanPublishedProperty = ModelProperty.Register(typeof(Achievement), "IsDifferentThanPublished", typeof(bool), false);
        public bool IsDifferentThanPublished 
        {
            get { return (bool)GetValue(IsDifferentThanPublishedProperty); }
            internal set { SetValue(IsDifferentThanPublishedProperty, value); }
        }

        public static readonly ModelProperty IsDifferentThanLocalProperty = ModelProperty.Register(typeof(Achievement), "IsDifferentThanLocal", typeof(bool), false);
        public bool IsDifferentThanLocal
        {
            get { return (bool)GetValue(IsDifferentThanLocalProperty); }
            internal set { SetValue(IsDifferentThanLocalProperty, value); }
        }

        public static readonly ModelProperty IsNotGeneratedProperty = ModelProperty.Register(typeof(Achievement), "IsNotGenerated", typeof(bool), false);
        public bool IsNotGenerated
        {
            get { return (bool)GetValue(IsNotGeneratedProperty); }
            internal set { SetValue(IsNotGeneratedProperty, value); }
        }

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
