using RATools.Parser.Internal;
using System;

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

            var fallback = GetParameter(scope, "fallback", out result);
            if (fallback == null)
                return false;

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { name, expression, dictionary, fallback });
            CopyLocation(result);
            return true;
        }

        public override bool BuildMacro(RichPresenceDisplayFunction.RichPresenceDisplayContext context, InterpreterScope scope, out ExpressionBase result)
        {
            var name = GetStringParameter(scope, "name", out result);
            if (name == null)
                return false;

            var expression = GetParameter(scope, "expression", out result);
            if (expression == null)
                return false;

            var dictionary = GetParameter(scope, "dictionary", out result) as DictionaryExpression;
            if (dictionary == null)
                return false;

            var fallback = GetParameter(scope, "fallback", out result);
            if (fallback == null)
                return false;

            var value = TriggerBuilderContext.GetValueString(expression, scope, out result);
            if (value == null)
                return false;

            var error = context.RichPresence.AddLookupField(name.Value, dictionary, fallback);
            if (error != null)
            {
                result = error;
                return false;
            }

            result = new StringConstantExpression(String.Format("@{0}({1})", name.Value, value));
            return true;
        }
    }
}
