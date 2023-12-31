using RATools.Parser.Functions;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class AlwaysTrueExpression : RequirementExpressionBase
    {
        public AlwaysTrueExpression()
            : base()
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

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            context.Trigger.Add(AlwaysTrueFunction.CreateAlwaysTrueRequirement());
            return null;
        }

        public override RequirementExpressionBase InvertLogic()
        {
            return new AlwaysFalseExpression();
        }
    }
}
