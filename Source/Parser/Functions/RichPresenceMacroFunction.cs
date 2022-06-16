using RATools.Data;
using RATools.Parser.Internal;
using System;

namespace RATools.Parser.Functions
{
    internal class RichPresenceMacroFunction : RichPresenceDisplayFunction.FunctionDefinition
    {
        public RichPresenceMacroFunction()
            : base("rich_presence_macro")
        {
            Parameters.Add(new VariableDefinitionExpression("macro"));
            Parameters.Add(new VariableDefinitionExpression("expression"));
        }

        public static ValueFormat GetValueFormat(string macro)
        {
            switch (macro)
            {
                case "Number":
                    return ValueFormat.Value;

                case "Score":
                    return ValueFormat.Score;

                case "Centisecs":
                    return ValueFormat.TimeMillisecs;

                case "Seconds":
                    return ValueFormat.TimeSecs;

                case "Minutes":
                    return ValueFormat.TimeMinutes;

                case "Float1":
                    return ValueFormat.Float1;

                case "Float2":
                    return ValueFormat.Float2;

                case "Float3":
                    return ValueFormat.Float3;

                case "Float4":
                    return ValueFormat.Float4;

                case "Float5":
                    return ValueFormat.Float5;

                case "Float6":
                    return ValueFormat.Float6;

                case "ASCIIChar":
                    return ValueFormat.ASCIIChar;

                case "UnicodeChar":
                    return ValueFormat.UnicodeChar;

                default:
                    return ValueFormat.None;
            }
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var macro = GetStringParameter(scope, "macro", out result);
            if (macro == null)
                return false;

            if (GetValueFormat(macro.Value) == ValueFormat.None)
            {
                result = new ErrorExpression("Unknown rich presence macro: " + macro.Value);
                return false;
            }

            var expression = GetParameter(scope, "expression", out result);
            if (expression == null)
                return false;

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { macro, expression });
            CopyLocation(result);
            return true;
        }

        public override bool BuildMacro(RichPresenceDisplayFunction.RichPresenceDisplayContext context, InterpreterScope scope, out ExpressionBase result)
        {
            var macro = GetStringParameter(scope, "macro", out result);
            if (macro == null)
                return false;

            var expression = GetParameter(scope, "expression", out result);
            if (expression == null)
                return false;

            var value = TriggerBuilderContext.GetValueString(expression, scope, out result);
            if (value == null)
                return false;

            var functionCall = scope.GetContext<FunctionCallExpression>();
            var valueFormat = GetValueFormat(macro.Value);
            context.RichPresence.AddValueField(functionCall, macro.Value, valueFormat);

            result = new StringConstantExpression(String.Format("@{0}({1})", macro.Value, value));
            return true;
        }
    }
}
