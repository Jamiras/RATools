using System.Text;

namespace RATools.Parser.Internal
{
    internal class VariableExpression : ExpressionBase
    {
        public VariableExpression(string name)
            : base(ExpressionType.Variable)
        {
            Name = name;
        }

        public string Name { get; private set; }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(Name);
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            ExpressionBase value = scope.GetVariable(Name);
            if (value == null)
            {
                var func = scope.GetFunction(Name);
                if (func != null)
                    result = new ParseErrorExpression("Function used like a variable: " + Name);
                else
                    result = new ParseErrorExpression("Unknown variable: " + Name);

                return false;
            }

            return value.ReplaceVariables(scope, out result);
        }
    }
}
