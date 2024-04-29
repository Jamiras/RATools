using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class RichPresenceMacroFunction : FunctionDefinitionExpression
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

                case "Centiseconds":
                    return ValueFormat.TimeCentisecs;

                case "Seconds":
                    return ValueFormat.TimeSecs;

                case "SecondsAsMinutes":
                    return ValueFormat.TimeSecsAsMins;

                case "Minutes":
                    return ValueFormat.TimeMinutes;

                case "Fixed1":
                    return ValueFormat.Fixed1;

                case "Fixed2":
                    return ValueFormat.Fixed2;

                case "Fixed3":
                    return ValueFormat.Fixed3;

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

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var macro = GetStringParameter(scope, "macro", out result);
            if (macro == null)
                return false;

            var valueFormat = GetValueFormat(macro.Value);
            if (valueFormat == ValueFormat.None)
            {
                result = new ErrorExpression("Unknown rich presence macro: " + macro.Value);
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

            result = new RichPresenceMacroExpression(macro, expression) { Format = valueFormat };
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
