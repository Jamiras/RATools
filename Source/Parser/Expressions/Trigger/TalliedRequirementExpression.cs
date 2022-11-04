using RATools.Data;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class TalliedRequirementExpression : RequirementExpressionBase,
        ICloneableExpression
    {
        public TalliedRequirementExpression()
        {
        }

        public TalliedRequirementExpression(TalliedRequirementExpression source)
            : this()
        {
            HitTarget = source.HitTarget;

            if (source._conditions != null)
            {
                _conditions = new List<ExpressionBase>();
                foreach (var condition in source._conditions)
                    _conditions.Add(condition);
            }
        }

        public uint HitTarget { get; set; }

        public IEnumerable<ExpressionBase> Conditions
        {
            get { return _conditions ?? Enumerable.Empty<ExpressionBase>(); }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected List<ExpressionBase> _conditions;

        public IEnumerable<ExpressionBase> ResetConditions
        {
            get { return _resetConditions ?? Enumerable.Empty<ExpressionBase>(); }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected List<ExpressionBase> _resetConditions;

        public void AddTalliedCondition(ExpressionBase condition)
        {
            if (_conditions == null)
                _conditions = new List<ExpressionBase>();

            var reqClause = condition as RequirementClauseExpression;
            if (reqClause != null && reqClause.Operation == ConditionalOperation.And &&
                reqClause.Conditions.OfType<BehavioralRequirementExpression>().Any(c => c.Behavior == RequirementType.ResetIf))
            {
                if (_resetConditions == null)
                    _resetConditions = new List<ExpressionBase>();

                var newClause = new RequirementClauseExpression { Operation = ConditionalOperation.And };
                foreach (var c in reqClause.Conditions)
                {
                    var behavioral = c as BehavioralRequirementExpression;
                    if (behavioral != null && behavioral.Behavior == RequirementType.ResetIf)
                        _resetConditions.Add(behavioral.Condition);
                    else
                        newClause.AddCondition(c);
                }

                condition = newClause;
            }

            _conditions.Add(condition);
        }

        public void AddDeductedCondition(RequirementExpressionBase condition)
        {
            AddTalliedCondition(new BehavioralRequirementExpression
            {
                Condition = condition,
                Behavior = RequirementType.SubHits
            });
        }

        public void AddResetCondition(RequirementExpressionBase condition)
        {
            if (_resetConditions == null)
                _resetConditions = new List<ExpressionBase>();

            _resetConditions.Add(condition);
        }

        ExpressionBase ICloneableExpression.Clone()
        {
            return Clone();
        }

        public new TalliedRequirementExpression Clone()
        {
            return new TalliedRequirementExpression(this);
        }

        internal override void AppendString(StringBuilder builder)
        {
            if (_conditions != null && _conditions.Count > 1)
            {
                builder.AppendFormat("tally({0}, ", HitTarget);

                foreach (var condition in _conditions)
                {
                    condition.AppendString(builder);
                    builder.Append(", ");
                }

                builder.Length -= 2; // remove last ", "
                builder.Append(')');
            }
            else
            {
                if (HitTarget == 1)
                    builder.Append("once(");
                else
                    builder.AppendFormat("repeated({0}, ", HitTarget);

                if (_conditions == null || _conditions.Count == 0)
                    builder.Append("always_false()");
                else
                    _conditions[0].AppendString(builder);

                if (_resetConditions != null)
                {
                    foreach (var condition in _resetConditions)
                    {
                        builder.Append(" && never(");
                        condition.AppendString(builder);
                        builder.Append(')');
                    }
                }

                builder.Append(')');
            }
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as TalliedRequirementExpression;
            return (that != null && HitTarget == that.HitTarget &&
                CompareRequirements(_conditions, that._conditions) &&
                CompareRequirements(_resetConditions, that._resetConditions));
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            if (HitTarget == 0 && context is not ValueBuilderContext)
                return new ErrorExpression("Unbounded count is only supported in measured value expressions", this);

            ErrorExpression error;

            var sortedConditions = new List<ExpressionBase>(Conditions);
            int i = sortedConditions.Count - 1;
            for (; i >= 0; i--)
            {
                var behaviorRequirement = sortedConditions[i] as BehavioralRequirementExpression;
                if (behaviorRequirement != null && behaviorRequirement.Behavior == RequirementType.SubHits)
                    continue;

                if (!RequirementClauseExpression.HasHitTarget(sortedConditions[i]))
                    break;
            }

            bool needsAlwaysFalse = false;
            if (i == -1)
            {
                // nothing could be moved, need an always_false clause
                needsAlwaysFalse = true;
            }
            else if (i < sortedConditions.Count - 1)
            {
                var condition = sortedConditions[i];
                sortedConditions.RemoveAt(i);
                sortedConditions.Add(condition);
            }

            bool hasAddHits = false;
            var lastCondition = sortedConditions.LastOrDefault();
            foreach (var condition in sortedConditions)
            {
                var expr = condition as RequirementExpressionBase;
                if (expr == null)
                    return new ErrorExpression("Cannot count " + condition.Type, condition);

                // reset conditions have to be inserted before each tallied condition and the last condition
                var tallied = condition as TalliedRequirementExpression;
                if (tallied != null || ReferenceEquals(condition, lastCondition))
                {
                    error = BuildResetClause(context);
                    if (error != null)
                        return error;
                }

                var reqClause = expr as RequirementClauseExpression;
                if (reqClause != null)
                {
                    var reordered = reqClause.EnsureLastConditionHasNoHitTarget();
                    if (reordered == null)
                    {
                        // could not find a subclause without a hit count
                        // dump the subclause and append an always_false() to hold the total hit count
                        error = expr.BuildSubclauseTrigger(context);
                        if (error != null)
                            return error;

                        context.LastRequirement.Type = RequirementType.AddHits;
                        expr = new AlwaysFalseExpression();
                    }
                    else
                    {
                        expr = reordered;
                    }
                }

                error = expr.BuildSubclauseTrigger(context);
                if (error != null)
                    return error;

                var behaviorRequirement = condition as BehavioralRequirementExpression;
                if (behaviorRequirement != null && behaviorRequirement.Behavior == RequirementType.SubHits)
                {
                    Debug.Assert(context.LastRequirement.Type == RequirementType.SubHits);
                }
                else
                {
                    context.LastRequirement.Type = RequirementType.AddHits;
                    hasAddHits = true;
                }
            }

            if (!hasAddHits)
                return new ErrorExpression("tally requires at least one non-deducted item", this);

            if (needsAlwaysFalse)
            {
                // always add an always false clause if SubHits are present, or if we couldn't
                // find an AddHits clause without a hit target.
                context.Trigger.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());
            }

            context.LastRequirement.Type = RequirementType.None;
            context.LastRequirement.HitCount = HitTarget;
            return null;
        }

        private ErrorExpression BuildResetClause(TriggerBuilderContext context)
        {
            foreach (var condition in ResetConditions)
            {
                var expr = condition as RequirementExpressionBase;
                if (expr == null)
                    return new ErrorExpression("Cannot count " + condition.Type, condition);

                var error = expr.BuildSubclauseTrigger(context);
                if (error != null)
                    return error;

                context.LastRequirement.Type = RequirementType.ResetNextIf;
            }

            return null;
        }

        public override RequirementExpressionBase Optimize(TriggerBuilderContext context)
        {
            bool updated = false;
            var newConditions = new List<ExpressionBase>();

            var tallyContext = new TallyBuilderContext();
            foreach (var condition in Conditions)
            {
                var optimized = condition;

                var expr = condition as RequirementExpressionBase;
                if (expr != null)
                    optimized = expr.Optimize(tallyContext);

                // always false conditions cannot capture hits. no reason to include them in the trigger
                if (optimized is AlwaysFalseExpression)
                {
                    updated = true;
                    continue;
                }

                var behavioral = optimized as BehavioralRequirementExpression;
                if (behavioral != null && behavioral.Behavior == RequirementType.SubHits && 
                    behavioral.Condition is AlwaysFalseExpression)
                {
                    updated = true;
                    continue;
                }

                updated |= !ReferenceEquals(condition, optimized);
                newConditions.Add(optimized);
            }

            var newResetConditions = new List<ExpressionBase>();
            foreach (var condition in ResetConditions)
            {
                var optimized = condition;

                var expr = condition as RequirementExpressionBase;
                if (expr != null)
                    optimized = expr.Optimize(context);

                // always false conditions cannot capture hits. no reason to include them in the trigger
                if (optimized is AlwaysFalseExpression)
                {
                    updated = true;
                    continue;
                }

                updated |= !ReferenceEquals(condition, optimized);
                newResetConditions.Add(optimized);
            }

            if (updated)
            {
                return new TalliedRequirementExpression
                {
                    HitTarget = HitTarget,
                    _conditions = newConditions,
                    _resetConditions = (newResetConditions.Count > 0) ? newResetConditions : null
                };
            }

            return base.Optimize(context);
        }
    }
}
