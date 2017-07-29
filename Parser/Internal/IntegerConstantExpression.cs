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

        public int Value { get; private set; }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(Value);
        }
    }
}
