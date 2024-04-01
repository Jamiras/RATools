using RATools.Data;
using RATools.Parser.Expressions;

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

        public static ValueFormat ParseFormat(string format)
        {
            var valueFormat = Leaderboard.ParseFormat(format);
            if (valueFormat == ValueFormat.None)
            {
                if (format == "ASCIICHAR")
                    valueFormat = ValueFormat.ASCIIChar;
                else if (format == "UNICODECHAR")
                    valueFormat = ValueFormat.UnicodeChar;
            }
            return valueFormat;
        }

        public static string GetFormatString(ValueFormat format)
        {
            switch (format)
            {
                case ValueFormat.ASCIIChar:
                    return "ASCIICHAR";

                case ValueFormat.UnicodeChar:
                    return "UNICODECHAR";

                default:
                    return Leaderboard.GetFormatString(format);
            }
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var name = GetStringParameter(scope, "name", out result);
            if (name == null)
                return false;

            var format = GetStringParameter(scope, "format", out result);
            if (format == null)
                return false;

            var valueFormat = ParseFormat(format.Value);
            if (valueFormat == ValueFormat.None)
            {
                result = new ErrorExpression(format.Value + " is not a supported rich_presence_value format", format);
                return false;
            }

            var expression = GetParameter(scope, "expression", out result);
            if (expression == null)
                return false;

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { name, expression, format });
            CopyLocation(result);
            return true;
        }

        protected override bool BuildMacro(RichPresenceDisplayFunction.RichPresenceDisplayContext context, InterpreterScope scope, out ExpressionBase result)
        {
            var name = GetStringParameter(scope, "name", out result);
            if (name == null)
                return false;

            var format = GetStringParameter(scope, "format", out result);
            if (format == null)
                return false;

            var valueFormat = ParseFormat(format.Value);
            if (valueFormat == ValueFormat.None)
            {
                result = new ErrorExpression("Unknown format", format);
                return false;
            }

            var value = GetExpressionValue(scope, out result);
            if (value == null)
                return false;

            var functionCall = scope.GetContext<FunctionCallExpression>();
            result = context.RichPresence.AddValueField(functionCall, name, valueFormat);
            if (result != null)
                return false;

            context.DisplayString.AddParameter(name.Value, value);
            return true;
        }
    }
}
