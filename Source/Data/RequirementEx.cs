using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Data
{
    /// <summary>
    /// A collection of one or more <see cref="Requirement"/>s that build into a single
    /// logical statement - all requirements but the last will be one of the following:
    /// AddAddress, AddSource, SubSource, AddHits, SubHits, AndNext, OrNext.
    /// </summary>
    public class RequirementEx
    {
        public RequirementEx()
        {
            Requirements = new List<Requirement>();
        }

        public List<Requirement> Requirements { get; private set; }

        public RequirementType Type
        {
            get
            {
                if (Requirements.Count == 0)
                    return RequirementType.None;

                return Requirements.Last().Type;
            }
        }

        public bool IsMeasured
        {
            get
            {
                if (Requirements.Count == 0)
                    return false;

                return Requirements.Last().IsMeasured;
            }
        }

        public bool IsAffectedByPauseIf
        {
            get
            {
                if (Requirements.Count == 0)
                    return false;

                switch (Requirements.Last().Type)
                {
                    case RequirementType.ResetIf:
                        // ResetIf can't fire when paused
                        return true;

                    case RequirementType.Measured:
                    case RequirementType.MeasuredPercent:
                        // Measured values don't update while paused (even non-HitCount ones)
                        return true;

                    case RequirementType.PauseIf:
                        // a PauseIf is only affected by other PauseIfs if it has a HitCount
                        return Requirements.Last().HitCount > 0;

                    default:
                        // if any clause in the complex condition chain has a HitCount, pausing will stop it from incrementing
                        return HasHitCount;
                }
            }
        }

        public bool HasHitCount
        {
            get
            {
                foreach (var requirement in Requirements)
                {
                    if (requirement.HitCount > 0)
                        return true;
                }

                return false;
            }
        }

        public override string ToString()
        {
            if (Requirements.Count == 1)
                return Requirements[0].ToString();

            var builder = new StringBuilder();
            if (Type != RequirementType.None)
            {
                builder.Append(Type.ToString());
                builder.Append(' ');
            }

            builder.Append("Requirements: ");
            builder.Append(Requirements.Count);

            if (Requirements.Last().HitCount > 0)
                builder.AppendFormat(" ({0})", Requirements.Last().HitCount);

            return builder.ToString();
        }

        public bool? Evaluate()
        {
            if (Requirements.Count == 1)
                return Requirements[0].Evaluate();

            if (Requirements.Count == 0)
                return true;

            return null;
        }

        public bool LeftEquals(RequirementEx that)
        {
            if (Requirements.Count != that.Requirements.Count)
                return false;

            for (int i = 0; i < Requirements.Count; ++i)
            {
                var left = Requirements[i];
                var right = that.Requirements[i];
                if (left.Type != right.Type)
                    return false;

                if (left.Left != right.Left)
                    return false;

                if (i < Requirements.Count - 1 && left.IsCombining)
                {
                    if (left.Operator != right.Operator)
                        return false;

                    if (left.Right != right.Right)
                        return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            var that = obj as RequirementEx;
            if (ReferenceEquals(that, null))
                return false;

            if (that.Requirements.Count != Requirements.Count)
                return false;

            for (int i = 0; i < Requirements.Count; i++)
            {
                if (that.Requirements[i] != Requirements[i])
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(RequirementEx left, RequirementEx right)
        {
            if (ReferenceEquals(left, null))
                return ReferenceEquals(right, null);

            return left.Equals(right);
        }

        public static bool operator !=(RequirementEx left, RequirementEx right)
        {
            if (ReferenceEquals(left, null))
                return !ReferenceEquals(right, null);

            return !left.Equals(right);
        }

        /// <summary>
        /// Constructs a series of <see cref="RequirementEx" />s from a series of <see cref="Requirement" />s.
        /// </summary>
        public static List<RequirementEx> Combine(IEnumerable<Requirement> requirements)
        {
            var group = new List<RequirementEx>();

            Requirement combiningRequirement = null;
            foreach (var requirement in requirements)
            {
                if (requirement == null)
                    continue;

                if (combiningRequirement != null &&
                    (combiningRequirement.Type == RequirementType.AndNext ||
                     combiningRequirement.Type == RequirementType.OrNext))
                {
                    // "A || always_false()" and "A && always_true()" are both just "A",
                    // but we need to preserve the flag and hit target from the second condition.

                    // create a copy without the hit count for evaluation
                    var newRequirement = new Requirement
                    {
                        Left = requirement.Left,
                        Operator = requirement.Operator,
                        Right = requirement.Right
                    };

                    bool redundantEvaluation = combiningRequirement.Type == RequirementType.AndNext;
                    if (newRequirement.Evaluate() == redundantEvaluation)
                    {
                        if (combiningRequirement.HitCount != 0 && requirement.HitCount != 0)
                        {
                            // if both requirements have separate hit targets, we can't combine them
                        }
                        else
                        {
                            // going to be modifying the combiningRequirement, create a copy
                            newRequirement = new Requirement
                            {
                                // use the flag from the redundant condition
                                Type = requirement.Type,

                                Left = combiningRequirement.Left,
                                Operator = combiningRequirement.Operator,
                                Right = combiningRequirement.Right
                            };

                            // one of the two conditions has a hit count of zero, so this
                            // effectively takes whichever isn't zero.
                            newRequirement.HitCount = requirement.HitCount + combiningRequirement.HitCount;

                            // replace the last requirement with the updated requirement
                            var lastGroupRequirements = group.Last().Requirements;
                            lastGroupRequirements.Remove(combiningRequirement);
                            lastGroupRequirements.Add(newRequirement);

                            // decide if the condition is still combining
                            combiningRequirement = requirement.IsCombining ? newRequirement : null;

                            continue;
                        }
                    }
                }
                else if (combiningRequirement == null || !IsAccumulated(group.Last().Requirements))
                {
                    // if this requirement is not part of an accumulator chain (AddSource/SubSource),
                    // check to see if it can be discarded.
                    switch (requirement.Type)
                    {
                        case RequirementType.AddHits:
                        case RequirementType.SubHits:
                            // an always_false() condition will never generate a hit
                            if (requirement.Evaluate() == false)
                                continue;
                            break;

                        case RequirementType.AndNext:
                            // an always_true() condition will not affect the next condition
                            if (requirement.Evaluate() == true)
                                continue;
                            break;

                        case RequirementType.OrNext:
                            // an always_false() condition will not affect the next condition
                            if (requirement.Evaluate() == false)
                                continue;
                            break;
                    }
                }

                if (combiningRequirement == null)
                    group.Add(new RequirementEx());

                group.Last().Requirements.Add(requirement);

                combiningRequirement = requirement.IsCombining ? requirement : null;
            }

            return group;
        }

        private static bool IsAccumulated(List<Requirement> requirements)
        {
            for (int i = requirements.Count - 1; i >= 0; i--)
            {
                switch (requirements[i].Type)
                {
                    // AddAddress doesn't affect accumulator, ignore it.
                    case RequirementType.AddAddress:
                        continue;

                    // These affect accumulator.
                    case RequirementType.AddSource:
                    case RequirementType.SubSource:
                    case RequirementType.Remember:
                        return true;

                    // Everything else consumes and resets the accumulator.
                    default:
                        return false;
                }
            }

            return false;
        }
    }
}
