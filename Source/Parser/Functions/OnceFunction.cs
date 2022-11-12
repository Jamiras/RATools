using RATools.Parser.Expressions;
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
            var comparison = GetRequirementParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            if (!RepeatedFunction.CreateTallyExpression(comparison, 1, out result))
                return false;

            CopyLocation(result);
            return true;
        }
    }
}
