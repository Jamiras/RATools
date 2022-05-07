using System.Text;

namespace RATools.Parser.Internal
{
    internal class IntegerConstantExpression : ExpressionBase
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
            return (that != null && Value == that.Value);
        }
    }
}
