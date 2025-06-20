﻿using RATools.Data;
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
                    {
                        if (Behavior == RequirementType.ResetIf)
                        {
                            // ResetIf(true) should be allowed if guarded by a PauseIf
                            var achievementBuilderContext = context as AchievementBuilderContext;
                            if (achievementBuilderContext != null && achievementBuilderContext.HasPauseIf == true)
                                break;
                        }

                        return new AlwaysFalseExpression();
                    }

                    if (Behavior == RequirementType.ResetIf)
                    {
                        // ResetIf with a hit target of 1 will automatically clear its
                        // hit count when true, so it's no different than a ResetIf without
                        // a hit target. Discard the hit target.
                        var tallied = optimized as TalliedRequirementExpression;
                        if (tallied != null && tallied.HitTarget == 1)
                            optimized = OptimizeNeverOnce(tallied);
                    }

                    break;

                case RequirementType.Trigger:
                    if (optimized is AlwaysTrueExpression)
                        return optimized;

                    // trigger_when(always_false()) makes the group always false,
                    // but doing so in an alt allows showing a trigger alongside a measured.
                    if (optimized is AlwaysFalseExpression && context is not AltBuilderContext)
                        return optimized;

                    if (optimized is MeasuredRequirementExpression && context is not AltBuilderContext)
                    {
                        // trigger_when(measured(...)) can be split into two alts:
                        //   measured(...) || trigger_when(always_false())
                        //
                        // if core is false, the trigger icon will not be shown
                        // if core is true, and the measured alt is false, it will show the trigger icon
                        //   because if the trigger condition was true, it would trigger
                        // if core is true, and the measured alt is true, it will fire
                        var clause = new RequirementClauseExpression
                        {
                            Operation = ConditionalOperation.Or,
                            Location = Location,
                        };
                        clause.AddCondition(optimized);
                        clause.AddCondition(new BehavioralRequirementExpression
                        {
                            Behavior = RequirementType.Trigger,
                            Condition = new AlwaysFalseExpression(),
                        });
                        return clause;
                    }
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

        private static RequirementExpressionBase OptimizeNeverOnce(TalliedRequirementExpression tallied)
        {
            // multiple tallied conditions is an AddHits chain. we could try to convert it
            // to an OrNext chain, but let's keep things simple for now.
            if (tallied.Conditions.Count() != 1)
                return tallied; 

            RequirementExpressionBase optimized = tallied.Conditions.First();

            // if there's no nested reset clause, just return the condition
            if (tallied.ResetCondition == null)
                return optimized;

            // if there is a Reset subclause, merge it in
            var logicalCombining = optimized as ILogicalCombineExpression;
            if (logicalCombining != null)
            {
                RequirementExpressionBase combined = null;

                // if A can never accumulate a hit, the ResetNextIf acts as a blocker for A,
                // so it can just be logically inverted and appeneded as another condition
                //
                //    never(once(A) && never(B)) => never(A && !B)
                //
                var notCondition = tallied.ResetCondition.InvertLogic();
                if (notCondition != null)
                    combined = logicalCombining.Combine(notCondition, ConditionalOperation.And) as RequirementExpressionBase;

                if (combined == null)
                {
                    // - if B cannot be inverted, keep it as a ResetNextIf
                    var resetCondition = new BehavioralRequirementExpression
                    {
                        Behavior = RequirementType.ResetIf,
                        Condition = tallied.ResetCondition,
                    };

                    combined = logicalCombining.Combine(resetCondition, ConditionalOperation.And) as RequirementExpressionBase;
                }

                optimized = combined ?? tallied; // fallback to never(once(...))
            }

            return optimized;
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
