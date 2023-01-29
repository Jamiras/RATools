using RATools.Data;
using RATools.Parser.Internal;
using System.Collections.Generic;
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

            var requirements = new List<Requirement>();
            ErrorExpression error;
            {
                // generate the subclause using a clean set of requirements in case we need to
                // rearrange some stuff later
                var oldRequirements = context.Trigger;
                context.Trigger = requirements;

                error = Condition.BuildSubclauseTrigger(context);

                context.Trigger = oldRequirements;
            }

            if (error != null)
                return error;

            // make sure at least one condition is flagged with Measured
            EnsureHasMeasuredRequirement(requirements);

            if (Format != RequirementType.Measured)
            {
                var measured = requirements.Last(r => r.Type == RequirementType.Measured);
                measured.Type = Format;
            }

            // copy the Measured clauses to the context
            foreach (var requirement in requirements)
                context.Trigger.Add(requirement);

            // if there's a MeasuredIf, process it too
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

        internal static void EnsureHasMeasuredRequirement(List<Requirement> requirements)
        {
            if (requirements.Any(r => r.Type == RequirementType.Measured))
                return;

            // find a condition without a flag
            var last = requirements.LastOrDefault(r => r.Type == RequirementType.None);
            if (last == null)
            {
                // find the last AddSource or SubSource
                last = requirements.LastOrDefault(r => r.Type == RequirementType.AddSource || r.Type == RequirementType.SubSource);
                if (last != null && last.Type == RequirementType.SubSource)
                {
                    // if it's a SubSource, try to find an AddSource
                    var index = requirements.IndexOf(last);
                    last = requirements.LastOrDefault(r => r.Type == RequirementType.AddSource);
                    if (last != null)
                    {
                        // found an AddSource. move it to the end (and any AddAddresses associated to it)
                        var index2 = requirements.IndexOf(last);
                        do
                        {
                            var toMove = requirements[index2];
                            requirements.RemoveAt(index2);
                            requirements.Insert(index, toMove);
                            index--;
                            index2--;
                        } while (index2 >= 0 && requirements[index2].Type == RequirementType.AddAddress);
                    }
                    else
                    {
                        // did not find an AddSource, add a dummy requirement
                        last = new Requirement { Left = FieldFactory.CreateField(0) };
                        requirements.Add(last);
                    }
                }
            }

            last.Type = RequirementType.Measured;
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
