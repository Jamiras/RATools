using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class SumOfFunction : IterableJoiningFunction
    {
        public SumOfFunction()
            : base("sum_of")
        {
        }

        protected override ExpressionBase Combine(ExpressionBase left, ExpressionBase right)
        {
            if (left == null)
                return right;

            var combined = new MathematicExpression(left, MathematicOperation.Add, right);
            return combined.MergeOperands();
        }

        protected override ExpressionBase GenerateEmptyResult()
        {
            return new IntegerConstantExpression(0);
        }
    }
}
