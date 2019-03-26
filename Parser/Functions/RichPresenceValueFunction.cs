using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class RichPresenceValueFunction : RichPresenceDisplayFunction.FunctionDefinition
    {
        public RichPresenceValueFunction()
            : base("rich_presence_value")
        {
            Parameters.Add(new VariableDefinitionExpression("name"));
            Parameters.Add(new VariableDefinitionExpression("expression"));
            Parameters.Add(new VariableDefinitionExpression("format"));

            DefaultParameters["format"] = new StringConstantExpression("value");
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var name = GetStringParameter(scope, "name", out result);
            if (name == null)
                return false;

            var format = GetStringParameter(scope, "format", out result);
            if (format == null)
                return false;

            var valueFormat = Leaderboard.ParseFormat(format.Value);
            if (valueFormat == ValueFormat.None)
            {
                result = new ParseErrorExpression(format.Value + " is not a supported rich_presence_value format", format);
                return false;
            }

            var expression = GetParameter(scope, "expression", out result);
            if (expression == null)
                return false;

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { name, expression, format });
            return true;
        }

        public override ParseErrorExpression BuildMacro(RichPresenceDisplayFunction.RichPresenceDisplayContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var name = (StringConstantExpression)functionCall.Parameters.First();
            var expression = functionCall.Parameters.ElementAt(1);
            var format = (StringConstantExpression)functionCall.Parameters.ElementAt(2);
            var valueFormat = Leaderboard.ParseFormat(format.Value);

            ExpressionBase result;
            var value = TriggerBuilderContext.GetValueString(expression, scope, out result);
            if (value == null)
                return (ParseErrorExpression)result;

            context.RichPresence.AddValueField(name.Value, valueFormat);

            context.DisplayString.Append('@');
            context.DisplayString.Append(name.Value);
            context.DisplayString.Append('(');
            context.DisplayString.Append(value);
            context.DisplayString.Append(')');
            return null;
        }
    }
}
