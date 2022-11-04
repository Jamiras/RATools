using RATools.Data;
using RATools.Parser.Internal;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class DisableWhenExpression : RequirementExpressionBase,
        ICloneableExpression
    {
        public DisableWhenExpression()
            : base()
        {
        }

        public DisableWhenExpression(DisableWhenExpression source)
        {
            Condition = source.Condition;
            Until = source.Until;
        }

        public RequirementExpressionBase Condition { get; set; }
        public RequirementExpressionBase Until { get; set; }

        ExpressionBase ICloneableExpression.Clone()
        {
            return Clone();
        }

        public new DisableWhenExpression Clone()
        {
            return new DisableWhenExpression(this);
        }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("disable_when(");

            if (Condition != null)
                Condition.AppendString(builder);
            else
                builder.Append("always_false()");

            if (Until != null && Until is not AlwaysFalseExpression)
            {
                builder.Append(", until=");
                Until.AppendString(builder);
            }

            builder.Append(')');
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as DisableWhenExpression;
            return (that != null && Condition == that.Condition && Until == that.Until);
        }

        public override RequirementExpressionBase Optimize(TriggerBuilderContext context)
        {
            Debug.Assert(Condition != null && Until != null);

            var untilOptimized = Until.Optimize(context);
            var updated = !ReferenceEquals(untilOptimized, Until);

            var conditionOptimized = Condition.Optimize(context);
            updated |= !ReferenceEquals(conditionOptimized, Condition);

            if (updated)
            {
                var optimized = new DisableWhenExpression
                {
                    Condition = conditionOptimized,
                    Until = untilOptimized
                };
                CopyLocation(optimized);
                return optimized;
            }

            return base.Optimize(context);
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            ErrorExpression error;

            Debug.Assert(Until != null);
            if (Until is not AlwaysFalseExpression)
            {
                var tallied = Condition as TalliedRequirementExpression;
                if (tallied != null && tallied.Conditions.Count() > 1)
                {
                    // Until has to be injected before each clause of the tally.
                    // do so using the reset clause of a new TalliedRequirementExpression
                    var newTally = tallied.Clone();
                    newTally.AddResetCondition(Until);
                    var newDisableWhen = new DisableWhenExpression 
                    {
                        Condition = newTally, 
                        Until = new AlwaysFalseExpression() 
                    };
                    return newDisableWhen.BuildTrigger(context);
                }

                error = Until.BuildSubclauseTrigger(context);
                if (error != null)
                    return error;

                context.LastRequirement.Type = RequirementType.ResetNextIf;
            }

            Debug.Assert(Condition != null);
            error = Condition.BuildSubclauseTrigger(context);
            if (error != null)
                return error;

            context.LastRequirement.Type = RequirementType.PauseIf;
            if (context.LastRequirement.HitCount == 0)
                context.LastRequirement.HitCount = 1;

            return null;
        }
    }
}
