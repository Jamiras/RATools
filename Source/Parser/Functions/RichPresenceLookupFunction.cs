using RATools.Parser.Expressions;

namespace RATools.Parser.Functions
{
    internal class RichPresenceLookupFunction : RichPresenceDisplayFunction.FunctionDefinition
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

        protected override bool BuildMacro(RichPresenceDisplayFunction.RichPresenceDisplayContext context, InterpreterScope scope, out ExpressionBase result)
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

            var integer = expression as IntegerConstantExpression;
            if (integer != null)
            {
                var entry = dictionary.GetEntry(integer) ?? fallback;

                var stringValue = entry as StringConstantExpression;
                if (stringValue != null)
                {
                    context.DisplayString.AddParameter(stringValue);
                    return true;
                }
            }

            var value = GetExpressionValue(scope, out result);
            if (value == null)
                return false;

            var functionCall = scope.GetContext<FunctionCallExpression>();
            result = context.RichPresence.AddLookupField(functionCall, name, dictionary, fallback);
            if (result != null)
                return false;

            context.DisplayString.AddParameter(name.Value, value);
            return true;
        }
    }
}
