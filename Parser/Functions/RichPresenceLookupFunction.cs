using RATools.Parser.Internal;
using System.Linq;

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
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            if (!IsInRichPresenceDisplayClause(scope, out result))
                return false;

            var name = GetStringParameter(scope, "name", out result);
            if (name == null)
                return false;

            var expression = GetParameter(scope, "expression", out result);
            if (expression == null)
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

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { name, expression, dictionary });
            return true;
        }

        public override ParseErrorExpression BuildMacro(RichPresenceDisplayFunction.RichPresenceDisplayContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var name = (StringConstantExpression)functionCall.Parameters.First();
            var expression = functionCall.Parameters.ElementAt(1);
            var dictionary = (DictionaryExpression)functionCall.Parameters.ElementAt(2);

            ExpressionBase result;
            var value = TriggerBuilderContext.GetValueString(expression, scope, out result);
            if (value == null)
                return (ParseErrorExpression)result;

            var error = context.RichPresence.AddLookupField(name.Value, dictionary);
            if (error != null)
                return error;

            context.DisplayString.Append('@');
            context.DisplayString.Append(name.Value);
            context.DisplayString.Append('(');
            context.DisplayString.Append(value);
            context.DisplayString.Append(')');
            return null;
        }
    }
}
