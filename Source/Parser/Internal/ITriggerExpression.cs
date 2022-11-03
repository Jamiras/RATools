using RATools.Parser.Expressions;
using RATools.Data;

namespace RATools.Parser.Internal
{
    internal interface ITriggerExpression
    {
        /// <summary>
        /// Appends <see cref="Requirement"/>s to the <paramref name="context"/> represented by this expression.
        /// </summary>
        /// <param name="context">The context to append <see cref="Requirement"/>s to.</param>
        /// <returns><c>null</c> on success, or an <see cref="ErrorExpression"/> on failure.</returns>
        ErrorExpression BuildTrigger(TriggerBuilderContext context);
    }
}
