using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class RememberedFunction : FunctionDefinitionExpression
    {
        public RememberedFunction()
            : base("remembered")
        {
            Parameters.Add(new VariableDefinitionExpression("accessor"));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var accessor = GetParameter(scope, "accessor", out result);
            if (accessor == null)
                return false;

            if (!accessor.ReplaceVariables(scope, out result))
                return false;

            var remembered = RememberRecallExpression.WrapInRemember(result);
            if (remembered != null)
                result = remembered;

            CopyLocation(result, scope);
            result.MakeReadOnly();
            return true;
        }
    }
}
