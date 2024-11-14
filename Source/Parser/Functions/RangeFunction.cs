﻿using RATools.Parser.Expressions;

namespace RATools.Parser.Functions
{
    internal class RangeFunction : FunctionDefinitionExpression
    {
        public RangeFunction()
            : base("range")
        {
            // required parameters
            Parameters.Add(new VariableDefinitionExpression("start"));
            Parameters.Add(new VariableDefinitionExpression("stop"));

            // optional parameters
            Parameters.Add(new VariableDefinitionExpression("step"));
            DefaultParameters["step"] = new IntegerConstantExpression(1);
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var start = GetIntegerParameter(scope, "start", out result);
            if (start == null)
                return false;

            var stop = GetIntegerParameter(scope, "stop", out result);
            if (stop == null)
                return false;

            var step = GetIntegerParameter(scope, "step", out result);
            if (step == null)
                return false;

            if (step.Value == 0)
            {
                result = new ErrorExpression("step must not be 0", step);
                return false;
            }

            var array = new ArrayExpression();

            if (start.Value > stop.Value)
            {
                if (step.Value > 0)
                {
                    result = new ErrorExpression("step must be negative if start is after stop", step.Location.Start.Line > 0 ? step : stop);
                    return false;
                }

                for (int i = start.Value; i >= stop.Value; i += step.Value)
                    array.Entries.Add(new IntegerConstantExpression(i));
            }
            else
            {
                if (step.Value < 0)
                {
                    result = new ErrorExpression("step must be positive if stop is after start", step);
                    return false;
                }

                for (int i = start.Value; i <= stop.Value; i += step.Value)
                    array.Entries.Add(new IntegerConstantExpression(i));
            }

            result = array;
            CopyLocation(result, scope);
            return true;
        }
    }
}
