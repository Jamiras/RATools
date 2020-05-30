using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class ArrayPushFunction : FunctionDefinitionExpression
    {
        public ArrayPushFunction()
            : base("array_push")
        {
            // required parameters
            Parameters.Add(new VariableDefinitionExpression("array"));
            Parameters.Add(new VariableDefinitionExpression("value"));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var arrayExpression = GetParameter(scope, "array", out result);
            if (arrayExpression == null)
                return false;
            var array = arrayExpression as ArrayExpression;
            if (array == null)
            {
                result = new ParseErrorExpression("array did not evaluate to an array", arrayExpression);
                return false;
            }

            var value = GetParameter(scope, "value", out result);
            if (value == null)
                return false;
            // don't call ReplaceVariables, we don't want to evaluate the item being added here

            array.Entries.Add(value);
            result = array;
            return true;
        }
    }
}
