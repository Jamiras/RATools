using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;

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

            var expression = RequirementExpressionBase.ConvertToRequirementExpression(comparison);
            if (expression == null)
            {
                result = InvalidParameter(comparison, scope, "comparison", ExpressionType.Comparison);
                return false;
            }

            var when = GetRequirementParameter(scope, "when", out result);
            if (when == null)
                return false;

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

            result = new MeasuredRequirementExpression() { Condition = expression, When = when, Format = format };
            CopyLocation(result, scope);
            return true;
        }
    }
}
