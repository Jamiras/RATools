using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal abstract class IterableJoiningFunction : FunctionDefinitionExpression
    {
        protected IterableJoiningFunction(string name)
            : base(name)
        {
            Parameters.Clear();
            Parameters.Add(new VariableDefinitionExpression("inputs"));
            Parameters.Add(new VariableDefinitionExpression("predicate"));
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var value = GetParameter(scope, "inputs", out result);
            if (value == null)
                return false;

            if (!value.ReplaceVariables(scope, out result))
                return false;

            var inputs = result as IIterableExpression;
            if (inputs == null)
            {
                result = new ErrorExpression("Cannot iterate over " + result.ToString(), result);
                return false;
            }

            var predicate = GetFunctionParameter(scope, "predicate", out result);
            if (predicate == null)
                return false;

            if ((predicate.Parameters.Count - predicate.DefaultParameters.Count) != 1)
            {
                result = new ErrorExpression("predicate function must accept a single parameter");
                return false;
            }

            var iteratorScope = predicate.CreateCaptureScope(scope);

            var predicateParameter = new VariableExpression(predicate.Parameters.First().Name);
            foreach (var kvp in predicate.DefaultParameters)
                iteratorScope.AssignVariable(new VariableExpression(kvp.Key), kvp.Value);

            ExpressionBase expression = null;
            foreach (var input in inputs.IterableExpressions())
            {
                if (!input.ReplaceVariables(iteratorScope, out result))
                    return false;

                iteratorScope.AssignVariable(predicateParameter, result);

                if (!predicate.Evaluate(iteratorScope, out result))
                    return false;

                expression = Combine(expression, result);
                if (expression.Type == ExpressionType.Error)
                {
                    result = expression;
                    return false;
                }
            }

            if (expression != null)
                result = expression;
            else
                result = GenerateEmptyResult();

            result.IsLogicalUnit = true;
            return true;
        }

        protected abstract ExpressionBase Combine(ExpressionBase left, ExpressionBase right);

        protected abstract ExpressionBase GenerateEmptyResult();
    }
}
