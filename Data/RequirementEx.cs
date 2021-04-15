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

                case RequirementOperator.LogicalAnd:
                    builder.Append(" & ");
                    requirement.Right.AppendString(builder, numberFormat);
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
            StringBuilder addSources, StringBuilder subSources, StringBuilder addAddress, ref RequirementType lastAndNext)
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
            else
            {
                requirement.AppendString(andNext, numberFormat);
            }

            if (lastAndNext != requirement.Type)
            {
                if (lastAndNext != RequirementType.None)
                {
                    andNext.Insert(0, '(');
                    andNext.Append(')');
                }

                lastAndNext = requirement.Type;
            }

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

        private static void AppendString(StringBuilder builder, IEnumerable<Requirement> requirements, 
            NumberFormat numberFormat, ref int width, int wrapWidth, int indent, string measuredIf)
        {
            var addSources = new StringBuilder();
            var subSources = new StringBuilder();
            var addHits = new StringBuilder();
            var andNext = new StringBuilder();
            var addAddress = new StringBuilder();
            var resetNextIf = new StringBuilder();
            var definition = new StringBuilder();
            RequirementType lastAndNext = RequirementType.None;

            foreach (var requirement in requirements)
            {
                // precedence is AddAddress
                //             > AddSource/SubSource
                //             > AndNext/OrNext
                //             > AddHits/ResetNextIf
                //             > ResetIf/PauseIf/Measured/MeasuredIf
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
                        AppendModifyHits(addHits, requirement, numberFormat, addSources, subSources, addAddress, andNext, resetNextIf);
                        addHits.Append(", ");
                        break;

                    case RequirementType.ResetNextIf:
                        AppendModifyHits(resetNextIf, requirement, numberFormat, addSources, subSources, addAddress, andNext, resetNextIf);

                        // remove "resetnext_if(" and ")" - they'll get converted to "never()" when resetNextIf is used
                        resetNextIf.Length--;
                        resetNextIf.Remove(0, 13);
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
                            (requirement.Type == RequirementType.Measured) ? measuredIf : null,
                            resetNextIf.Length > 0 ? resetNextIf.ToString() : null);

                        addSources.Clear();
                        subSources.Clear();
                        addHits.Clear();
                        andNext.Clear();
                        addAddress.Clear();
                        resetNextIf.Clear();
                        lastAndNext = RequirementType.None;
                        break;
                }
            }

            while (definition.Length > wrapWidth)
            {
                var index = width;
                while (index > 0 && definition[index] != ' ')
                    index--;

                builder.Append(definition.ToString(), 0, index);
                builder.AppendLine();
                builder.Append(' ', indent);
                definition.Remove(0, index);
                width = wrapWidth - indent;
            }

            if (width - definition.Length < 0)
            {
                builder.AppendLine();
                builder.Append(' ', indent);
                width = wrapWidth - indent;
            }
            else
            {
                width -= definition.Length;
            }

            builder.Append(definition.ToString());
        }

        internal void AppendString(StringBuilder builder, NumberFormat numberFormat, ref int width, int wrapWidth, int indent, string measuredIf)
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

            bool combiningRequirement = false;
            foreach (var requirement in requirements)
            {
                switch (requirement.Type)
                {
                    case RequirementType.AddHits:
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

                if (!combiningRequirement)
                    group.Add(new RequirementEx());

                group.Last().Requirements.Add(requirement);
                combiningRequirement = requirement.IsCombining;
            }

            return group;
        }
    }
}
