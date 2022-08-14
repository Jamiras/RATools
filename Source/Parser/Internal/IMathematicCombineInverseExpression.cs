using RATools.Parser.Expressions;

namespace RATools.Parser.Internal
{
    internal interface IMathematicCombineInverseExpression
    {
        /// <summary>
        /// Combines the current expression with the <paramref name="right"/> expression using the <paramref name="operation"/> operator.
        /// </summary>
        /// <param name="right">The expression to combine with the current expression.</param>
        /// <param name="operation">How to combine the expressions.</param>
        /// <returns>
        /// An expression representing the combined values on success, or <c>null</c> if the expressions could not be combined.
        /// </returns>
        ExpressionBase CombineInverse(ExpressionBase left, MathematicOperation operation);
    }
}
