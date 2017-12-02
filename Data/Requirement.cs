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

            if (HitCount == 1)
                builder.Append("once(");
            else if (HitCount > 0)
                builder.AppendFormat("repeated({0}, ", HitCount);

            switch (Type)
            {
                case RequirementType.ResetIf:
                    builder.Append("never(");
                    break;

                case RequirementType.PauseIf:
                    builder.Append("unless(");
                    break;
            }

            builder.Append(Left.ToString());

            switch (Type)
            {
                case RequirementType.AddSource:
                    builder.Append(" + ");
                    break;
                case RequirementType.SubSource:
                    builder.Append(" - ");
                    break;
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
            }

            if (Operator != RequirementOperator.None)
                builder.Append(Right.ToString());

            switch (Type)
            {
                case RequirementType.ResetIf:
                case RequirementType.PauseIf:
                    builder.Append(')');
                    break;
            }

            if (HitCount != 0)
                builder.Append(')');

            return builder.ToString();
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
    }
}
