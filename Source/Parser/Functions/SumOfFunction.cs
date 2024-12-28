using RATools.Parser.Expressions;

namespace RATools.Parser.Functions
{
    internal class SumOfFunction : IterableJoiningFunction
    {
        public SumOfFunction()
            : base("sum_of")
        {
        }

        protected override ExpressionBase Combine(ExpressionBase accumulator, ExpressionBase predicateResult, ExpressionBase predicateInput)
        {
            if (accumulator == null)
                return predicateResult;

            var combined = new MathematicExpression(accumulator, MathematicOperation.Add, predicateResult);
            return combined.MergeOperands();
        }

        protected override ExpressionBase GenerateEmptyResult()
        {
            return new IntegerConstantExpression(0);
        }
    }
}
