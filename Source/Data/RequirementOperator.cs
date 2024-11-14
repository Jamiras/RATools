namespace RATools.Data
{
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

        /// <summary>
        /// The right value is added to the left value. (combining conditions only)
        /// </summary>
        Add,

        /// <summary>
        /// The right value is subtracted from the left value. (combining conditions only)
        /// </summary>
        Subtract,

        /// <summary>
        /// The left value is multiplied by the right value. (combining conditions only)
        /// </summary>
        Multiply,

        /// <summary>
        /// The left value is divided by the right value. (combining conditions only)
        /// </summary>
        Divide,

        /// <summary>
        /// The left value is masked by the right value. (combining conditions only)
        /// </summary>
        BitwiseAnd,

        /// <summary>
        /// The bits in the left value are toggled by the bits in the right value. (combining conditions only)
        /// </summary>
        BitwiseXor,

        /// <summary>
        /// The left value is divided by the right value and the remainder is returned. (combining conditions only)
        /// </summary>
        Modulus,
    }

    public static class RequirementOperatorExtension
    {
        /// <summary>
        /// Gets the equivalent operator if the operands are switched.
        /// </summary>
        public static string ToOperatorString(this RequirementOperator op)
        {
            switch (op)
            {
                case RequirementOperator.Equal: return "==";
                case RequirementOperator.NotEqual: return "!=";
                case RequirementOperator.LessThan: return "<";
                case RequirementOperator.LessThanOrEqual: return "<=";
                case RequirementOperator.GreaterThan: return ">";
                case RequirementOperator.GreaterThanOrEqual: return ">=";
                case RequirementOperator.Multiply: return "*";
                case RequirementOperator.Add: return "+";
                case RequirementOperator.Subtract: return "-";
                case RequirementOperator.Divide: return "/";
                case RequirementOperator.Modulus: return "%";
                case RequirementOperator.BitwiseAnd: return "&";
                case RequirementOperator.BitwiseXor: return "^";
                default: return "";
            }
        }

        /// <summary>
        /// Gets the equivalent operator if the operands are switched.
        /// </summary>
        public static RequirementOperator ReverseOperator(this RequirementOperator op)
        {
            switch (op)
            {
                case RequirementOperator.Equal: return RequirementOperator.Equal;
                case RequirementOperator.NotEqual: return RequirementOperator.NotEqual;
                case RequirementOperator.LessThan: return RequirementOperator.GreaterThan;
                case RequirementOperator.LessThanOrEqual: return RequirementOperator.GreaterThanOrEqual;
                case RequirementOperator.GreaterThan: return RequirementOperator.LessThan;
                case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.LessThanOrEqual;
                default: return RequirementOperator.None;
            }
        }

        /// <summary>
        /// Gets the logically opposite operator.
        /// </summary>
        public static RequirementOperator OppositeOperator(this RequirementOperator op)
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

        /// <summary>
        /// Gets whether or not the operator is used to compare two operands.
        /// </summary>
        public static bool IsComparison(this RequirementOperator op)
        {
            switch (op)
            {
                case RequirementOperator.Equal:
                case RequirementOperator.NotEqual:
                case RequirementOperator.LessThan:
                case RequirementOperator.LessThanOrEqual:
                case RequirementOperator.GreaterThan:
                case RequirementOperator.GreaterThanOrEqual:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets whether or not the operator is used to modify an operand.
        /// </summary>
        public static bool IsModifier(this RequirementOperator op)
        {
            switch (op)
            {
                case RequirementOperator.Add:
                case RequirementOperator.Subtract:
                case RequirementOperator.Multiply:
                case RequirementOperator.Divide:
                case RequirementOperator.Modulus:
                case RequirementOperator.BitwiseAnd:
                case RequirementOperator.BitwiseXor:
                    return true;

                default:
                    return false;
            }
        }
    }
}
