using RATools.Parser.Expressions;
using RATools.Parser.Internal;

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

        protected override ExpressionBase Combine(ExpressionBase left, ExpressionBase right)
        {
            var array = left as ArrayExpression;
            if (array == null)
                array = new ArrayExpression();

            array.Entries.Add(right);
            return array;
        }

        protected override ExpressionBase GenerateEmptyResult()
        {
            return new ArrayExpression();
        }
    }
}
