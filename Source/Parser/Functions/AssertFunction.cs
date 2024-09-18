using RATools.Parser.Expressions;
using System.Linq;
using System.Text;

namespace RATools.Parser.Functions
{
    internal class AssertFunction : FunctionDefinitionExpression
    {
        public AssertFunction()
            : base("assert")
        {
            Parameters.Add(new VariableDefinitionExpression("condition"));
            Parameters.Add(new VariableDefinitionExpression("message"));

            DefaultParameters["message"] = new StringConstantExpression("");
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var condition = GetBooleanParameter(scope, "condition", out result);
            if (condition == null)
                return false;

            if (condition.Value == false)
            {
                var message = GetStringParameter(scope, "message", out result);
                if (message == null)
                    return false;

                if (message.Value != "")
                {
                    result = new ErrorExpression("Assertion failed: " + message.Value, condition);
                }
                else
                {
                    var functionCall = scope.GetContext<FunctionCallExpression>();
                    if (functionCall != null && functionCall.FunctionName.Name == "assert")
                    {
                        var parameter = functionCall.Parameters.First();
                        var builder = new StringBuilder();
                        parameter.AppendString(builder);
                        result = new ErrorExpression("Assertion failed: " + builder.ToString(), parameter);
                    }
                    else
                    {
                        result = new ErrorExpression("Assertion failed", condition);
                    }
                }

                return false;
            }

            result = null;
            return true;
        }
    }
}
