using RATools.Data;
using RATools.Parser.Internal;
using System;
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

                case RequirementType.Trigger:
                    return "trigger_when";

                case RequirementType.SubHits:
                    return "deduct";

                default:
                    return null;
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
                        error = Condition.BuildSubclauseTrigger(context, ConditionalOperation.And, Behavior);
                    }
                    else
                    {
                        error = Condition.BuildSubclauseTrigger(context);
                    }

                    //if (error != null && error.Message.Contains("too complex"))
                    //{
                    //    // trigger_when(A || B) can be split into alt1:trigger_when(A) and alt2:trigger_when(B)
                    //    if (DistributeConditionAcrossAlts(context, ref error))
                    //        return null;
                    //}
                    break;

                case RequirementType.ResetIf:
                case RequirementType.PauseIf:
                    if (reqClause != null && reqClause.Operation == ConditionalOperation.Or)
                    {
                        // never(A || B) -> never(A) && never(B)
                        // unless(A || B) -> unless(A) && unless(B)
                        error = Condition.BuildSubclauseTrigger(context, ConditionalOperation.Or, Behavior);
                        break;
                    }
                    goto default;

                default:
                    error = Condition.BuildSubclauseTrigger(context);
                    break;
            }

            if (error != null)
                return error;

            context.LastRequirement.Type = Behavior;

            return null;
        }

        private bool DistributeConditionAcrossAlts(TriggerBuilderContext context, ref ErrorExpression error)
        {
            //var achievementContext = context as AchievementBuilderContext;
            //if (achievementContext.Achievement.AlternateRequirements.Count == 0) // no alts yet
            //{
            //    var clause = Condition as RequirementClauseExpression;
            //    if (clause != null && clause.Operation == ConditionalOperation.Or)
            //    {
            //        foreach (var condition in clause.Conditions)
            //        {
            //            var alt = new List<Requirement>();
            //            var subclauseContext = new TriggerBuilderContext { Trigger = alt };
            //            error = condition.BuildSubclauseTrigger(subclauseContext);
            //            if (error != null)
            //                return false;

            //            alt[alt.Count - 1].Type = Behavior;
            //            achievementContext.Groups.Add(alt);
            //        }

            //        return true;
            //    }
            //}

            return false;
        }

        public override RequirementExpressionBase Optimize(TriggerBuilderContext context)
        {
            var optimized = Condition.Optimize(context);
            if (!ReferenceEquals(Condition, optimized))
            {
                return new BehavioralRequirementExpression
                {
                    Behavior = Behavior,
                    Condition = optimized
                };
            }

            return base.Optimize(context);
        }

        public override RequirementExpressionBase LogicalIntersect(RequirementExpressionBase that, ConditionalOperation condition)
        {
            var thatBehavior = that as BehavioralRequirementExpression;
            if (thatBehavior != null && thatBehavior.Behavior == Behavior)
            {
                var intersect = Condition.LogicalIntersect(thatBehavior.Condition, condition);
                if (intersect == null)
                    return null;

                if (ReferenceEquals(intersect, Condition))
                    return this;
                if (ReferenceEquals(intersect, thatBehavior.Condition))
                    return that;
            }

            return base.LogicalIntersect(that, condition);
        }
    }
}
