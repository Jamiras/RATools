using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RATools.Data
{
    [DebuggerDisplay("{Title} ({Points})")]
    public class Achievement
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

        public bool IsDifferentThanPublished { get; internal set; }
        public bool IsDifferentThanLocal { get; internal set; }

        public IEnumerable<Requirement> CoreRequirements { get; internal set; }
        public IEnumerable<IEnumerable<Requirement>> AlternateRequirements { get; internal set; }

        internal void ParseRequirements(string requirementsString)
        {
            throw new NotImplementedException();
        }

        public bool AreRequirementsSame(Achievement achievement)
        {
            if (!AreRequirementsSame(CoreRequirements, achievement.CoreRequirements))
                return false;

            var enum1 = AlternateRequirements.GetEnumerator();
            var enum2 = achievement.AlternateRequirements.GetEnumerator();
            while (!enum1.MoveNext())
            {
                if (!enum2.MoveNext())
                    return false;

                if (!AreRequirementsSame(enum1.Current, enum2.Current))
                    return false;
            }

            return !enum2.MoveNext();
        }

        private static bool AreRequirementsSame(IEnumerable<Requirement> left, IEnumerable<Requirement> right)
        {
            var rightRequirements = new List<Requirement>(right);
            var enumerator = left.GetEnumerator();
            while (enumerator.MoveNext())
            {
                int index = rightRequirements.IndexOf(enumerator.Current);
                if (index == -1)
                    return false;

                rightRequirements.RemoveAt(index);
                if (rightRequirements.Count == 0)
                    return !enumerator.MoveNext();
            }

            return rightRequirements.Count == 0;
        }
    }
}
