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
            if (!IsInTriggerClause(scope, out result))
                return false;

            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { comparison });
            CopyLocation(result);
            return true;
        }

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var comparison = functionCall.Parameters.First();
            return BuildTriggerConditions(context, scope, comparison, 1);
        }
    }
}
