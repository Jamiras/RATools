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

        public override bool Equals(object obj)
        {
            var that = obj as IntegerConstantExpression;
            if (that == null)
                return false;

            return (this.Value == that.Value);
        }

        public override int GetHashCode()
        {
            return Value;
        }
    }
}
