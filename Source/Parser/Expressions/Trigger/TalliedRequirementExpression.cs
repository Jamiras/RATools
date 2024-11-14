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
                _conditions = new List<RequirementExpressionBase>(source._conditions);
        }

        public uint HitTarget { get; set; }

        public IEnumerable<RequirementExpressionBase> Conditions
        {
            get { return _conditions ?? Enumerable.Empty<RequirementExpressionBase>(); }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected List<RequirementExpressionBase> _conditions;

        public void AddTalliedCondition(RequirementExpressionBase condition)
        {
            if (_conditions == null)
                _conditions = new List<RequirementExpressionBase>();

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

        public RequirementExpressionBase ResetCondition { get; set; }

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

                if (ResetCondition != null)
                {
                    builder.Append(" && never(");
                    ResetCondition.AppendString(builder);
                    builder.Append(')');
                }
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

                if (ResetCondition != null)
                {
                    builder.Append(" && never(");
                    ResetCondition.AppendString(builder);
                    builder.Append(')');
                }

                builder.Append(')');
            }
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as TalliedRequirementExpression;
            return (that != null && HitTarget == that.HitTarget &&
                CompareRequirements(_conditions, that._conditions) &&
                ResetCondition == that.ResetCondition);
        }

        public override RequirementExpressionBase Optimize(TriggerBuilderContext context)
        {
            bool updated = false;
            var newConditions = new List<RequirementExpressionBase>();

            var tallyContext = new TallyBuilderContext();
            foreach (var condition in Conditions)
            {
                var optimized = condition.Optimize(tallyContext);

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

                // only invert if it's an singular behavioral clause
                if (behavioral != null)
                {
                    // never(A) => !A
                    // unless(A) => !A
                    optimized = behavioral.InvertResetsAndPauses() ?? behavioral;
                    updated |= !ReferenceEquals(behavioral, optimized);
                }

                updated |= !ReferenceEquals(condition, optimized);
                newConditions.Add(optimized);
            }

            RequirementExpressionBase newResetCondition = null;
            if (ResetCondition != null)
            {
                var optimized = ResetCondition.Optimize(context);

                // always false conditions cannot capture hits. no reason to include them in the trigger
                if (optimized is AlwaysFalseExpression)
                {
                    updated = true;
                }
                else
                {
                    updated |= !ReferenceEquals(ResetCondition, optimized);
                    newResetCondition = optimized;
                }
            }

            if (newResetCondition == null && newConditions.Count == 1)
            {
                var nestedTally = newConditions[0] as TalliedRequirementExpression;
                if (nestedTally != null)
                {
                    // once(once(A)) => once(A)   repeated(3, repeated(2, A)) => repeated(6, A)
                    var clone = nestedTally.Clone();
                    clone.HitTarget *= HitTarget;
                    CopyLocation(clone);
                    return clone;
                }

                var nestedBehavior = newConditions[0] as BehavioralRequirementExpression;
                if (nestedBehavior != null)
                {
                    // repeated(3, never(A))  =>  never(repeated(3, A))
                    var newTally = new TalliedRequirementExpression
                    {
                        HitTarget = HitTarget,
                        _conditions = new List<RequirementExpressionBase>() { nestedBehavior.Condition }
                    };
                    return new BehavioralRequirementExpression
                    {
                        Behavior = nestedBehavior.Behavior,
                        Condition = newTally,
                        Location = Location
                    };
                }
            }

            if (updated)
            {
                if (newConditions.Count == 0)
                    return new AlwaysFalseExpression();

                return new TalliedRequirementExpression
                {
                    HitTarget = HitTarget,
                    _conditions = newConditions,
                    ResetCondition = newResetCondition,
                    Location = Location
                };
            }

            return base.Optimize(context);
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            if (HitTarget == 0 && context is not ValueBuilderContext)
                return new ErrorExpression("Unbounded count is only supported in measured value expressions", this);

            ErrorExpression error;

            var sortedConditions = new List<RequirementExpressionBase>(Conditions);
            int i = sortedConditions.Count - 1;
            for (; i >= 0; i--)
            {
                var behaviorRequirement = sortedConditions[i] as BehavioralRequirementExpression;
                if (behaviorRequirement != null && behaviorRequirement.Behavior == RequirementType.SubHits)
                    continue;

                if (!RequirementClauseExpression.AllClausesHaveHitTargets(sortedConditions[i]))
                    break;
            }

            bool needsAlwaysFalse = false;
            if (i == -1)
            {
                if (HitTarget == 1 && sortedConditions.Count == 1)
                {
                    // once(once(A) && repeated(3, B))  =>  once(A) && repeated(3, B)
                    var clause = sortedConditions[0] as RequirementClauseExpression;
                    if (clause != null && clause.Operation == ConditionalOperation.And &&
                        clause.Conditions.All(c => c is TalliedRequirementExpression))
                    {
                        return clause.BuildSubclauseTrigger(context);
                    }
                }

                // nothing could be moved, need an always_false clause
                needsAlwaysFalse = true;
            }
            else if (i < sortedConditions.Count - 1)
            {
                var condition = sortedConditions[i];
                sortedConditions.RemoveAt(i);
                sortedConditions.Add(condition);
            }

            if (sortedConditions.Count == 1)
            {
                // once() or repeated(). try to rearrange the clauses in the condition so
                // the last clause doesn't have a hit target and can be used to hold the total
                var reqClause = sortedConditions[0] as RequirementClauseExpression;
                if (reqClause != null)
                {
                    var reordered = reqClause.EnsureLastConditionHasNoHitTarget();
                    if (reordered != null)
                    {
                        sortedConditions[0] = reordered;
                    }
                    else
                    {
                        // could not find a subclause without a hit count, we'll need
                        // an always_false() to hold the total
                        needsAlwaysFalse = true;
                    }
                }
            }
            else
            {
                // if this is a tally(), we aren't going to reorder the requirements within
                // the individual clauses. look for a clause that doesn't have a hit target,
                // and move it to the end. otherwise, use an always_false() for the total.
                bool found = false;
                for (i = sortedConditions.Count - 1; i >= 0; i--)
                {
                    var condition = sortedConditions[i];
                    if (!RequirementClauseExpression.LastClauseHasHitTarget(condition))
                    {
                        sortedConditions.RemoveAt(i);
                        sortedConditions.Add(condition);
                        found = true;
                        break;
                    }
                }

                needsAlwaysFalse |= !found;
            }

            bool hasAddHits = false;
            foreach (var condition in sortedConditions)
            {
                // reset conditions have to be inserted before each tallied condition and the last condition
                if (ResetCondition != null)
                {
                    error = ResetCondition.BuildSubclauseTrigger(context);
                    if (error != null)
                        return error;

                    context.LastRequirement.Type = RequirementType.ResetNextIf;
                }

                error = condition.BuildSubclauseTrigger(context);
                if (error != null)
                    return new ErrorExpression("Cannot tally expression", this) { InnerError = error };

                var behaviorRequirement = condition as BehavioralRequirementExpression;
                if (behaviorRequirement != null && behaviorRequirement.Behavior == RequirementType.SubHits)
                {
                    Debug.Assert(context.LastRequirement.Type == RequirementType.SubHits);
                    // if there's a SubHits, we need an always_false clause for the total hits as the
                    // individual hits can potentially exceed the total.
                    needsAlwaysFalse = true;
                }
                else
                {
                    // if there was an explicit always_true() in the clause, it will likely have been
                    // optimized out. if the preceding item has a hitcount, add it back for the new hitcount
                    if (context.LastRequirement.HitCount > 0)
                    {
                        var clause = condition as RequirementClauseExpression;
                        if (clause != null && clause.Operation == ConditionalOperation.And && clause.Conditions.Last() is AlwaysTrueExpression)
                        {
                            context.LastRequirement.Type = RequirementType.AndNext;
                            context.Trigger.Add(AlwaysTrueFunction.CreateAlwaysTrueRequirement());
                        }
                    }

                    context.LastRequirement.Type = RequirementType.AddHits;
                    hasAddHits = true;
                }
            }

            if (!hasAddHits)
                return new ErrorExpression("tally requires at least one non-deducted item", this);

            if (needsAlwaysFalse)
            {
                // always add an always false clause if SubHits are present,
                // or if we couldn't find an AddHits clause without a hit target.
                context.Trigger.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());
            }

            context.LastRequirement.Type = RequirementType.None;
            context.LastRequirement.HitCount = HitTarget;

            return null;
        }

        /// <summary>
        /// Returns an expression where any 'never(A)'s have been converted to '!A's
        /// </summary>
        /// <returns>New requirement, or <c>null</c> if the requirement cannot be inverted.</returns>
        /// <remarks>May return the original expression if nothing needed to be converted</remarks>
        public override RequirementExpressionBase InvertResetsAndPauses()
        {
            if (_conditions != null && _conditions.Count == 1)
            {
                var expr = _conditions[0];
                var inverted = expr.InvertResetsAndPauses();
                if (!ReferenceEquals(inverted, expr))
                {
                    return new TalliedRequirementExpression
                    {
                        HitTarget = HitTarget,
                        _conditions = new List<RequirementExpressionBase> { inverted },
                        Location = Location
                    };
                }
            }

            return base.InvertResetsAndPauses();
        }

        public override RequirementExpressionBase LogicalIntersect(RequirementExpressionBase that, ConditionalOperation condition)
        {
            if (_conditions == null)
                return null;

            var thatTallied = that as TalliedRequirementExpression;
            if (thatTallied == null)
                return null;

            if (_conditions.Count == 1 && thatTallied._conditions.Count == 1)
            {
                var leftExpression = _conditions[0];
                var rightExpression = thatTallied._conditions[0];

                var intersect = leftExpression.LogicalIntersect(rightExpression, ConditionalOperation.Or);
                if (intersect == null)
                    return null;

                if (thatTallied.HitTarget == HitTarget)
                {
                    if (ReferenceEquals(intersect, leftExpression))
                        return this;
                    if (ReferenceEquals(intersect, rightExpression))
                        return that;

                    return new TalliedRequirementExpression
                    {
                        HitTarget = HitTarget,
                        _conditions = new List<RequirementExpressionBase> { intersect },
                        Location = Location,
                    };
                }

                if (ReferenceEquals(intersect, leftExpression) && HitTarget > thatTallied.HitTarget)
                {
                    return new TalliedRequirementExpression
                    {
                        HitTarget = thatTallied.HitTarget,
                        _conditions = new List<RequirementExpressionBase> { leftExpression },
                        Location = Location,
                    };
                }

                if (ReferenceEquals(intersect, rightExpression) && HitTarget < thatTallied.HitTarget)
                {
                    return new TalliedRequirementExpression
                    {
                        HitTarget = HitTarget,
                        _conditions = new List<RequirementExpressionBase> { rightExpression },
                        Location = Location,
                    };
                }
            }
            else
            {
                // TODO: tallied
            }

            return base.LogicalIntersect(that, condition);
        }
    }
}
