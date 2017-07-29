using System.Text;

namespace RATools.Parser.Internal
{
    internal class ReturnExpression : ExpressionBase
    {
        public ReturnExpression(ExpressionBase value)
            : base(ExpressionType.Return)
        {
            Value = value;
        }

        public ExpressionBase Value { get; private set; }

        internal override void AppendString(StringBuilder builder)
        {
            Value.AppendString(builder);
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            ExpressionBase value;
            if (!Value.ReplaceVariables(scope, out value))
            {
                result = value;
                return false;
            }

            result = new ReturnExpression(value);
            return true;
        }
    }
}
