using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class AlwaysFalseExpression : ExpressionBase, ITriggerExpression, IExecutableExpression
    {
        public AlwaysFalseExpression()
            : base(ExpressionType.Requirement)
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

        public ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            context.Trigger.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());
            return null;
        }

        public ErrorExpression Execute(InterpreterScope scope)
        {
            return new ErrorExpression("always_false() has no meaning outside of a trigger clause", this);
        }
    }
}
