using RATools.Parser.Internal;
using System.Text;

namespace RATools.Parser.Expressions
{
    internal class StringConstantExpression : ExpressionBase, IMathematicCombineOperation
    {
        public StringConstantExpression(string value)
            : base(ExpressionType.StringConstant)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public string Value { get; private set; }

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
        /// Appends the textual representation of this expression to <paramref name="builder"/>.
        /// </summary>
        /// <remarks>Used for constructing a StringConstantExpression from smaller expressions.</remarks>
        internal override void AppendStringLiteral(StringBuilder builder)
        {
            builder.Append(Value);
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append('"');
            builder.Append(Value);
            builder.Append('"');
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            result = new StringConstantExpression(Value);
            CopyLocation(result);
            return true;
        }

        /// <summary>
        /// Determines whether the specified <see cref="StringConstantExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="StringConstantExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="StringConstantExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as StringConstantExpression;
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
            switch (right.Type)
            {
                case ExpressionType.StringConstant:
                    if (operation == MathematicOperation.Add)
                        return new StringConstantExpression(Value + ((StringConstantExpression)right).Value);
                    break;

                case ExpressionType.IntegerConstant:
                case ExpressionType.FloatConstant:
                case ExpressionType.BooleanConstant:
                    var builder = new StringBuilder();
                    builder.Append(Value);
                    right.AppendString(builder);
                    return new StringConstantExpression(builder.ToString());
            }

            return null;
        }
    }
}
