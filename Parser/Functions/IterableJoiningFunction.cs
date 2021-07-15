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
            var inputs = GetParameter(scope, "inputs", out result);
            if (inputs == null)
                return false;

            if (!inputs.ReplaceVariables(scope, out result))
                return false;

            var iterableInputs = inputs as IIterableExpression;
            if (iterableInputs == null)
            {
                result = new ParseErrorExpression("Cannot iterate over " + inputs.ToString(), inputs);
                return false;
            }

            var predicate = GetParameter(scope, "predicate", out result);
            if (predicate == null)
                return false;

            var functionReference = predicate as FunctionReferenceExpression;
            if (functionReference == null)
            {
                result = new ParseErrorExpression("predicate must be a function reference");
                return false;
            }

            var predicateFunction = scope.GetFunction(functionReference.Name);
            if (predicate == null)
            {
                result = new ParseErrorExpression("Could not locate function: " + functionReference.Name, functionReference);
                return false;
            }

            if ((predicateFunction.Parameters.Count - predicateFunction.DefaultParameters.Count) != 1)
            {
                result = new ParseErrorExpression("predicate function must accept a single parameter");
                return false;
            }

            var iteratorScope = new InterpreterScope(scope);
            var predicateParameter = new VariableExpression(predicateFunction.Parameters.First().Name);
            foreach (var kvp in predicateFunction.DefaultParameters)
                iteratorScope.AssignVariable(new VariableExpression(kvp.Key), kvp.Value);

            ExpressionBase expression = null;
            foreach (var input in iterableInputs.IterableExpressions())
            {
                if (!input.ReplaceVariables(iteratorScope, out result))
                    return false;

                iteratorScope.AssignVariable(predicateParameter, result);

                if (!predicateFunction.Evaluate(iteratorScope, out result))
                    return false;

                expression = Combine(expression, result);
                if (expression.Type == ExpressionType.ParseError)
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
