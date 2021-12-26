using RATools.Data;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class UnlessFunction : FlagConditionFunction
    {
        public UnlessFunction()
            : base("unless", RequirementType.PauseIf)
        {
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            // unless(A || B) => unless(A) && unless(B)
            var condition = comparison as ConditionalExpression;
            if (condition != null && condition.Operation == ConditionalOperation.Or)
                return SplitConditions(scope, condition, ConditionalOperation.And, out result);

            return base.ReplaceVariables(scope, out result);
        }
    }
}
