using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RATools.Data;

namespace RATools.Parser.Internal
{
    [DebuggerDisplay("{Title} Core:{_core.Count} Alts:{_alts.Count}")]
    public class AchievementBuilder
    {
        public AchievementBuilder()
        {
            Current = _core = new List<Requirement>();
            _alts = new List<List<Requirement>>();
        }

        public string Title { get; set; }
        public string Description { get; set; }
        public int Points { get; set; }

        private List<Requirement> _core;
        private List<List<Requirement>> _alts;

        internal List<Requirement> Current { get; set; }

        internal bool IsInNot { get; set; }
        internal int EqualityModifier { get; set; }

        public void BeginAlt()
        {
            if (Current == _core || Current.Count > 0)
            {
                var newAlt = new List<Requirement>();
                Current = newAlt;
                _alts.Add(newAlt);
            }
        }

        public Requirement LastRequirement
        {
            get { return Current.Last(); }
        }

        public Achievement ToAchievement()
        {
            var achievement = new Achievement { Title = Title, Description = Description, Points = Points, CoreRequirements = _core.ToArray() };
            var alts = new Requirement[_alts.Count][];
            for (int i = 0; i < _alts.Count; i++)
                alts[i] = _alts[i].ToArray();
            achievement.AlternateRequirements = alts;
            return achievement;
        }

        private void NormalizeComparisons(List<Requirement> requirements)
        {
            var alwaysTrue = new List<Requirement>();

            foreach (var requirement in requirements)
            {
                switch (requirement.Left.Size)
                {
                    case FieldSize.Bit0:
                    case FieldSize.Bit1:
                    case FieldSize.Bit2:
                    case FieldSize.Bit3:
                    case FieldSize.Bit4:
                    case FieldSize.Bit5:
                    case FieldSize.Bit6:
                    case FieldSize.Bit7:
                        if (requirement.Right.Type == FieldType.Value)
                        {
                            if (requirement.Right.Value == 0)
                            {
                                switch (requirement.Operator)
                                {
                                    case RequirementOperator.NotEqual: // bit != 0 -> bit = 1
                                    case RequirementOperator.GreaterThan: // bit > 0 -> bit = 1
                                        requirement.Operator = RequirementOperator.Equal;
                                        requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.Value, Value = 1 };
                                        break;

                                    case RequirementOperator.GreaterThanOrEqual: // bit >= 0 -> always true
                                        alwaysTrue.Add(requirement);
                                        break;
                                }
                            }
                            else // value is non-zero
                            {
                                if (requirement.Right.Value != 1)
                                    requirement.Right = new Field { Size = requirement.Left.Size, Type = FieldType.Value, Value = 1 };

                                switch (requirement.Operator)
                                {
                                    case RequirementOperator.NotEqual: // bit != 1 -> bit = 0
                                    case RequirementOperator.LessThan: // bit < 1 -> bit = 0
                                        requirement.Operator = RequirementOperator.Equal;
                                        requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.Value, Value = 0 };
                                        break;

                                    case RequirementOperator.LessThanOrEqual: // bit <= 1 -> always true
                                        alwaysTrue.Add(requirement);
                                        break;
                                }
                            }
                        }

                        break;
                }
            }

            foreach (var requirement in alwaysTrue)
                requirements.Remove(requirement);
        }

        private void PromoteCommonAltsToCore()
        {
            var requirementsFoundInAll = new List<Requirement>();
            foreach (var requirement in _alts[0])
            {
                bool foundInAll = true;
                for (int i = 1; i < _alts.Count; i++)
                {
                    if (!_alts[i].Any(a => a == requirement))
                    {
                        foundInAll = false;
                        break;
                    }
                }

                if (foundInAll)
                    requirementsFoundInAll.Add(requirement);
            }

            foreach (var requirement in requirementsFoundInAll)
            {
                foreach (var alt in _alts)
                {
                    for (int i = alt.Count - 1; i >= 0; i--)
                    {
                        if (alt[i] == requirement)
                            alt.RemoveAt(i);
                    }
                }

                _core.Add(requirement);
            }
        }

        private void RemoveDuplicates(List<Requirement> requirements)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                for (int j = requirements.Count - 1; j > i; j--)
                {
                    if (requirements[j] == requirements[i])
                        requirements.RemoveAt(j);
                }
            }
        }

        private void RemoveRedundancies(List<Requirement> requirements)
        {
            for (int i = requirements.Count - 1; i >= 0; i--)
            {
                if (_core.Any(r => r == requirements[i]))
                    requirements.RemoveAt(i);
            }
        }

        public void Optimize()
        {
            // normalize BitX() methods to compare against 1
            NormalizeComparisons(_core);
            foreach (var alt in _alts)
                NormalizeComparisons(alt);

            // remove duplicates
            RemoveDuplicates(_core);
            foreach (var alt in _alts)
            {
                RemoveDuplicates(alt);
                RemoveRedundancies(alt);
            }

            // identify any item common to all alts and promote it to core
            if (_alts.Count > 1)
                PromoteCommonAltsToCore();
        }
    }
}
