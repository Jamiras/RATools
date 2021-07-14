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

            inputs = result;
            if (!(inputs is IIterableExpression))
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

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { inputs, predicate });
            CopyLocation(result);
            return true;
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var inputs = GetParameter(scope, "inputs", out result) as IIterableExpression;
            if (inputs == null)
                return false;

            var predicateReference = GetParameter(scope, "predicate", out result) as FunctionReferenceExpression;
            if (predicateReference == null)
                return false;

            var predicate = scope.GetFunction(predicateReference.Name);
            if (predicate == null)
            {
                result = new ParseErrorExpression("Could not locate function: " + predicateReference.Name, predicateReference);
                return false;
            }

            if ((predicate.Parameters.Count - predicate.DefaultParameters.Count) != 1)
            {
                result = new ParseErrorExpression("predicate function must accept a single parameter");
                return false;
            }

            var iteratorScope = new InterpreterScope(scope);
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

            return true;
        }

        protected abstract ExpressionBase Combine(ExpressionBase left, ExpressionBase right);

        protected abstract ExpressionBase GenerateEmptyResult();
    }
}
