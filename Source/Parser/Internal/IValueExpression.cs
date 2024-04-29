using RATools.Parser.Expressions;

namespace RATools.Parser.Internal
{
    internal interface IValueExpression
    {
        /// <summary>
        /// Evaluates an expression
        /// </summary>
        /// <returns><see cref="ErrorExpression"/> indicating the failure, or the result of evaluating the expression.</returns>
        ExpressionBase Evaluate(InterpreterScope scope);
    }
}
