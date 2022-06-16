using RATools.Parser.Internal;
using System.Text;

namespace RATools.Parser.Expressions
{
    internal class FloatConstantExpression : ExpressionBase
    {
        public FloatConstantExpression(float value)
            : base(ExpressionType.FloatConstant)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public float Value { get; private set; }

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
            builder.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0#####}", Value);
        }

        /// <summary>
        /// Determines whether the specified <see cref="FloatConstantExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="FloatConstantExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="FloatConstantExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as IntegerConstantExpression;
            return that != null && Value == that.Value;
        }

        /// <summary>
        /// Attempts to convert an expression to a <see cref="FloatConstantExpression"/>.
        /// </summary>
        /// <param name="expression">The expression to convert.</param>
        /// <returns>The converted expression, or a <see cref="ErrorExpression"/> if the expression could not be converted.</returns>
        public static ExpressionBase ConvertFrom(ExpressionBase expression)
        {
            FloatConstantExpression floatExpression;

            switch (expression.Type)
            {
                case ExpressionType.FloatConstant:
                    return expression;

                case ExpressionType.IntegerConstant:
                    floatExpression = new FloatConstantExpression(((IntegerConstantExpression)expression).Value);
                    break;

                default:
                    return new ErrorExpression("Cannot convert to float", expression);
            }

            expression.CopyLocation(floatExpression);
            return floatExpression;
        }
    }
}
