using RATools.Parser.Expressions;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class AllOfFunction : IterableJoiningFunction
    {
        public AllOfFunction()
            : base("all_of")
        {
        }

        protected override ExpressionBase Combine(ExpressionBase left, ExpressionBase right)
        {
            right.IsLogicalUnit = true;

            if (left == null)
                return right;

            return new ConditionalExpression(left, ConditionalOperation.And, right);
        }

        protected override ExpressionBase GenerateEmptyResult()
        {
            return AlwaysTrueFunction.CreateAlwaysTrueFunctionCall();
        }
    }
}
