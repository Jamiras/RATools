using System.Text;

namespace RATools.Data
{
    /// <summary>
    /// Defines a single requirement within an <see cref="Achievement"/>.
    /// </summary>
    public class Requirement
    {
        /// <summary>
        /// Gets or sets the left part of the requirement.
        /// </summary>
        public Field Left { get; set; }

        /// <summary>
        /// Gets or sets the right part of the requirement.
        /// </summary>
        public Field Right { get; set; }

        /// <summary>
        /// Gets or sets the requirement type.
        /// </summary>
        public RequirementType Type { get; set; }

        /// <summary>
        /// Gets or sets the requirement operator.
        /// </summary>
        public RequirementOperator Operator { get; set; }

        /// <summary>
        /// Gets or sets the required hit count for the requirement.
        /// </summary>
        /// <remarks>
        /// <c>0</c> means the requirement must be true at the time the achievement triggers.
        /// Any other value indicates the number of frames a requirement must be true before the achievement can trigger.
        /// </remarks>
        public ushort HitCount { get; set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            AppendString(builder, NumberFormat.Decimal);
            return builder.ToString();
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder"/>.
        /// </summary>
        internal void AppendString(StringBuilder builder, NumberFormat numberFormat, 
            string addSources = null, string subSources = null, string addHits = null, string andNext = null)
        {
            switch (Type)
            {
                case RequirementType.ResetIf:
                    builder.Append("never(");
                    AppendRepeatedCondition(builder, numberFormat, addSources, subSources, addHits, andNext);
                    builder.Append(')');
                    break;

                case RequirementType.PauseIf:
                    builder.Append("unless(");
                    AppendRepeatedCondition(builder, numberFormat, addSources, subSources, addHits, andNext);
                    builder.Append(')');
                    break;

                default:
                    AppendRepeatedCondition(builder, numberFormat, addSources, subSources, addHits, andNext);
                    break;
            }
        }

        private void AppendRepeatedCondition(StringBuilder builder, NumberFormat numberFormat,
            string addSources, string subSources, string addHits, string andNext)
        {
            if (HitCount == 0)
            {
                AppendCondition(builder, numberFormat, addSources, subSources, addHits, andNext);
            }
            else
            {
                if (HitCount == 1)
                    builder.Append("once(");
                else
                    builder.AppendFormat("repeated({0}, ", HitCount);

                AppendCondition(builder, numberFormat, addSources, subSources, addHits, andNext);

                builder.Append(')');
            }
        }

        internal void AppendCondition(StringBuilder builder, NumberFormat numberFormat, 
            string addSources = null, string subSources = null, string addHits = null, string andNext = null)
        {
            if (!string.IsNullOrEmpty(addSources))
            {
                builder.Append('(');
                builder.Append(addSources);
            }
            else if (!string.IsNullOrEmpty(subSources))
            {
                builder.Append('(');
            }
            else if (!string.IsNullOrEmpty(addHits))
            {
                builder.Append(addHits);
            }
            else if (!string.IsNullOrEmpty(andNext))
            {
                builder.Append(andNext);
            }

            switch (Type)
            {
                case RequirementType.AddSource:
                    Left.AppendString(builder, numberFormat);
                    builder.Append(" + ");
                    break;

                case RequirementType.SubSource:
                    builder.Append(" - ");
                    Left.AppendString(builder, numberFormat);
                    break;

                default:
                    Left.AppendString(builder, numberFormat);
                    break;
            }

            if (!string.IsNullOrEmpty(subSources))
            {
                builder.Append(subSources);
                builder.Append(')');
            }
            else if (!string.IsNullOrEmpty(addSources))
            {
                builder.Append(')');
            }

            switch (Operator)
            {
                case RequirementOperator.Equal:
                    builder.Append(" == ");
                    break;
                case RequirementOperator.NotEqual:
                    builder.Append(" != ");
                    break;
                case RequirementOperator.LessThan:
                    builder.Append(" < ");
                    break;
                case RequirementOperator.LessThanOrEqual:
                    builder.Append(" <= ");
                    break;
                case RequirementOperator.GreaterThan:
                    builder.Append(" > ");
                    break;
                case RequirementOperator.GreaterThanOrEqual:
                    builder.Append(" >= ");
                    break;

                case RequirementOperator.None:
                    return;
            }

            Right.AppendString(builder, numberFormat);
        }

        /// <summary>
        /// Determines if the requirement always evaluates true or false.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the requirement is always true, <c>false</c> if it's always false, or 
        /// <c>null</c> if the result cannot be determined at this time.
        /// </returns>
        public bool? Evaluate()
        {
            if (Left.Type != Right.Type)
                return null;

            bool result = false;

            if (Left.IsMemoryReference)
            {
                // memory reference - can only be equal or not equal to same memory reference
                if (Left.Value != Right.Value || Left.Size != Right.Size)
                    return null;

                // same memory reference in the same frame is always equal
                switch (Operator)
                {
                    case RequirementOperator.Equal:
                    case RequirementOperator.GreaterThanOrEqual:
                    case RequirementOperator.LessThanOrEqual:
                        result = true;
                        break;

                    default:
                        result = false;
                        break;
                }
            }
            else
            {
                // comparing constants
                switch (Operator)
                {
                    case RequirementOperator.Equal:
                        result = (Left.Value == Right.Value);
                        break;
                    case RequirementOperator.NotEqual:
                        result = (Left.Value != Right.Value);
                        break;
                    case RequirementOperator.LessThan:
                        result = (Left.Value < Right.Value);
                        break;
                    case RequirementOperator.LessThanOrEqual:
                        result = (Left.Value <= Right.Value);
                        break;
                    case RequirementOperator.GreaterThan:
                        result = (Left.Value > Right.Value);
                        break;
                    case RequirementOperator.GreaterThanOrEqual:
                        result = (Left.Value >= Right.Value);
                        break;
                    default:
                        result = false;
                        break;
                }
            }

            // even if the condition is always true, if there's a target hit count, it won't be true initially.
            if (result && HitCount > 1)
                return null;

            return result;
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            var that = obj as Requirement;
            if (ReferenceEquals(that, null))
                return false;

            if (that.Type != this.Type || that.Operator != this.Operator || that.HitCount != this.HitCount)
                return false;

            return (that.Left == this.Left && that.Right == this.Right);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Determines if two <see cref="Requirement"/>s are equivalent.
        /// </summary>
        public static bool operator ==(Requirement left, Requirement right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;

            return left.Equals(right);
        }

        /// <summary>
        /// Determines if two <see cref="Requirement"/>s are not equivalent.
        /// </summary>
        public static bool operator !=(Requirement left, Requirement right)
        {
            if (ReferenceEquals(left, right))
                return false;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return true;

            return !left.Equals(right);
        }

        /// <summary>
        /// Gets the logically opposing operator.
        /// </summary>
        public static RequirementOperator GetOpposingOperator(RequirementOperator op)
        {
            switch (op)
            {
                case RequirementOperator.Equal: return RequirementOperator.NotEqual;
                case RequirementOperator.NotEqual: return RequirementOperator.Equal;
                case RequirementOperator.LessThan: return RequirementOperator.GreaterThanOrEqual;
                case RequirementOperator.LessThanOrEqual: return RequirementOperator.GreaterThan;
                case RequirementOperator.GreaterThan: return RequirementOperator.LessThanOrEqual;
                case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.LessThan;
                default: return RequirementOperator.None;
            }
        }
    }

    /// <summary>
    /// Specifies how the <see cref="Requirement.Left"/> and <see cref="Requirement.Right"/> values should be compared.
    /// </summary>
    public enum RequirementOperator
    {
        /// <summary>
        /// Unspecified.
        /// </summary>
        None = 0,

        /// <summary>
        /// The left and right values are equivalent.
        /// </summary>
        Equal,

        /// <summary>
        /// The left and right values are not equivalent.
        /// </summary>
        NotEqual,

        /// <summary>
        /// The left value is less than the right value.
        /// </summary>
        LessThan,

        /// <summary>
        /// The left value is less than or equal to the right value.
        /// </summary>
        LessThanOrEqual,

        /// <summary>
        /// The left value is greater than the right value.
        /// </summary>
        GreaterThan,

        /// <summary>
        /// The left value is greater than or equal to the right value.
        /// </summary>
        GreaterThanOrEqual,
    }

    /// <summary>
    /// Special requirement behaviors
    /// </summary>
    public enum RequirementType
    {
        /// <summary>
        /// No special behavior.
        /// </summary>
        None = 0,

        /// <summary>
        /// Resets any HitCounts in the current requirement group if true.
        /// </summary>
        ResetIf,

        /// <summary>
        /// Pauses processing of the achievement if true.
        /// </summary>
        PauseIf,

        /// <summary>
        /// Adds the Left part of the requirement to the Left part of the next requirement.
        /// </summary>
        AddSource,

        /// <summary>
        /// Subtracts the Left part of the next requirement from the Left part of the requirement.
        /// </summary>
        SubSource,

        /// <summary>
        /// Adds the HitsCounts from this requirement to the next requirement.
        /// </summary>
        AddHits,

        /// <summary>
        /// This requirement must also be true for the next requirement to be true.
        /// </summary>
        AndNext,
    }
}
