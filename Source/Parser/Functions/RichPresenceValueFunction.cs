using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using static RATools.Parser.Functions.RichPresenceDisplayFunction;

namespace RATools.Parser.Functions
{
    internal class RichPresenceValueFunction : FunctionDefinitionExpression
    {
        public RichPresenceValueFunction()
            : base("rich_presence_value")
        {
            Parameters.Add(new VariableDefinitionExpression("name"));
            Parameters.Add(new VariableDefinitionExpression("expression"));
            Parameters.Add(new VariableDefinitionExpression("format"));

            DefaultParameters["format"] = new StringConstantExpression("value");
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var name = GetStringParameter(scope, "name", out result);
            if (name == null)
                return false;

            var format = GetStringParameter(scope, "format", out result);
            if (format == null)
                return false;

            var valueFormat = RichPresenceValueExpression.ParseFormat(format.Value);
            if (valueFormat == ValueFormat.None)
            {
                result = new ErrorExpression(format.Value + " is not a supported rich_presence_value format", format);
                return false;
            }

            var expression = GetParameter(scope, "expression", out result);
            if (expression == null)
                return false;

            if (!ValueBuilder.IsConvertible(expression))
            {
                result = ValueBuilder.InconvertibleError(expression);
                return false;
            }

            result = new RichPresenceValueExpression(name, expression) { Format = valueFormat };
            CopyLocation(result);
            result.MakeReadOnly();
            return true;
        }

        public override bool Invoke(InterpreterScope scope, out ExpressionBase result)
        {
            var functionCall = scope.GetContext<FunctionCallExpression>();
            result = new ErrorExpression(Name.Name + " has no meaning outside of a rich_presence_display call", functionCall.FunctionName);
            return false;
        }
    }
}
