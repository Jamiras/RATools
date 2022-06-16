using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class NeverFunction : FlagConditionFunction
    {
        public NeverFunction()
            : base("never", RequirementType.ResetIf)
        {
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            // never(A || B) => never(A) && never(B)
            var condition = comparison as ConditionalExpression;
            if (condition != null && condition.Operation == ConditionalOperation.Or)
                return SplitConditions(scope, condition, ConditionalOperation.And, out result);

            return base.ReplaceVariables(scope, out result);
        }
    }
}
