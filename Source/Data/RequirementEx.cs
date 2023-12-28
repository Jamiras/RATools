using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Data
{
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

        private static void AppendFieldModifier(StringBuilder builder, Requirement requirement, NumberFormat numberFormat)
        {
            switch (requirement.Operator)
            {
                case RequirementOperator.Multiply:
                    builder.Append(" * ");
                    requirement.Right.AppendString(builder, numberFormat);
                    break;

                case RequirementOperator.Divide:
                    builder.Append(" / ");
                    requirement.Right.AppendString(builder, numberFormat);
                    break;

                case RequirementOperator.BitwiseAnd:
                    builder.Append(" & ");
                    requirement.Right.AppendString(builder, NumberFormat.Hexadecimal);
                    break;
            }
        }

        private static void AppendField(StringBuilder builder, Requirement requirement, NumberFormat numberFormat, StringBuilder addAddress)
        {
            if (addAddress.Length > 0)
            {
                var addAddressString = addAddress.ToString();
                addAddress.Clear();

                if (!ReferenceEquals(addAddress, builder))
                    builder.Append('(');

                requirement.Left.AppendString(builder, numberFormat, addAddressString);
                AppendFieldModifier(builder, requirement, numberFormat);

                if (!ReferenceEquals(addAddress, builder))
                    builder.Append(')');
            }
            else
            {
                requirement.Left.AppendString(builder, numberFormat);
                AppendFieldModifier(builder, requirement, numberFormat);
            }
        }

        private static void AppendAndOrNext(StringBuilder andNext, Requirement requirement, NumberFormat numberFormat,
            StringBuilder addSources, StringBuilder subSources, StringBuilder addAddress, ref Requirement lastAndNext)
        {
            if (addSources.Length > 0 || subSources.Length > 0 || addAddress.Length > 0)
            {
                andNext.Append('(');
                requirement.AppendString(andNext, numberFormat,
                    addSources.Length > 0 ? addSources.ToString() : null,
                    subSources.Length > 0 ? subSources.ToString() : null,
                    null,
                    null,
                    addAddress.Length > 0 ? addAddress.ToString() : null);
                andNext.Append(')');

                addAddress.Clear();
                addSources.Clear();
                subSources.Clear();
            }
            else if (requirement.HitCount > 0 && lastAndNext != null && lastAndNext.HitCount == 0)
            {
                var andNextString = andNext.ToString();
                andNext.Clear();
                requirement.AppendString(andNext, numberFormat, null, null, null, andNextString);
            }
            else
            {
                requirement.AppendString(andNext, numberFormat);
            }

            if (lastAndNext != null && lastAndNext.Type != requirement.Type)
            {
                andNext.Insert(0, '(');
                andNext.Append(')');
            }

            lastAndNext = requirement;

            if (requirement.Type == RequirementType.OrNext)
                andNext.Append(" || ");
            else
                andNext.Append(" && ");
        }

        private static void AppendModifyHits(StringBuilder builder, Requirement requirement, NumberFormat numberFormat,
            StringBuilder addSources, StringBuilder subSources, StringBuilder addAddress, StringBuilder andNext,
            StringBuilder resetNextIf)
        {
            if (addSources.Length > 0 || subSources.Length > 0 || addAddress.Length > 0 || andNext.Length > 0 || resetNextIf.Length > 0)
            {
                string resetNextIfString = resetNextIf.Length > 0 ? resetNextIf.ToString() : null;
                resetNextIf.Clear();

                requirement.AppendString(builder, numberFormat,
                    addSources.Length > 0 ? addSources.ToString() : null,
                    subSources.Length > 0 ? subSources.ToString() : null,
                    null,
                    andNext.Length > 0 ? andNext.ToString() : null,
                    addAddress.Length > 0 ? addAddress.ToString() : null,
                    null,
                    resetNextIfString);

                addAddress.Clear();
                addSources.Clear();
                subSources.Clear();
                andNext.Clear();
            }
            else
            {
                requirement.AppendString(builder, numberFormat);
            }
        }

        private static void AppendTally(StringBuilder builder, IEnumerable<Requirement> requirements,
            NumberFormat numberFormat, ref int width, int wrapWidth, int indent, string measuredIf)
        {
            // find the last subclause
            var addHitsRequirements = new List<Requirement>();
            foreach (var requirement in requirements)
            {
                if (requirement.Type == RequirementType.AddHits || requirement.Type == RequirementType.SubHits)
                    addHitsRequirements.Clear();
                else
                    addHitsRequirements.Add(requirement);
            }

            // the final clause will get generated as a "repeated" because we've ignored the AddHits subclauses
            var repeated = new StringBuilder();
            AppendString(repeated, addHitsRequirements, numberFormat, ref width, wrapWidth, indent, measuredIf);

            var repeatedString = repeated.ToString();
            var repeatedIndex = repeatedString.IndexOf("repeated(");
            var onceIndex = repeatedString.IndexOf("once(");
            var index = 0;
            if (repeatedIndex >= 0 && (onceIndex == -1 || repeatedIndex < onceIndex))
            {
                // replace the "repeated(" with "tally("
                builder.Append(repeatedString, 0, repeatedIndex);
                builder.Append("tally(");

                repeatedIndex += 9;
                while (Char.IsDigit(repeatedString[repeatedIndex]))
                    builder.Append(repeatedString[repeatedIndex++]);
                builder.Append(", ");

                index = repeatedIndex + 2;
            }
            else if (onceIndex >= 0)
            {
                // replace the "once(" with "tally(1, "
                builder.Append(repeatedString, 0, onceIndex);
                builder.Append("tally(1, ");

                index = onceIndex + 5;
            }

            // append the AddHits subclauses
            addHitsRequirements.Clear();
            indent += 4;

            foreach (var requirement in requirements)
            {
                if (requirement.Type == RequirementType.AddHits || requirement.Type == RequirementType.SubHits)
                {
                    // create a copy of the AddHits requirement without the Type
                    addHitsRequirements.Add(new Requirement
                    {
                        Left = requirement.Left,
                        Operator = requirement.Operator,
                        Right = requirement.Right,
                        HitCount = requirement.HitCount
                    });

                    if (wrapWidth != Int32.MaxValue)
                    {
                        builder.AppendLine();
                        builder.Append(' ', indent);
                    }

                    if (requirement.Type == RequirementType.AddHits)
                    {
                        bool hasAlwaysFalse = false;
                        if (addHitsRequirements.Count(r => !r.IsScalable) > 1 &&
                            addHitsRequirements.All(r => r.HitCount != 0 || r.IsScalable))
                        {
                            addHitsRequirements.Last().Type = RequirementType.OrNext;
                            addHitsRequirements.Add(Requirement.CreateAlwaysFalseRequirement());
                            hasAlwaysFalse = true;
                        }

                        int subclauseWidth = wrapWidth - indent - 4;
                        AppendString(builder, addHitsRequirements, numberFormat, ref subclauseWidth, wrapWidth, indent, null);

                        if (hasAlwaysFalse)
                            builder.Replace(" || always_false()", "");
                    }
                    else
                    {
                        builder.Append("deduct(");
                        int subclauseWidth = wrapWidth - indent - 4 - 8;
                        AppendString(builder, addHitsRequirements, numberFormat, ref subclauseWidth, wrapWidth, indent, null);
                        builder.Append(')');
                    }
                    builder.Append(", ");

                    addHitsRequirements.Clear();
                }
                else
                {
                    addHitsRequirements.Add(requirement);
                }
            }

            // always_false() final clause separates tally target from individual condition targets
            // it can be safely removed. any other final clause must be preserved
            var remaining = repeatedString.Substring(index);
            if (remaining.StartsWith("always_false()"))
            {
                builder.Length -= 2; // remove ", "
                remaining = remaining.Substring(14);
            }

            if (wrapWidth != Int32.MaxValue)
            {
                indent -= 4;
                builder.AppendLine();
                builder.Append(' ', indent);
            }

            builder.Append(remaining);
            width = wrapWidth - indent - remaining.Length;
        }

        private static void AppendString(StringBuilder builder, IEnumerable<Requirement> requirements, 
            NumberFormat numberFormat, ref int width, int wrapWidth, int indent, string measuredIf)
        {
            // special handling for tally
            if (requirements.Last().HitCount > 0 && requirements.Any(r => r.Type == RequirementType.AddHits))
            {
                AppendTally(builder, requirements, numberFormat, ref width, wrapWidth, indent, measuredIf);
                return;
            }

            var addSources = new StringBuilder();
            var subSources = new StringBuilder();
            var addHits = new StringBuilder();
            var andNext = new StringBuilder();
            var addAddress = new StringBuilder();
            var resetNextIf = new StringBuilder();
            var definition = new StringBuilder();
            Requirement lastAndNext = null;

            foreach (var requirement in requirements)
            {
                // precedence is AddAddress
                //             > AddSource/SubSource
                //             > AndNext/OrNext
                //             > ResetNextIf
                //             > AddHits/SubHits
                //             > ResetIf/PauseIf/Measured/MeasuredIf/Trigger
                switch (requirement.Type)
                {
                    case RequirementType.AddAddress:
                        AppendField(addAddress, requirement, numberFormat, addAddress);
                        addAddress.Append(" + ");
                        break;

                    case RequirementType.AddSource:
                        AppendField(addSources, requirement, numberFormat, addAddress);
                        addSources.Append(" + ");
                        break;

                    case RequirementType.SubSource:
                        subSources.Append(" - ");
                        AppendField(subSources, requirement, numberFormat, addAddress);
                        break;

                    case RequirementType.AndNext:
                    case RequirementType.OrNext:
                        AppendAndOrNext(andNext, requirement, numberFormat, addSources, subSources, addAddress, ref lastAndNext);
                        break;

                    case RequirementType.AddHits:
                    case RequirementType.SubHits:
                        AppendModifyHits(addHits, requirement, numberFormat, addSources, subSources, addAddress, andNext, resetNextIf);
                        addHits.Append(", ");
                        break;

                    case RequirementType.ResetNextIf:
                        AppendModifyHits(resetNextIf, requirement, numberFormat, addSources, subSources, addAddress, andNext, resetNextIf);

                        // remove "resetnext_if(" and ")" - they'll get converted to "never()" when resetNextIf is used
                        resetNextIf.Length--;
                        resetNextIf.Remove(0, 13);

                        andNext.Clear();
                        lastAndNext = null;
                        break;

                    default:
                        if (definition.Length > 0)
                            definition.Append(" && ");

                        requirement.AppendString(definition, numberFormat,
                            addSources.Length > 0 ? addSources.ToString() : null,
                            subSources.Length > 0 ? subSources.ToString() : null,
                            addHits.Length > 0 ? addHits.ToString() : null,
                            andNext.Length > 0 ? andNext.ToString() : null,
                            addAddress.Length > 0 ? addAddress.ToString() : null,
                            requirement.IsMeasured ? measuredIf : null,
                            resetNextIf.Length > 0 ? resetNextIf.ToString() : null);

                        addSources.Clear();
                        subSources.Clear();
                        addHits.Clear();
                        andNext.Clear();
                        addAddress.Clear();
                        resetNextIf.Clear();
                        lastAndNext = null;
                        break;
                }
            }

            while (definition.Length > width)
            {
                if (width > 0)
                {
                    var index = width;
                    while (index > 0 && definition[index] != ' ')
                        index--;

                    if (index == 0 && width >= wrapWidth - indent)
                    {
                        index = width;
                        while (index < definition.Length && definition[index] != ' ')
                            index++;
                    }

                    builder.Append(definition.ToString(), 0, index);
                    definition.Remove(0, index);
                }

                builder.AppendLine();
                builder.Append(' ', indent);
                width = wrapWidth - indent;
            }

            width -= definition.Length;
            builder.Append(definition.ToString());
        }

        public void AppendString(StringBuilder builder, NumberFormat numberFormat, ref int width, int wrapWidth, int indent, string measuredIf)
        {
            AppendString(builder, Requirements, numberFormat, ref width, wrapWidth, indent, measuredIf);
        }

        public void AppendString(StringBuilder builder, NumberFormat numberFormat)
        {
            int width = Int32.MaxValue;
            AppendString(builder, Requirements, numberFormat, ref width, Int32.MaxValue, 0, null);
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            AppendString(builder, NumberFormat.Hexadecimal);
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

                    bool redundantEvaluation = (combiningRequirement.Type == RequirementType.AndNext);
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
                else if (requirement != null)
                {
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

                combiningRequirement = (requirement != null && requirement.IsCombining) ? requirement : null;
            }

            return group;
        }
    }
}
