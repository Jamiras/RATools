using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class FunctionCallExpression : ExpressionBase
    {
        public FunctionCallExpression(string functionName, ICollection<ExpressionBase> parameters)
            : base(ExpressionType.FunctionCall)
        {
            FunctionName = functionName;
            Parameters = parameters;
        }

        public string FunctionName { get; private set; }
        public ICollection<ExpressionBase> Parameters { get; private set; }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(FunctionName);
            builder.Append('(');

            if (Parameters.Count > 0)
            {
                foreach (var parameter in Parameters)
                {
                    parameter.AppendString(builder);
                    builder.Append(", ");
                }
                builder.Length -= 2;
            }

            builder.Append(')');
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var parameters = new List<ExpressionBase>();
            foreach (var parameter in Parameters)
            {
                ExpressionBase value;
                if (!parameter.ReplaceVariables(scope, out value))
                {
                    result = value;
                    return false;
                }

                parameters.Add(value);
            }

            result = new FunctionCallExpression(FunctionName, parameters);
            return true;
        }
    }
}
