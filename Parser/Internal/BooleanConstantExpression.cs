using Jamiras.Components;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class BooleanConstantExpression : ExpressionBase
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
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(Value ? "true" : "false");
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
            var that = obj as BooleanConstantExpression;
            return (that != null && Value == that.Value);
        }

        public override bool? IsTrue(InterpreterScope scope, out ParseErrorExpression error)
        {
            error = null;
            return Value;
        }
    }
}
