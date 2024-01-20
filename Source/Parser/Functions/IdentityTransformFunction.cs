using RATools.Parser.Expressions;

namespace RATools.Parser.Functions
{
    internal class IdentityTransformFunction : FunctionDefinitionExpression
    {
        public IdentityTransformFunction()
            : base("identity_transform")
        {
            Parameters.Add(new VariableDefinitionExpression("accessor"));
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var parameter = GetParameter(scope, "accessor", out result);
            if (parameter == null)
                return false;

            if (!parameter.ReplaceVariables(scope, out result))
                return false;

            CopyLocation(result);
            return true;
        }
    }
}
