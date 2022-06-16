using RATools.Parser.Expressions;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class ArrayPopFunction : FunctionDefinitionExpression
    {
        public ArrayPopFunction()
            : base("array_pop")
        {
            Parameters.Add(new VariableDefinitionExpression("array"));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var arrayExpression = GetReferenceParameter(scope, "array", out result);
            if (arrayExpression == null)
                return false;

            var array = arrayExpression.Expression as ArrayExpression;
            if (array == null)
            {
                result = new ErrorExpression("array did not evaluate to an array", arrayExpression);
                return false;
            }

            if (array.Entries.Count == 0)
            {
                result = new IntegerConstantExpression(0);
            }
            else
            {
                result = array.Entries[array.Entries.Count - 1];
                array.Entries.RemoveAt(array.Entries.Count - 1);
            }

            return true;
        }
    }
}
