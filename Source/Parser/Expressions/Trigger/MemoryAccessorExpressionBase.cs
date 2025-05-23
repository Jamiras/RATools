﻿using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Expressions.Trigger
{
    internal abstract class MemoryAccessorExpressionBase : ExpressionBase, IValueExpression
    {
        protected MemoryAccessorExpressionBase()
            : base(ExpressionType.MemoryAccessor)
        {
        }

        /// <summary>
        /// Evaluates an expression
        /// </summary>
        /// <returns><see cref="ErrorExpression"/> indicating the failure, or the result of evaluating the expression.</returns>
        public ExpressionBase Evaluate(InterpreterScope scope)
        {
            return this;
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

                if (!memoryValue.HasConstant && memoryValue.MemoryAccessors.Count() == 1)
                {
                    var memoryAccessor = memoryValue.MemoryAccessors.First();
                    if (memoryAccessor.CombiningOperator == RequirementType.AddSource)
                    {
                        if (memoryAccessor.ModifyingOperator == RequirementOperator.None)
                            return memoryAccessor.MemoryAccessor;

                        memoryAccessor = memoryAccessor.Clone();
                        memoryAccessor.CombiningOperator = RequirementType.None;
                        return memoryAccessor;
                    }
                }
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
            {
                if (expr is INumericConstantExpression)
                    return WrapInMemoryValue(new MemoryValueExpression().ApplyMathematic(expr, MathematicOperation.Add));

                return null;
            }

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
