using System;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class ConditionalExpression : LeftRightExpressionBase
    {
        public ConditionalExpression(ExpressionBase left, ConditionalOperation operation, ExpressionBase right)
            : base(left, right, ExpressionType.Conditional)
        {
            Operation = operation;
        }

        /// <summary>
        /// Gets the conditional operation.
        /// </summary>
        public ConditionalOperation Operation { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            if (Operation == ConditionalOperation.Not)
            {
                builder.Append('!');
                if (Right.Type == ExpressionType.Conditional || Right.Type == ExpressionType.Comparison)
                {
                    builder.Append('(');
                    Right.AppendString(builder);
                    builder.Append(')');
                }
                else
                {
                    Right.AppendString(builder);
                }
                return;
            }

            if (Left.IsLogicalUnit)
                builder.Append('(');
            Left.AppendString(builder);
            if (Left.IsLogicalUnit)
                builder.Append(')');
            builder.Append(' ');

            builder.Append(GetOperatorString(Operation));

            builder.Append(' ');
            if (Right.IsLogicalUnit)
                builder.Append('(');
            Right.AppendString(builder);
            if (Right.IsLogicalUnit)
                builder.Append(')');
        }

        internal static string GetOperatorString(ConditionalOperation operation)
        {
            switch (operation)
            {
                case ConditionalOperation.Not: return "!";
                case ConditionalOperation.And: return "&&";
                case ConditionalOperation.Or: return "||";
                default: return null;
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
            if (Operation == ConditionalOperation.Not)
            {
                var invertedExpression = Right;
                if (invertedExpression.Type == ExpressionType.FunctionCall)
                {
                    if (!Right.ReplaceVariables(scope, out invertedExpression))
                    {
                        result = invertedExpression;
                        return false;
                    }
                }

                invertedExpression = InvertExpression(invertedExpression);
                if (invertedExpression.Type == ExpressionType.ParseError)
                {
                    result = invertedExpression;
                    return false;
                }

                return invertedExpression.ReplaceVariables(scope, out result);
            }

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

            result = new ConditionalExpression(left, Operation, right);
            CopyLocation(result);
            return true;
        }

        private static ExpressionBase InvertExpression(ExpressionBase expression)
        {
            // logical inversion
            var condition = expression as ConditionalExpression;
            if (condition != null)
            {
                switch (condition.Operation)
                {
                    case ConditionalOperation.Not:
                        // !(!A) => A
                        return condition.Right;

                    case ConditionalOperation.And:
                        // !(A && B) => !A || !B
                        return new ConditionalExpression(
                            new ConditionalExpression(null, ConditionalOperation.Not, condition.Left),
                            ConditionalOperation.Or,
                            new ConditionalExpression(null, ConditionalOperation.Not, condition.Right));

                    case ConditionalOperation.Or:
                        // !(A || B) => !A && !B
                        return new ConditionalExpression(
                            new ConditionalExpression(null, ConditionalOperation.Not, condition.Left),
                            ConditionalOperation.And,
                            new ConditionalExpression(null, ConditionalOperation.Not, condition.Right));

                    default:
                        throw new NotImplementedException("Unsupported condition operation");
                }
            }

            // comparative inversion
            var comparison = expression as ComparisonExpression;
            if (comparison != null)
            {
                // !(A == B) => A != B, !(A < B) => A >= B, ...
                return new ComparisonExpression(
                    comparison.Left,
                    ComparisonExpression.GetOppositeComparisonOperation(comparison.Operation),
                    comparison.Right);
            }

            // special handling for built-in functions
            var function = expression as FunctionCallExpression;
            if (function != null)
            {
                if (function.FunctionName.Name == "always_true")
                    return new FunctionCallExpression("always_false", function.Parameters);

                if (function.FunctionName.Name == "always_false")
                    return new FunctionCallExpression("always_true", function.Parameters);
            }

            // unsupported inversion
            return new ParseErrorExpression("! operator cannot be applied to " + expression.Type, expression);
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
                // the tree will be built weighted to the right. AND has higher priority than OR, so if an
                // ungrouped AND is followed by an OR, shift them around so the AND will be evaluated first
                //
                //   A && B || C  ~>  (A && B) || C
                //
                //     &&                      ||
                //   A      ||           &&       C
                //        B    C       A    B
                if (Operation == ConditionalOperation.And)
                {
                    var conditionalRight = Right as ConditionalExpression;
                    if (conditionalRight != null && conditionalRight.Operation == ConditionalOperation.Or)
                    {
                        // enforce order of operations
                        return Rebalance(conditionalRight);
                    }
                }
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
            bool result = Left.IsTrue(scope, out error);
            if (error != null)
                return false;

            switch (Operation)
            {
                case ConditionalOperation.And:
                    if (result)
                        result = Right.IsTrue(scope, out error);
                    break;

                case ConditionalOperation.Or:
                    if (!result)
                        result = Right.IsTrue(scope, out error);
                    break;

                case ConditionalOperation.Not:
                    result = !result;
                    break;
            }

            return result;
        }

        /// <summary>
        /// Determines whether the specified <see cref="ConditionalExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="ConditionalExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="ConditionalExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as ConditionalExpression;
            return that != null && Operation == that.Operation && Left == that.Left && Right == that.Right;
        }
    }

    /// <summary>
    /// Specifies how the two sides of the <see cref="ConditionalExpression"/> should be compared.
    /// </summary>
    public enum ConditionalOperation
    {
        /// <summary>
        /// Unspecified.
        /// </summary>
        None = 0,

        /// <summary>
        /// Both sides must be true.
        /// </summary>
        And,

        /// <summary>
        /// Either side can be true.
        /// </summary>
        Or,

        /// <summary>
        /// Right is not true (Left is ignored)
        /// </summary>
        Not,
    }
}
