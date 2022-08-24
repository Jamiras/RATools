using RATools.Parser.Expressions;
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

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var comparison = functionCall.Parameters.First();
            return BuildTriggerConditions(context, scope, comparison, new IntegerConstantExpression(1));
        }
    }
}
