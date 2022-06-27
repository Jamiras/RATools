using RATools.Parser.Expressions;

namespace RATools.Parser.Internal
{
    internal interface IExecutableExpression
    {
        /// <summary>
        /// Executes the expression.
        /// </summary>
        /// <returns><c>null</c> on success, or a <see cref="ErrorExpression"/> indicating the failure.</returns>
        ErrorExpression Execute(InterpreterScope scope);
    }
}
