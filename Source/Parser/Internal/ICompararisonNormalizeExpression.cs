using RATools.Parser.Expressions;

namespace RATools.Parser.Internal
{
    internal interface IComparisonNormalizeExpression
    {
        /// <summary>
        /// Normalizes the comparison between the current expression and the <paramref name="right"/> expression using the <paramref name="operation"/> operator.
        /// </summary>
        /// <param name="right">The expression to compare with the current expression.</param>
        /// <param name="operation">How to compare the expressions.</param>
        /// <param name="canModifyRight"><c>true</c> if <paramref name="right"/> can be changed, <c>false</c> if not.</param>
        /// <returns>
        /// An expression representing the normalized comparison, or <c>null</c> if normalization did not occur.
        /// </returns>
        ExpressionBase NormalizeComparison(ExpressionBase right, ComparisonOperation operation, bool canModifyRight);
    }
}
