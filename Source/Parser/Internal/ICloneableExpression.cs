using RATools.Parser.Expressions;

namespace RATools.Parser.Internal
{
    internal interface ICloneableExpression
    {
        /// <summary>
        /// Creates a clone of the expression.
        /// </summary>
        ExpressionBase Clone();
    }
}
