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

        public override bool Equals(object obj)
        {
            var that = obj as StringConstantExpression;
            if (that == null)
                return false;

            return (this.Value == that.Value);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}
