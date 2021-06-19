using RATools.Data;
using RATools.Parser.Internal;
using System;

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
            CopyLocation(result);
            return true;
        }

        public override bool BuildMacro(RichPresenceDisplayFunction.RichPresenceDisplayContext context, InterpreterScope scope, out ExpressionBase result)
        {
            var name = GetStringParameter(scope, "name", out result);
            if (name == null)
                return false;

            var format = GetStringParameter(scope, "format", out result);
            if (format == null)
                return false;

            var expression = GetParameter(scope, "expression", out result);
            if (expression == null)
                return false;

            var value = TriggerBuilderContext.GetValueString(expression, scope, out result);
            if (value == null)
                return false;

            var functionCall = scope.GetContext<FunctionCallExpression>();
            var valueFormat = Leaderboard.ParseFormat(format.Value);
            context.RichPresence.AddValueField(functionCall, name.Value, valueFormat);

            result = new StringConstantExpression(String.Format("@{0}({1})", name.Value, value));
            return true;
        }
    }
}
