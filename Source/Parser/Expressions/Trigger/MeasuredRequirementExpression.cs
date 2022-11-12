using RATools.Data;
using RATools.Parser.Internal;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class MeasuredRequirementExpression : RequirementExpressionBase,
        ICloneableExpression
    {
        public MeasuredRequirementExpression()
            : base()
        {
        }

        public MeasuredRequirementExpression(MeasuredRequirementExpression source)
        {
            Condition = source.Condition;
            When = source.When;
            Format = source.Format;
        }

        public RequirementExpressionBase Condition { get; set; }
        public RequirementExpressionBase When { get; set; }
        public RequirementType Format { get; set; }


        ExpressionBase ICloneableExpression.Clone()
        {
            return Clone();
        }

        public new MeasuredRequirementExpression Clone()
        {
            return new MeasuredRequirementExpression(this);
        }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("measured(");

            if (Condition != null)
                Condition.AppendString(builder);
            else
                builder.Append("always_false()");

            if (When != null && When is not AlwaysTrueExpression)
            {
                builder.Append(", when=");
                When.AppendString(builder);
            }

            switch (Format)
            {
                case RequirementType.MeasuredPercent:
                    builder.Append(", format=\"percent\"");
                    break;
            }

            builder.Append(')');
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as MeasuredRequirementExpression;
            return (that != null && Format == that.Format && Condition == that.Condition && When == that.When);
        }

        public override RequirementExpressionBase Optimize(TriggerBuilderContext context)
        {
            Debug.Assert(Condition != null && When != null);

            var whenOptimized = When.Optimize(context);
            var updated = !ReferenceEquals(whenOptimized, When);

            var conditionOptimized = Condition.Optimize(context);
            updated |= !ReferenceEquals(conditionOptimized, Condition);

            if (updated)
            {
                var optimized = new MeasuredRequirementExpression
                {
                    Condition = conditionOptimized,
                    When = whenOptimized,
                    Format = this.Format
                };
                CopyLocation(optimized);
                return optimized;
            }

            return base.Optimize(context);
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            if (Format == RequirementType.MeasuredPercent && context is ValueBuilderContext)
                return new ErrorExpression("Value fields only support raw measured values", this);

            Debug.Assert(Condition != null);
            var clause = Condition as RequirementClauseExpression;
            if (clause != null && clause.Conditions.Count() > 1)
                return new ErrorExpression("measured comparison can only have one logical clause", Condition);

            var error = Condition.BuildSubclauseTrigger(context);
            if (error != null)
                return error;

            context.LastRequirement.Type = Format;

            Debug.Assert(When != null);
            if (When is not AlwaysTrueExpression)
            {
                var reqClause = When as RequirementClauseExpression;
                if (reqClause != null && reqClause.Operation == ConditionalOperation.And)
                {
                    // measured_if(A && B) -> measured_if(A) && measured_if(B)
                    error = When.BuildSubclauseTrigger(context, ConditionalOperation.And, RequirementType.MeasuredIf);
                }
                else
                {
                    error = When.BuildSubclauseTrigger(context);
                }

                if (error != null)
                    return error;

                context.LastRequirement.Type = RequirementType.MeasuredIf;
            }

            return null;
        }

        internal class MemoryValueWrapper : RequirementExpressionBase
        {
            public MemoryValueWrapper(MemoryValueExpression expression)
            {
                MemoryValue = expression;
            }

            public MemoryValueExpression MemoryValue { get; set; }

            internal override void AppendString(StringBuilder builder)
            {
                MemoryValue.AppendString(builder);
            }

            protected override bool Equals(ExpressionBase obj)
            {
                var that = obj as MemoryValueWrapper;
                return (that != null && MemoryValue == that.MemoryValue);
            }

            public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
            {
                return MemoryValue.BuildTrigger(context);
            }
        }
    }
}
