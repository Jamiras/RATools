using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class AlwaysTrueExpression : ExpressionBase, ITriggerExpression, IExecutableExpression
    {
        public AlwaysTrueExpression()
            : base(ExpressionType.RequirementClause)
        {
        }

        protected override bool Equals(ExpressionBase obj)
        {
            return (obj is AlwaysTrueExpression);
        }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("always_true()");
        }

        public override bool? IsTrue(InterpreterScope scope, out ErrorExpression error)
        {
            error = null;
            return true;
        }

        public ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            context.Trigger.Add(AlwaysTrueFunction.CreateAlwaysTrueRequirement());
            return null;
        }

        public ErrorExpression Execute(InterpreterScope scope)
        {
            return new ErrorExpression("always_true() has no meaning outside of a trigger clause", this);
        }
    }
}
