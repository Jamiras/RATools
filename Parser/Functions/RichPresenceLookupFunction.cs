using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class RichPresenceLookupFunction : FunctionDefinitionExpression
    {
        public RichPresenceLookupFunction()
            : base("rich_presence_lookup")
        {
            Parameters.Add(new VariableExpression("name"));
            Parameters.Add(new VariableExpression("expression"));
            Parameters.Add(new VariableExpression("dictionary"));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var context = scope.GetContext<RichPresenceDisplayFunction.RichPresenceDisplayContext>();
            if (context == null)
            {
                result = new ParseErrorExpression(Name.Name + " has no meaning outside of a rich_presence_display call");
                return false;
            }

            var name = GetStringParameter(scope, "name", out result);
            if (name == null)
                return false;

            var expression = GetParameter(scope, "expression", out result);
            if (expression == null)
                return false;

            var value = TriggerBuilderContext.GetValueString(expression, scope, out result);
            if (value == null)
                return false;

            var parameter = GetParameter(scope, "dictionary", out result);
            if (parameter == null)
                return false;

            var dictionary = parameter as DictionaryExpression;
            if (dictionary == null)
            {
                result = new ParseErrorExpression("dictionary is not a dictionary", parameter);
                return false;
            }

            context.RichPresence.AddLookupField(name.Value, dictionary);

            context.DisplayString.Append('@');
            context.DisplayString.Append(name.Value);
            context.DisplayString.Append('(');
            context.DisplayString.Append(value);
            context.DisplayString.Append(')');
            return true;
        }
    }
}
