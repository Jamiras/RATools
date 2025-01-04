using RATools.Parser.Expressions;
using System;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class ArrayReduceFunction : FunctionDefinitionExpression
    {
        public ArrayReduceFunction()
            : base("array_reduce")
        {
            Parameters.Add(new VariableDefinitionExpression("inputs"));
            Parameters.Add(new VariableDefinitionExpression("initial"));
            Parameters.Add(new VariableDefinitionExpression("reducer"));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var array = GetParameter(scope, "inputs", out result);
            if (array == null)
            {
                return false;
            }
            if (!array.ReplaceVariables(scope, out result))
            {
                return false;
            }
            var inputs = result as IIterableExpression;
            if (inputs == null)
            {
                result = new ErrorExpression("Cannot iterate over " + result.ToString(), result);
                return false;
            }

            var accumulator = GetParameter(scope, "initial", out result);
            if (accumulator == null)
            {
                return false;
            }

            var reducer = GetFunctionParameter(scope, "reducer", out result);
            if (reducer == null)
            {
                return false;
            }
            if ((reducer.Parameters.Count - reducer.DefaultParameters.Count) != 2)
            {
                result = new ErrorExpression("reducer function must accept two parameters (acc, value)");
                return false;
            }

            var iteratorScope = reducer.CreateCaptureScope(scope);
            foreach (var kvp in reducer.DefaultParameters)
            {
                iteratorScope.AssignVariable(new VariableExpression(kvp.Key), kvp.Value);
            }

            var innerAccumulator = new VariableExpression(reducer.Parameters.First().Name);
            var nextInput = new VariableExpression(reducer.Parameters.ElementAt(1).Name);

            foreach (var input in inputs.IterableExpressions())
            {
                if (!input.ReplaceVariables(iteratorScope, out result))
                {
                    return false;
                }
                iteratorScope.AssignVariable(innerAccumulator, accumulator);
                iteratorScope.AssignVariable(nextInput, result);
                if (!reducer.Evaluate(iteratorScope, out result))
                {
                    return false;
                }
                if (result == null)
                {
                    result = new ErrorExpression("reducer function did not return a value", reducer);
                    return false;
                }
                else if (result.Type == ExpressionType.Error)
                    return false;

                accumulator = result;
            }

            result = accumulator;
            return true;
        }
    }
}
