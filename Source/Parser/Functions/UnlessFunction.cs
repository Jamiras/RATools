using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class UnlessFunction : FunctionDefinitionExpression
    {
        public UnlessFunction()
            : base("unless")
        {
            Parameters.Add(new VariableDefinitionExpression("comparison"));
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
                result = new ErrorExpression("comparison did not evaluate to a valid comparison", comparison);
                return false;
            }

            result = new BehavioralRequirementExpression
            {
                Behavior = RequirementType.PauseIf,
                Condition = (RequirementExpressionBase)comparison
            };

            CopyLocation(result);
            return true;
        }
    }
}
