using RATools.Parser.Expressions;

namespace RATools.Parser.Functions
{
    internal class ArrayMapFunction : IterableJoiningFunction
    {
        public ArrayMapFunction()
            : base("array_map")
        {
        }

        protected ArrayMapFunction(string name)
            : base(name)
        {
        }

        protected override ExpressionBase Combine(ExpressionBase accumulator, ExpressionBase predicateResult, ExpressionBase predicateInput)
        {
            var array = accumulator as ArrayExpression;
            if (array == null)
                array = new ArrayExpression();

            array.Entries.Add(predicateResult);
            return array;
        }

        protected override ExpressionBase GenerateEmptyResult()
        {
            return new ArrayExpression();
        }
    }
}
