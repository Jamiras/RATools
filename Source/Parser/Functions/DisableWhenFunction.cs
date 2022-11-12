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
            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            var expression = comparison as RequirementExpressionBase;
            if (expression == null)
            {
                result = new ErrorExpression("comparison did not evaluate to a valid comparison", comparison);
                return false;
            }

            var until = GetParameter(scope, "until", out result);
            if (until == null)
                return false;

            var untilExpression = until as RequirementExpressionBase;
            if (untilExpression == null)
            {
                result = new ErrorExpression("until did not evaluate to a valid comparison", until);
                return false;
            }

            result = new DisableWhenRequirementExpression() { Condition = expression, Until = untilExpression };
            CopyLocation(result);
            return true;
        }
    }
}
