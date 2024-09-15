using RATools.Parser.Expressions;

namespace RATools.Parser.Functions
{
    internal class ArrayContainsFunction : FunctionDefinitionExpression
    {
        public ArrayContainsFunction()
            : base("array_contains")
        {
            Parameters.Add(new VariableDefinitionExpression("array"));
            Parameters.Add(new VariableDefinitionExpression("value"));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var array = GetArrayParameter(scope, "array", out result);
            if (array == null)
                return false;

            var value = GetParameter(scope, "value", out result);
            if (value == null)
                return false;

            result = new BooleanConstantExpression(false);

            foreach (var entry in array.Entries)
            {
                if (entry == value)
                {
                    result = new BooleanConstantExpression(true);
                    break;
                }
            }

            return true;
        }
    }
}
