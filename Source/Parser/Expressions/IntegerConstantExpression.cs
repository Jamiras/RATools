﻿using RATools.Parser.Internal;
using System.Text;

namespace RATools.Parser.Expressions
{
    internal class IntegerConstantExpression : ExpressionBase, IMathematicCombineOperation
    {
        public IntegerConstantExpression(int value)
            : base(ExpressionType.IntegerConstant)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public int Value { get; private set; }

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
            builder.Append(Value);
        }

        /// <summary>
        /// Determines whether the specified <see cref="IntegerConstantExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="IntegerConstantExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="IntegerConstantExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as IntegerConstantExpression;
            return that != null && Value == that.Value;
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
            var integerExpression = right as IntegerConstantExpression;
            if (integerExpression != null)
            {
                switch (operation)
                {
                    case MathematicOperation.Add:
                        return new IntegerConstantExpression(Value + integerExpression.Value);

                    case MathematicOperation.Subtract:
                        return new IntegerConstantExpression(Value - integerExpression.Value);

                    case MathematicOperation.Multiply:
                        return new IntegerConstantExpression(Value * integerExpression.Value);

                    case MathematicOperation.Divide:
                        if (integerExpression.Value == 0)
                            return new ErrorExpression("Division by zero");
                        return new IntegerConstantExpression(Value / integerExpression.Value);

                    case MathematicOperation.Modulus:
                        if (integerExpression.Value == 0)
                            return new ErrorExpression("Division by zero");
                        return new IntegerConstantExpression(Value % integerExpression.Value);

                    case MathematicOperation.BitwiseAnd:
                        return new IntegerConstantExpression(Value & integerExpression.Value);

                    default:
                        break;
                }
            }

            if (right is FloatConstantExpression)
            {
                var floatLeft = new FloatConstantExpression((float)Value) { Location = this.Location };
                return floatLeft.Combine(right, operation);
            }

            if (right is StringConstantExpression)
            {
                var stringLeft = new StringConstantExpression(Value.ToString()) { Location = this.Location };
                return stringLeft.Combine(right, operation);
            }

            return null;
        }
    }
}
