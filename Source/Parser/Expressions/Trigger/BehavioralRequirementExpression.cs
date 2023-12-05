using RATools.Data;
using RATools.Parser.Internal;
using System;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class BehavioralRequirementExpression : RequirementExpressionBase,
        ICloneableExpression
    {
        public BehavioralRequirementExpression()
        {
        }

        public BehavioralRequirementExpression(BehavioralRequirementExpression source)
            : this()
        {
            Behavior = source.Behavior;
            Condition = source.Condition;
        }

        public RequirementType Behavior { get; set; }

        public RequirementExpressionBase Condition { get; set; }

        ExpressionBase ICloneableExpression.Clone()
        {
            return Clone();
        }

        public new BehavioralRequirementExpression Clone()
        {
            return new BehavioralRequirementExpression(this);
        }

        public static string GetFunctionName(RequirementType behavior)
        {
            switch (behavior)
            {
                case RequirementType.PauseIf:
                    return "unless";

                case RequirementType.ResetIf:
                    return "never";

                case RequirementType.ResetNextIf:
                    return "resetnext_if";

                case RequirementType.Trigger:
                    return "trigger_when";

                case RequirementType.SubHits:
                    return "deduct";

                default:
                    return behavior.ToString();
            }
        }

        internal override void AppendString(StringBuilder builder)
        {
            var functionName = GetFunctionName(Behavior);
            if (functionName == null)
                throw new NotImplementedException(Behavior.ToString());

            builder.Append(functionName);
            builder.Append('(');
            Condition.AppendString(builder);
            builder.Append(')');
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as BehavioralRequirementExpression;
            return (that != null && that.Behavior == Behavior && that.Condition == Condition);
        }

        public bool CanBeEliminatedByInverting
        {
            get { return Behavior == RequirementType.ResetIf || Behavior == RequirementType.PauseIf; }
        }

        /// <summary>
        /// Returns an expression where any 'never(A)'s have been converted to '!A's
        /// </summary>
        /// <returns>New requirement, or <c>null</c> if the requirement cannot be inverted.</returns>
        /// <remarks>May return the original expression if nothing needed to be converted</remarks>
        public override RequirementExpressionBase InvertResetsAndPauses()
        {
            switch (Behavior)
            {
                case RequirementType.ResetIf:
                case RequirementType.PauseIf:
                    return Condition.InvertLogic();

                default:
                    return this;
            }
        }

        public override RequirementExpressionBase Optimize(TriggerBuilderContext context)
        {
            var optimized = Condition.Optimize(context);

            switch (Behavior)
            {
                case RequirementType.ResetIf:
                case RequirementType.ResetNextIf:
                case RequirementType.PauseIf:
                    optimized = optimized.InvertResetsAndPauses();

                    if (optimized is AlwaysFalseExpression)
                        return new AlwaysTrueExpression();

                    if (optimized is AlwaysTrueExpression)
                        return new AlwaysFalseExpression();

                    break;

                case RequirementType.Trigger:
                    if (optimized is AlwaysTrueExpression)
                        return optimized;
                    // trigger_when(always_false()) makes the group always false,
                    // but doing so in an alt allows showing a trigger alongside a measured.
                    if (optimized is AlwaysFalseExpression && context is not AltBuilderContext)
                        return optimized;
                    break;

                default:
                    break;
            }

            if (!ReferenceEquals(Condition, optimized))
            {
                return new BehavioralRequirementExpression
                {
                    Behavior = Behavior,
                    Condition = optimized,
                    Location = Location,
                };
            }

            return base.Optimize(context);
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            var reqClause = Condition as RequirementClauseExpression;

            ErrorExpression error;
            switch (Behavior)
            {
                case RequirementType.Trigger:
                    if (reqClause != null && reqClause.Operation == ConditionalOperation.And)
                    {
                        // trigger_when(A && B) -> trigger_when(A) && trigger_when(B)
                        return Condition.BuildSubclauseTrigger(context, ConditionalOperation.And, Behavior);
                    }
                    goto default;

                case RequirementType.ResetIf:
                case RequirementType.ResetNextIf:
                case RequirementType.PauseIf:
                    if (reqClause != null && reqClause.Operation == ConditionalOperation.Or)
                    {
                        // never(A || B) -> never(A) && never(B)
                        // unless(A || B) -> unless(A) && unless(B)
                        foreach (var condition in reqClause.Conditions.OfType<RequirementExpressionBase>())
                        {
                            if (condition is AlwaysFalseExpression)
                                continue;

                            error = condition.BuildSubclauseTrigger(context);
                            if (error != null)
                                return error;

                            context.LastRequirement.Type = Behavior;
                        }

                        return null;
                    }
                    goto default;

                default:
                    error = Condition.BuildSubclauseTrigger(context);
                    break;
            }

            if (error != null)
                return error;

            if (context.LastRequirement.Type != RequirementType.None)
            {
                switch (Behavior)
                {
                    case RequirementType.Trigger:
                        // trigger_when(trigger_when(A)) => trigger_when(A)
                        if (context.LastRequirement.Type == RequirementType.Trigger)
                            break;

                        goto default;

                    default:
                        return new ErrorExpression("Cannot apply " + GetFunctionName(Behavior) +
                            " to condition already flagged with " + GetFunctionName(context.LastRequirement.Type), this);
                }
            }

            context.LastRequirement.Type = Behavior;

            return null;
        }

        public override RequirementExpressionBase LogicalIntersect(RequirementExpressionBase that, ConditionalOperation condition)
        {
            var thatBehavior = that as BehavioralRequirementExpression;
            if (thatBehavior != null && thatBehavior.Behavior == Behavior)
            {
                RequirementExpressionBase intersect;
                if (Behavior == RequirementType.Trigger)
                    intersect = Condition.LogicalIntersect(thatBehavior.Condition, condition);
                else if (condition == ConditionalOperation.And)
                    intersect = Condition.LogicalIntersect(thatBehavior.Condition, ConditionalOperation.Or);
                else
                    intersect = Condition.LogicalIntersect(thatBehavior.Condition, ConditionalOperation.And);

                if (intersect == null)
                    return null;

                if (ReferenceEquals(intersect, Condition))
                    return this;
                if (ReferenceEquals(intersect, thatBehavior.Condition))
                    return that;
            }

            if (Behavior == RequirementType.Trigger)
            {
                // when the same clause is used inside and outside a trigger, the achievement will
                // become true in the same frame where the challenge indicator is shown.
                // assume the user is duplicating logic from another clause explicitly to show the
                // challenge indicator and discard the non-trigger logic
                //
                //     trigger_when(A > 2) && A > 2  => trigger_when(A > 2)
                //
                // this could also apply if the outside clause was more restrictive than the inside
                // clause, but don't automatically collapse that as it's probably a logical error 
                // rather than an intentional decision.
                if (Condition == that)
                    return this;
            }
            else
            {
                var inverted = that.InvertLogic();
                if (inverted == Condition)
                {
                    // never(A == X) && A != X  =>  never(A == X)
                    return this;
                }
            }

            return base.LogicalIntersect(that, condition);
        }
    }
}
