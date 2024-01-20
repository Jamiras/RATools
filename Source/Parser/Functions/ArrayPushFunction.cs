using RATools.Parser.Expressions;

namespace RATools.Parser.Functions
{
    internal class ArrayPushFunction : FunctionDefinitionExpression
    {
        public ArrayPushFunction()
            : base("array_push")
        {
            Parameters.Add(new VariableDefinitionExpression("array"));
            Parameters.Add(new VariableDefinitionExpression("value"));
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

            var value = GetParameter(scope, "value", out result);
            if (value == null)
                return false;

            var variableExpression = new VariableExpression("array_push(" + arrayExpression.Variable.Name + ")");
            var assignScope = new InterpreterScope(scope) { Context = new AssignmentExpression(variableExpression, value) };
            if (!value.ReplaceVariables(assignScope, out result))
                return false;

            array.Entries.Add(result);
            result = null;
            return true;
        }
    }
}
