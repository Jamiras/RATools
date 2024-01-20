using RATools.Parser.Expressions;

namespace RATools.Parser.Internal
{
    internal interface IUpconvertibleExpression
    {
        /// <summary>
        /// Attempts to create a new expression from the current expression without loss of data.
        /// </summary>
        /// <param name="newType">The type of express to try to convert to.</param>
        /// <returns>
        /// A new expression of the requested type, or <c>null</c> if the conversion could not be performed.
        /// </returns>
        ExpressionBase UpconvertTo(ExpressionType newType);
    }
}
