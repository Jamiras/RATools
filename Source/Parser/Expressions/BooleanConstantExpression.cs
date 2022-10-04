using Jamiras.Components;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Parser.Expressions
{
    internal class BooleanConstantExpression : ExpressionBase, IMathematicCombineExpression, IComparisonNormalizeExpression
    {
        public BooleanConstantExpression(bool value, int line, int column)
            : this(value)
        {
            Location = new TextRange(line, column, line, column + (value ? 4 : 5));
        }

        public BooleanConstantExpression(bool value)
            : base(ExpressionType.BooleanConstant)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public bool Value { get; private set; }

        /// <summary>
        /// Gets whether this is non-changing.
        /// </summary>
        public override bool IsConstant
        {
            get { return true; }
        }

        /// <summary>
        /// Gets whether this is a compile-time constant.
        /// </summary>
        public override bool IsLiteralConstant
        {
            get { return true; }
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(Value ? "true" : "false");
        }

        /// <summary>
        /// Determines whether the specified <see cref="BooleanConstantExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="BooleanConstantExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="BooleanConstantExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as BooleanConstantExpression;
            return that != null && Value == that.Value;
        }

        public override bool? IsTrue(InterpreterScope scope, out ErrorExpression error)
        {
            error = null;
            return Value;
        }

        /// <summary>
        /// Combines the current expression with the <paramref name="right"/> expression using the <paramref name="operation"/> operator.
        /// </summary>
        /// <param name="right">The expression to combine with the current expression.</param>
        /// <param name="operation">How to combine the expressions.</param>
        /// <returns>
        /// An expression representing the combined values on success, or <c>null</c> if the expressions could not be combined.
        /// </returns>
        public ExpressionBase Combine(ExpressionBase right, MathematicOperation operation)
        {
            var stringExpression = right as StringConstantExpression;
            if (stringExpression != null)
            {
                var builder = new StringBuilder();
                AppendString(builder);
                builder.Append(stringExpression.Value);
                return new StringConstantExpression(builder.ToString());
            }

            return null;
        }

        /// <summary>
        /// Normalizes the comparison between the current expression and the <paramref name="right"/> expression using the <paramref name="operation"/> operator.
        /// </summary>
        /// <param name="right">The expression to compare with the current expression.</param>
        /// <param name="operation">How to compare the expressions.</param>
        /// <returns>
        /// An expression representing the normalized comparison, or <c>null</c> if normalization did not occur.
        /// </returns>
        public ExpressionBase NormalizeComparison(ExpressionBase right, ComparisonOperation operation)
        {
            var booleanRight = right as BooleanConstantExpression;
            if (booleanRight != null)
            {
                switch (operation)
                {
                    case ComparisonOperation.Equal:
                        return new BooleanConstantExpression(Value == booleanRight.Value);
                    case ComparisonOperation.NotEqual:
                        return new BooleanConstantExpression(Value != booleanRight.Value);
                    default:
                        return new ErrorExpression("Cannot perform relative comparison on boolean values", this);
                }
            }

            // prefer constants on right side of comparison
            if (!right.IsLiteralConstant)
                return new ComparisonExpression(right, ComparisonExpression.ReverseComparisonOperation(operation), this);

            return null;
        }
    }
}
