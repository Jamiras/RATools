using System.Text;

namespace RATools.Parser.Internal
{
    internal class AssignmentExpression : ExpressionBase
    {
        public AssignmentExpression(VariableExpression variable, ExpressionBase value)
            : base(ExpressionType.Assignment)
        {
            Variable = variable;
            Value = value;
        }

        public VariableExpression Variable { get; private set; }
        public ExpressionBase Value { get; private set; }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(Variable);
            builder.Append(" = ");
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

            result = new AssignmentExpression(Variable, value);
            return true;
        }
    }
}
