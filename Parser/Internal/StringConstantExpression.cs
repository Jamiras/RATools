using System.Text;

namespace RATools.Parser.Internal
{
    internal class StringConstantExpression : ExpressionBase
    {
        public StringConstantExpression(string value)
            : base(ExpressionType.StringConstant)
        {
            Value = value;
        }

        public string Value { get; private set; }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append('"');
            builder.Append(Value);
            builder.Append('"');
        }
    }
}
