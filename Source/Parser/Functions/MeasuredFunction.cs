using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class MeasuredFunction : FunctionDefinitionExpression
    {
        public MeasuredFunction()
            : base("measured")
        {
            Parameters.Add(new VariableDefinitionExpression("comparison"));
            Parameters.Add(new VariableDefinitionExpression("when"));
            Parameters.Add(new VariableDefinitionExpression("format"));

            DefaultParameters["when"] = new AlwaysTrueExpression();
            DefaultParameters["format"] = new StringConstantExpression("raw");
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            var expression = comparison as RequirementExpressionBase;
            if (expression == null)
            {
                var memoryValue = comparison as MemoryValueExpression;
                if (memoryValue == null)
                {
                    memoryValue = MemoryAccessorExpressionBase.WrapInMemoryValue(comparison);
                    if (memoryValue == null)
                    {
                        result = new ErrorExpression("comparison did not evaluate to a valid comparison", comparison);
                        return false;
                    }
                }

                expression = new MeasuredRequirementExpression.MemoryValueWrapper(memoryValue);
                memoryValue.CopyLocation(expression);
            }

            var when = GetParameter(scope, "when", out result);
            if (when == null)
                return false;

            var whenExpression = when as RequirementExpressionBase;
            if (whenExpression == null)
            {
                result = new ErrorExpression("when did not evaluate to a valid comparison", when);
                return false;
            }

            var formatStr = GetStringParameter(scope, "format", out result);
            if (formatStr == null)
                return false;

            RequirementType format = RequirementType.Measured;
            if (formatStr.Value == "percent")
            {
                format = RequirementType.MeasuredPercent;
            }
            else if (formatStr.Value != "raw")
            {
                result = new ErrorExpression("Unknown format: " + formatStr.Value, formatStr);
                return false;
            }

            result = new MeasuredRequirementExpression() { Condition = expression, When = whenExpression, Format = format };
            CopyLocation(result);
            return true;
        }
    }
}
