using RATools.Parser.Expressions;

namespace RATools.Parser.Internal
{
    internal interface ITriggerExpression
    {
        ErrorExpression BuildTrigger(TriggerBuilderContext context);
    }
}
