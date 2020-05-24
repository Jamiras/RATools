using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class OnceFunction : RepeatedFunction
    {
        public OnceFunction()
            : base("once")
        {
            // for increased compatibility, "once(A || B)" becomes "AddHits A, B (1)", 
            // because as soon as either A or B is true, the hit target is met.
            _orNextFlag = RequirementType.AddHits;
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
