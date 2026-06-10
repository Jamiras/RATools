using RATools.Parser.Internal;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class ErrorRequirementExpression : RequirementExpressionBase
    {
        public ErrorRequirementExpression(ErrorExpression error)
        {
            _error = error;
        }

        private readonly ErrorExpression _error;

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            return _error;
        }

        internal override void AppendString(StringBuilder builder)
        {
            _error.AppendString(builder);
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as ErrorRequirementExpression;
            return (that != null && that._error == _error);
        }
    }
}
