using RATools.Parser.Expressions;

namespace RATools.Parser.Functions
{
    internal class DictionaryContainsKeyFunction : FunctionDefinitionExpression
    {
        public DictionaryContainsKeyFunction()
            : base("dictionary_contains_key")
        {
            Parameters.Add(new VariableDefinitionExpression("dictionary"));
            Parameters.Add(new VariableDefinitionExpression("key"));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var dictionary = GetDictionaryParameter(scope, "dictionary", out result);
            if (dictionary == null)
                return false;

            var value = GetParameter(scope, "key", out result);
            if (value == null)
                return false;

            result = new BooleanConstantExpression(false);

            foreach (var entry in dictionary.Entries)
            {
                if (entry.Key == value)
                {
                    result = new BooleanConstantExpression(true);
                    break;
                }
            }

            return true;
        }
    }
}
