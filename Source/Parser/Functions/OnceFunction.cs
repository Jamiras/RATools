using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class OnceFunction : FunctionDefinitionExpression
    {
        public OnceFunction()
            : base("once")
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

            if (!RepeatedFunction.CanBeTallied(comparison, RequirementType.ResetIf, out result))
                return false;

            var tally = new TalliedRequirementExpression { HitTarget = 1 };
            tally.AddTalliedCondition(comparison);

            result = tally;
            CopyLocation(result);
            return true;
        }
    }
}
