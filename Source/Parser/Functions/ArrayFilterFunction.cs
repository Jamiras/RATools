using RATools.Parser.Expressions;

namespace RATools.Parser.Functions
{
    internal class ArrayFilterFunction : IterableJoiningFunction
    {
        public ArrayFilterFunction()
            : base("array_filter")
        {
        }

        protected ArrayFilterFunction(string name)
            : base(name)
        {
        }

        protected override ExpressionBase Combine(ExpressionBase accumulator, ExpressionBase predicateResult, ExpressionBase predicateInput)
        {
            var array = accumulator as ArrayExpression;
            if (array == null)
                array = new ArrayExpression();

            var boolResult = predicateResult as BooleanConstantExpression;
            if (boolResult == null)
            {
                if (AchievementScriptInterpreter.ContainsRuntimeLogic(predicateResult))
                    return new ErrorExpression("Cannot filter on runtime logic", predicateResult);

                return new ConversionErrorExpression(predicateResult, ExpressionType.BooleanConstant);
            }

            if (boolResult.Value)
                array.Entries.Add(predicateInput);

            return array;
        }

        protected override ExpressionBase GenerateEmptyResult()
        {
            return new ArrayExpression();
        }
    }
}
