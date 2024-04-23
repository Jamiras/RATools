﻿using Jamiras.Components;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class BooleanConstantExpression : LiteralConstantExpressionBase, 
        IMathematicCombineExpression, IComparisonNormalizeExpression
    {
        public BooleanConstantExpression(bool value, int line, int column)
            : this(value)
        {
            Location = new TextRange(line, column, line, column + (value ? 3 : 4));
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
        /// <param name="canModifyRight"><c>true</c> if <paramref name="right"/> can be changed, <c>false</c> if not.</param>
        /// <returns>
        /// An expression representing the normalized comparison, or <c>null</c> if normalization did not occur.
        /// </returns>
        public ExpressionBase NormalizeComparison(ExpressionBase right, ComparisonOperation operation, bool canModifyRight)
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
            if (right is not LiteralConstantExpressionBase)
                return new ComparisonExpression(right, ComparisonExpression.ReverseComparisonOperation(operation), this);

            return null;
        }
    }
}
