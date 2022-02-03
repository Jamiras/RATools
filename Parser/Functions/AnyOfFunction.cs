using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class AnyOfFunction : IterableJoiningFunction
    {
        public AnyOfFunction()
            : base("any_of")
        {
        }

        protected override ExpressionBase Combine(ExpressionBase left, ExpressionBase right)
        {
            right.IsLogicalUnit = true;

            if (left == null)
                return right;

            return new ConditionalExpression(left, ConditionalOperation.Or, right);
        }

        protected override ExpressionBase GenerateEmptyResult()
        {
            return AlwaysFalseFunction.CreateAlwaysFalseFunctionCall();
        }
    }
}
