using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class RichPresenceLookupFunction : FunctionDefinitionExpression
    {
        public RichPresenceLookupFunction()
            : base("rich_presence_lookup")
        {
            Parameters.Add(new VariableDefinitionExpression("name"));
            Parameters.Add(new VariableDefinitionExpression("expression"));
            Parameters.Add(new VariableDefinitionExpression("dictionary"));

            Parameters.Add(new VariableDefinitionExpression("fallback"));
            DefaultParameters["fallback"] = new StringConstantExpression("");
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var name = GetStringParameter(scope, "name", out result);
            if (name == null)
                return false;

            var dictionary = GetDictionaryParameter(scope, "dictionary", out result);
            if (dictionary == null)
                return false;

            var fallback = GetStringParameter(scope, "fallback", out result);
            if (fallback == null)
                return false;

            var expression = GetParameter(scope, "expression", out result);
            if (expression == null)
                return false;

            var integer = expression as IntegerConstantExpression;
            if (integer != null)
            {
                var entry = dictionary.GetEntry(integer) ?? fallback;

                var stringValue = entry as StringConstantExpression;
                if (stringValue != null)
                {
                    result = stringValue;
                    return true;
                }
            }
            else if (!ValueBuilder.IsConvertible(expression))
            {
                result = ValueBuilder.InconvertibleError(expression);
                return false;
            }

            result = new RichPresenceLookupExpression(name, expression) { Items = dictionary, Fallback = fallback };
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
