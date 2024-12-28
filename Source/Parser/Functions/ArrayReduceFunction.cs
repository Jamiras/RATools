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

            var accumulatorExpression = GetParameter(scope, "initial", out result);
            if (accumulatorExpression == null)
            {
                return false;
            }

            var funcParam = GetFunctionParameter(scope, "reducer", out result);
            if (funcParam == null)
            {
                return false;
            }
            if ((funcParam.Parameters.Count - funcParam.DefaultParameters.Count) != 2)
            {
                result = new ErrorExpression("reducer function must accept two parameters (acc, value)");
                return false;
            }

            var iteratorScope = funcParam.CreateCaptureScope(scope);
            foreach (var kvp in funcParam.DefaultParameters)
            {
                iteratorScope.AssignVariable(new VariableExpression(kvp.Key), kvp.Value);
            }

            foreach (var input in inputs.IterableExpressions())
            {
                if (!input.ReplaceVariables(iteratorScope, out result))
                {
                    return false;
                }
                iteratorScope.AssignVariable(new VariableExpression(funcParam.Parameters.First().Name), accumulatorExpression);
                iteratorScope.AssignVariable(new VariableExpression(funcParam.Parameters.Last().Name), input);
                if (!funcParam.Evaluate(iteratorScope, out result))
                {
                    return false;
                }
                if (result == null)
                {
                    result = new ErrorExpression("reducer function did not return a value", funcParam);
                    return false;
                }
                accumulatorExpression = result;

            }

            result = accumulatorExpression;
            return true;
        }
    }
}
