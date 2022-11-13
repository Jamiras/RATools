using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class DisableWhenFunction : FunctionDefinitionExpression
    {
        public DisableWhenFunction()
            : base("disable_when")
        {
            Parameters.Add(new VariableDefinitionExpression("comparison"));
            Parameters.Add(new VariableDefinitionExpression("until"));

            DefaultParameters["until"] = new AlwaysFalseExpression();
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var comparison = GetRequirementParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            var until = GetRequirementParameter(scope, "until", out result);
            if (until == null)
                return false;

            result = new DisableWhenRequirementExpression() { Condition = comparison, Until = until };
            CopyLocation(result);
            return true;
        }
    }
}
