using RATools.Parser.Expressions;

namespace RATools.Parser.Internal
{
    internal interface IAssignableExpression
    {
        /// <summary>
        /// Updates the data referenced by the expression to the <see cref="newValue"/>.
        /// </summary>
        /// <returns><see cref="ErrorExpression"/> indicating the failure, or the result of evaluating the expression.</returns>
        ErrorExpression Assign(InterpreterScope scope, ExpressionBase newValue);
    }
}
