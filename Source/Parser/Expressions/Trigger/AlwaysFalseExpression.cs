using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class AlwaysFalseExpression : RequirementExpressionBase
    {
        public AlwaysFalseExpression()
            : base()
        {
        }

        protected override bool Equals(ExpressionBase obj)
        {
            return (obj is AlwaysFalseExpression);
        }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("always_false()");
        }

        public override bool? IsTrue(InterpreterScope scope, out ErrorExpression error)
        {
            error = null;
            return false;
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            context.Trigger.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());
            return null;
        }
    }
}
