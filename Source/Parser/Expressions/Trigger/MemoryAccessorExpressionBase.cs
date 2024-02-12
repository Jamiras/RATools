using RATools.Data;

namespace RATools.Parser.Expressions.Trigger
{
    internal abstract class MemoryAccessorExpressionBase : ExpressionBase
    {
        protected MemoryAccessorExpressionBase()
            : base(ExpressionType.MemoryAccessor)
        {
        }

        /// <summary>
        /// Gets the lowest and highest values that can be represented by this expression.
        /// </summary>
        public abstract void GetMinMax(out long min, out long max);

        /// <summary>
        /// Removes redundant wrappers from the expression.
        /// </summary>
        public static ExpressionBase ReduceToSimpleExpression(ExpressionBase expression)
        {
            var memoryValue = expression as MemoryValueExpression;
            if (memoryValue != null)
            {
                if (!memoryValue.HasMemoryAccessor)
                    return memoryValue.ExtractConstant();

                expression = memoryValue.ConvertToModifiedMemoryAccessor() ?? expression;
            }

            var modifiedMemoryAccessor = expression as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null && modifiedMemoryAccessor.ModifyingOperator == RequirementOperator.None)
                expression = modifiedMemoryAccessor.MemoryAccessor;
                    
            return expression;
        }

        internal static MemoryValueExpression WrapInMemoryValue(ExpressionBase expr)
        {
            var memoryAccessorBase = expr as MemoryAccessorExpressionBase;
            if (memoryAccessorBase == null)
                return null;

            var memoryValue = expr as MemoryValueExpression;
            if (memoryValue != null)
                return memoryValue;

            var modifiedMemoryAccessor = expr as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null)
                return new MemoryValueExpression(modifiedMemoryAccessor);

            var memoryAccessor = expr as MemoryAccessorExpression;
            if (memoryAccessor != null)
                return new MemoryValueExpression(new ModifiedMemoryAccessorExpression(memoryAccessor));

            return null;
        }
    }
}
