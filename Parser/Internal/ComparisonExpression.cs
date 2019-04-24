using System;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class ComparisonExpression : LeftRightExpressionBase
    {
        public ComparisonExpression(ExpressionBase left, ComparisonOperation operation, ExpressionBase right)
            : base(left, right, ExpressionType.Comparison)
        {
            Operation = operation;
        }

        /// <summary>
        /// Gets the comparison operation.
        /// </summary>
        public ComparisonOperation Operation { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            Left.AppendString(builder);
            builder.Append(' ');

            builder.Append(GetOperatorString(Operation));

            builder.Append(' ');
            Right.AppendString(builder);
        }

        internal static string GetOperatorString(ComparisonOperation operation)
        {
            switch (operation)
            {
                case ComparisonOperation.Equal: return "==";
                case ComparisonOperation.NotEqual: return "!=";
                case ComparisonOperation.LessThan: return "<";
                case ComparisonOperation.LessThanOrEqual: return "<=";
                case ComparisonOperation.GreaterThan: return ">";
                case ComparisonOperation.GreaterThanOrEqual: return ">=";
                default: return null;
            }
        }

        internal static ComparisonOperation GetOppositeComparisonOperation(ComparisonOperation op)
        {
            switch (op)
            {
                case ComparisonOperation.Equal: return ComparisonOperation.NotEqual;
                case ComparisonOperation.NotEqual: return ComparisonOperation.Equal;
                case ComparisonOperation.LessThan: return ComparisonOperation.GreaterThanOrEqual;
                case ComparisonOperation.LessThanOrEqual: return ComparisonOperation.GreaterThan;
                case ComparisonOperation.GreaterThan: return ComparisonOperation.LessThanOrEqual;
                case ComparisonOperation.GreaterThanOrEqual: return ComparisonOperation.LessThan;
                default: return ComparisonOperation.None;
            }
        }

        /// <summary>
        /// Replaces the variables in the expression with values from <paramref name="scope" />.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] The new expression containing the replaced variables.</param>
        /// <returns>
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ParseErrorExpression" />.
        /// </returns>
        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            ExpressionBase left;
            if (!Left.ReplaceVariables(scope, out left))
            {
                result = left;
                return false;
            }

            ExpressionBase right;
            if (!Right.ReplaceVariables(scope, out right))
            {
                result = right;
                return false;
            }

            var comparison = new ComparisonExpression(left, Operation, right);
            CopyLocation(comparison);
            result = comparison;
            return true;
        }

        /// <summary>
        /// Rebalances this expression based on the precendence of operators.
        /// </summary>
        /// <returns>
        /// Rebalanced expression
        /// </returns>
        internal override ExpressionBase Rebalance()
        {
            if (!Right.IsLogicalUnit)
            {
                var conditionalRight = Right as ConditionalExpression;
                if (conditionalRight != null)
                    return Rebalance(conditionalRight);
            }

            return base.Rebalance();
        }

        /// <summary>
        /// Determines whether the expression evaluates to true for the provided <paramref name="scope" />
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="error">[out] The error that prevented evaluation (or null if successful).</param>
        /// <returns>
        /// The result of evaluating the expression
        /// </returns>
        public override bool IsTrue(InterpreterScope scope, out ParseErrorExpression error)
        {
            ExpressionBase left, right;
            if (!Left.ReplaceVariables(scope, out left))
            {
                error = left as ParseErrorExpression;
                return false;
            }

            if (!Right.ReplaceVariables(scope, out right))
            {
                error = right as ParseErrorExpression;
                return false;
            }

            error = null;

            var integerLeft = left as IntegerConstantExpression;
            if (integerLeft != null)
            {
                var integerRight = right as IntegerConstantExpression;
                if (integerRight == null)
                    return false;

                switch (Operation)
                {
                    case ComparisonOperation.Equal:
                        return integerLeft.Value == integerRight.Value;
                    case ComparisonOperation.NotEqual:
                        return integerLeft.Value != integerRight.Value;
                    case ComparisonOperation.GreaterThan:
                        return integerLeft.Value > integerRight.Value;
                    case ComparisonOperation.GreaterThanOrEqual:
                        return integerLeft.Value >= integerRight.Value;
                    case ComparisonOperation.LessThan:
                        return integerLeft.Value < integerRight.Value;
                    case ComparisonOperation.LessThanOrEqual:
                        return integerLeft.Value <= integerRight.Value;
                    default:
                        return false;
                }
            }

            var stringLeft = left as StringConstantExpression;
            if (stringLeft != null)
            {
                var stringRight = right as StringConstantExpression;
                if (stringRight == null)
                    return false;

                switch (Operation)
                {
                    case ComparisonOperation.Equal:
                        return stringLeft.Value == stringRight.Value;
                    case ComparisonOperation.NotEqual:
                        return stringLeft.Value != stringRight.Value;
                    case ComparisonOperation.GreaterThan:
                        return String.Compare(stringLeft.Value, stringRight.Value) > 0;
                    case ComparisonOperation.GreaterThanOrEqual:
                        return String.Compare(stringLeft.Value, stringRight.Value) >= 0;
                    case ComparisonOperation.LessThan:
                        return String.Compare(stringLeft.Value, stringRight.Value) < 0;
                    case ComparisonOperation.LessThanOrEqual:
                        return String.Compare(stringLeft.Value, stringRight.Value) <= 0;
                    default:
                        return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the specified <see cref="ComparisonExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="ComparisonExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="ComparisonExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = (ComparisonExpression)obj;
            return Operation == that.Operation && Left == that.Left && Right == that.Right;
        }
    }

    /// <summary>
    /// Specifies how the two sides of the <see cref="ComparisonExpression"/> should be compared.
    /// </summary>
    public enum ComparisonOperation
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
}
