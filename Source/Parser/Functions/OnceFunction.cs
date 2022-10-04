using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class OnceFunction : RepeatedFunction
    {
        public OnceFunction()
            : base("once")
        {
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            if (comparison is not RequirementClauseExpression)
            {
                // cannot directly access FunctionDefinitionExpression.ReplaceVariables, so mimic it
                result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { comparison });
                CopyLocation(result);
                return true;
            }

            result = AddHitCount(comparison, new IntegerConstantExpression(1), scope);
            return (result.Type != ExpressionType.Error);
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var comparison = functionCall.Parameters.First();
            return BuildTriggerConditions(context, scope, comparison, new IntegerConstantExpression(1));
        }
    }
}
