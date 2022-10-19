using RATools.Data;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class RequirementClauseExpression : RequirementExpressionBase,
        ICloneableExpression
    {
        public RequirementClauseExpression()
            : base()
        {
        }

        public RequirementClauseExpression(RequirementClauseExpression source)
            : this()
        {
            Operation = source.Operation;

            if (source._conditions != null)
            {
                _conditions = new List<ExpressionBase>();
                foreach (var clause in source._conditions)
                    _conditions.Add(clause);
            }
        }

        public ConditionalOperation Operation { get; set; }

        public IEnumerable<ExpressionBase> Conditions
        {
            get { return _conditions ?? Enumerable.Empty<ExpressionBase>(); }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected List<ExpressionBase> _conditions;

        public void AddCondition(ExpressionBase condition)
        {
            if (_conditions == null)
                _conditions = new List<ExpressionBase>();

            _conditions.Add(condition);
        }

        ExpressionBase ICloneableExpression.Clone()
        {
            return Clone();
        }

        public new RequirementClauseExpression Clone()
        {
            return new RequirementClauseExpression(this);
        }

        internal override void AppendString(StringBuilder builder)
        {
            if (_conditions == null || _conditions.Count == 0)
                return;

            for (int i = 0; i < _conditions.Count; i++)
            {
                var condition = _conditions[i];

                if (condition is RequirementClauseExpression)
                {
                    builder.Append('(');
                    condition.AppendString(builder);
                    builder.Append(')');
                }
                else
                {
                    condition.AppendString(builder);
                }

                switch (Operation)
                {
                    case ConditionalOperation.And:
                        builder.Append(" && ");
                        break;

                    case ConditionalOperation.Or:
                        builder.Append(" || ");
                        break;
                }
            }

            builder.Length -= 4; // remove last " && " or " || "
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as RequirementClauseExpression;
            return (that != null && Operation == that.Operation &&
                CompareRequirements(_conditions, that._conditions));
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            if (_conditions == null || _conditions.Count == 0)
                return null;

            if (Operation == ConditionalOperation.Or)
            {
                var achievementContext = context as AchievementBuilderContext;
                if (achievementContext != null)
                    return BuildAlts(achievementContext);

                return BuildTrigger(context, _conditions, RequirementType.OrNext);
/*
                // if there's more than one complex subclause, or a reset with non-reset conditions,
                // we have to use alts
                if (NeedAltsForOr(_conditions))
                {
                    var achievementContext = context as AchievementBuilderContext;
                    if (achievementContext == null)
                        return new ErrorExpression("Cannot logically join multiple subclauses", this);

                    return BuildAlts(achievementContext);
                }

                return BuildTrigger(context, _conditions, RequirementType.OrNext);
*/
            }

            // don't explicitly add AndNext joiners. they will be inserted by CollapseForSubClause if necessary
            return BuildTrigger(context, _conditions, RequirementType.None);
        }

        private static ErrorExpression BuildTrigger(TriggerBuilderContext context, 
            List<ExpressionBase> conditions, RequirementType joinBehavior)
        {
            // no complex subclauses, just dump them
            if (!conditions.OfType<RequirementClauseExpression>().Any())
                return AppendSubclauses(context, conditions, joinBehavior);

            // separate into complex and non-complex
            var complexSubclauses = new List<RequirementClauseExpression>();
            var subclauses = new List<ExpressionBase>();
            foreach (var condition in conditions)
            {
                var clause = condition as RequirementClauseExpression;
                if (clause != null && clause._conditions != null)
                    complexSubclauses.Add(clause);
                else
                    subclauses.Add(condition);
            }

            // if we're attempting to AND one or more OR subclauses at the top level, put them into alts
            if (joinBehavior == RequirementType.None && complexSubclauses.All(c => c.Operation == ConditionalOperation.Or))
            {
                var achievementContext = context as AchievementBuilderContext;
                if (achievementContext != null && achievementContext.Achievement.AlternateRequirements.Count == 0)
                {
                    BubbleUpOrs(complexSubclauses);

                    var altsNeeded = 1;
                    if (complexSubclauses.Count > 1)
                    {
                        foreach (var subclause in complexSubclauses)
                            altsNeeded *= subclause._conditions.Count;
                    }

                    if (altsNeeded < 20)
                    {
                        var error = AppendSubclauses(context, subclauses, joinBehavior);
                        if (error != null)
                            return error;

                        return CrossMultiplyOrs(achievementContext, complexSubclauses);
                    }
                }
            }

            if (joinBehavior != RequirementType.None)
            {
                // one complex clause can be joined to as many non-complex clauses as desired
                // as long as it's the first in the chain. if there are multiple, they can't
                // be joined.
                if (complexSubclauses.Count > 1)
                    return new ErrorExpression("Cannot logically join multiple subclauses");

                subclauses.Insert(0, complexSubclauses[0]);
                return AppendSubclauses(context, subclauses, joinBehavior);
            }

            // keep the AndNext/OrNext chains and proceed
            return AppendSubclauses(context, conditions, joinBehavior);
        }

        private static void BubbleUpOrs(List<RequirementClauseExpression> complexSubclauses)
        {
            // ASSERT: all subclauses are complex ORs
            // If one of them is an AND with nested ORs, we have to expand it
            // (A || (B && (C || D))) => (A || (B && C) || (B && D))
            for (int i = 0; i < complexSubclauses.Count; i++)
            {
                // complexSubclause = (A || (B && (C || D)))
                var complexSubclause = complexSubclauses[i];
                if (!complexSubclause._conditions.Any(c => c is RequirementClauseExpression))
                    continue;

                var newSubclause = new RequirementClauseExpression
                {
                    Operation = complexSubclauses[i].Operation
                };

                foreach (var condition in complexSubclause._conditions)
                {
                    // clause = (B && (C || D)) => (B && C) || (B && D)
                    var clause = condition as RequirementClauseExpression;
                    if (clause == null || !clause.Conditions.Any(c => c is RequirementClauseExpression))
                    {
                        newSubclause.AddCondition(condition);
                        continue;
                    }

                    var indices = new int[clause._conditions.Count];
                    int k;
                    do
                    {
                        var group = new List<ExpressionBase>();
                        for (int j = 0; j < indices.Length; j++)
                        {
                            var subclause = clause._conditions[j];
                            var subclauseClause = subclause as RequirementClauseExpression; 
                            if (subclauseClause == null)
                                group.Add(subclause); // B
                            else
                                group.Add(subclauseClause._conditions[indices[j]]); // C || D
                        }

                        newSubclause.AddCondition(new RequirementClauseExpression
                        {
                            _conditions = group,
                            Operation = ConditionalOperation.And
                        });

                        k = indices.Length - 1;
                        do
                        {
                            var subclause = clause._conditions[k] as RequirementClauseExpression;
                            if (subclause != null)
                            {
                                indices[k]++;
                                if (indices[k] < subclause._conditions.Count)
                                    break;
                            }

                            indices[k--] = 0;
                        } while (k >= 0);
                    } while (k >= 0);
                }

                complexSubclauses[i] = newSubclause;
            }
        }

        private static ErrorExpression AppendSubclauses(TriggerBuilderContext context, List<ExpressionBase> subclauses, RequirementType joinBehavior)
        {
            if (subclauses.Count == 0)
                return null;

            for (int i = 0; i < subclauses.Count - 1; i++)
            {
                var error = BuildSubclauseTrigger(subclauses[i], context);
                if (error != null)
                    return error;

                if (joinBehavior != RequirementType.None)
                {
                    if (context.LastRequirement.Type != RequirementType.None)
                        return new ErrorExpression("Cannot apply " + joinBehavior + " to condition already flagged with " + context.LastRequirement.Type);

                    context.LastRequirement.Type = joinBehavior;
                }
            }

            var lastClause = subclauses.Last();
            return BuildSubclauseTrigger(lastClause, context);
        }

        private static ErrorExpression CrossMultiplyOrs(AchievementBuilderContext context,
            List<RequirementClauseExpression> subclauses)
        {
            ErrorExpression error;
            var triggerContext = new TriggerBuilderContext();
            var indices = new int[subclauses.Count];
            do
            {
                context.BeginAlt();
                triggerContext.Trigger = context.Trigger;

                for (int i = 0; i < indices.Length; i++)
                {
                    var subclause = subclauses[i]._conditions[indices[i]];
                    var clause = subclause as RequirementClauseExpression;
                    if (clause != null)
                        error = BuildTrigger(triggerContext, clause._conditions, RequirementType.None);
                    else
                        error = BuildSubclauseTrigger(subclause, triggerContext);

                    if (error != null)
                        return error;
                }

                var j = indices.Length - 1;
                do
                {
                    indices[j]++;
                    if (indices[j] < subclauses[j]._conditions.Count)
                        break;

                    if (j == 0)
                        return null;

                    indices[j--] = 0;
                } while (true);
            } while (true);
        }

        private static ErrorExpression BuildSubclauseTrigger(ExpressionBase expression, TriggerBuilderContext context)
        {
            var requirement = expression as RequirementExpressionBase;
            if (requirement != null)
                return requirement.BuildSubclauseTrigger(context);

            var trigger = expression as ITriggerExpression;
            if (trigger != null)
            {
                var builder = new AchievementBuilder();
                var subclauseContext = new TriggerBuilderContext { Trigger = builder.CoreRequirements };
                var error = trigger.BuildTrigger(subclauseContext);
                if (error != null)
                    return error;

                var errorMessage = builder.OptimizeForSubClause();
                if (errorMessage != null)
                    return new ErrorExpression(errorMessage, expression);

                error = builder.CollapseForSubClause();
                if (error != null)
                    return error;

                foreach (var c in builder.CoreRequirements)
                    context.Trigger.Add(c);

                return null;
            }

            return new ErrorExpression("Cannot build trigger from " + expression.Type);
        }

        public override ErrorExpression BuildSubclauseTrigger(TriggerBuilderContext subclauseContext,
            ConditionalOperation splitCondition, RequirementType splitBehavior)
        {
            if (_conditions == null)
                return null;

            //if (Operation == ConditionalOperation.Or)
            //{
            //    BehavioralRequirementExpression firstReset = null;
            //    bool hasNonReset = false;
            //    foreach (var condition in _conditions)
            //    {
            //        var behavioral = condition as BehavioralRequirementExpression;
            //        if (behavioral != null && behavioral.Behavior == RequirementType.ResetIf)
            //            firstReset = behavioral;
            //        else
            //            hasNonReset = true;
            //    }

            //    if (firstReset != null)
            //    {
            //        // OR'd ResetIfs are designated to be put into alts. subclauses can't have alts

            //        // ResetIf(A) || B => assume B has a PauseIf that's being protected from the ResetIf
            //        // we can't use an alt to segregate that, so generate an error
            //        if (hasNonReset)
            //            return new ErrorExpression("never not allowed in OR subclause", firstReset);

            //        // ResetIf(A) || ResetIf(B)  =>  ResetIf(A) && ResetIf(B)
            //        // all conditions are ResetIfs, so they can be joined via ANDs in the subclause
            //        return BuildTrigger(subclauseContext, _conditions, RequirementType.None);
            //    }
            //}

            var achievementContext = new AchievementBuilderContext();
            ErrorExpression error;

            if (Operation == ConditionalOperation.Or && NeedAltsForOr(_conditions))
            {
                error = BuildAlts(achievementContext);
            }
            else
            {
                if (splitBehavior == RequirementType.None && Operation != splitCondition)
                    splitBehavior = (Operation == ConditionalOperation.Or) ? RequirementType.OrNext : RequirementType.AndNext;

                error = BuildTrigger(achievementContext, _conditions, splitBehavior);
            }

            if (error != null)
                return error;

            var errorMessage = achievementContext.Achievement.OptimizeForSubClause();
            if (errorMessage != null)
                return new ErrorExpression(errorMessage, this);

            error = achievementContext.Achievement.CollapseForSubClause();
            if (error != null)
                return error;

            if (achievementContext.Achievement.AlternateRequirements.Count > 0)
                return new ErrorExpression("Combination of &&s and ||s is too complex for subclause", this);

            foreach (var requirement in achievementContext.Achievement.CoreRequirements)
                subclauseContext.Trigger.Add(requirement);

            return null;
        }

        private static bool NeedAltsForOr(IEnumerable<ExpressionBase> conditions)
        {
            bool seenAndNext = false;
            //bool seenReset = false;
            //bool seenNonReset = false;
            foreach (var condition in conditions)
            {
                var clause = condition as RequirementClauseExpression;
                if (clause != null)
                {
                    // if more than one AND is present, we need to use alts
                    if (seenAndNext)
                        return true;

                    seenAndNext = (clause.Operation == ConditionalOperation.And);
                }
                //else
                //{
                //    var behavior = condition as BehavioralRequirementExpression;
                //    if (behavior != null && behavior.Behavior == RequirementType.ResetIf)
                //        seenReset = true;
                //    else
                //        seenNonReset = true;
                //}
            }

            // ResetIf(A) || B => assume B has a PauseIf that's being separated from the ResetIf
            //if (seenReset && seenNonReset)
            //    return true;

            return false;
        }

        private ErrorExpression BuildAlts(AchievementBuilderContext context)
        {
            var triggerContext = new TriggerBuilderContext();
            foreach (var condition in Conditions)
            {
                context.BeginAlt();
                triggerContext.Trigger = context.Trigger;

                // Since we're creating alt groups, we don't need to call BuildSubclauseTrigger.
                var requirement = condition as ITriggerExpression;
                if (requirement != null)
                {
                    var error = requirement.BuildTrigger(triggerContext);
                    if (error != null)
                        return error;
                }
            }

            return null;
        }

        public override RequirementExpressionBase Optimize(TriggerBuilderContext context)
        {
            if (_conditions == null || _conditions.Count == 0)
                return this;

            bool updated = false;
            var newRequirements = new List<ExpressionBase>(_conditions.Count);

            foreach (var requirement in _conditions)
            {
                var requirementExpression = requirement as RequirementExpressionBase;
                if (requirementExpression != null)
                {
                    var optimized = requirementExpression.Optimize(context);
                    newRequirements.Add(optimized);
                    updated |= !ReferenceEquals(optimized, requirement);

                    if (optimized is AlwaysTrueExpression)
                    {
                        if (Operation == ConditionalOperation.Or)
                        {
                            newRequirements.Clear();
                            newRequirements.Add(optimized);
                            updated = true;
                            break;
                        }
                    }
                    else if (optimized is AlwaysFalseExpression)
                    {
                        if (Operation == ConditionalOperation.And)
                        {
                            newRequirements.Clear();
                            newRequirements.Add(optimized);
                            updated = true;
                            break;
                        }
                    }
                }
                else
                {
                    newRequirements.Add(requirement);
                }
            }

            if (newRequirements.Count < 100)
                updated |= EliminateRedundantConditions(newRequirements, Operation);

            updated |= FlattenClauses(newRequirements, Operation);

            if (newRequirements.Count == 1 && newRequirements[0] is RequirementExpressionBase)
                return (RequirementExpressionBase)newRequirements[0];

            if (!updated)
                return this;

            var newClause = new RequirementClauseExpression { Operation = Operation };
            newClause._conditions = newRequirements;
            return newClause;
        }

        private static bool EliminateRedundantConditions(IList<ExpressionBase> requirements, ConditionalOperation condition)
        {
            bool updated = false;

            for (int i = requirements.Count - 1; i > 0; i--)
            {
                var conditionI = requirements[i] as RequirementConditionExpression;
                if (conditionI != null)
                {
                    // singular condition, see if it can be handled by any of the other conditions
                    for (int j = 0; j < requirements.Count - 1; j++)
                    {
                        if (i == j)
                            continue;

                        var requirementJ = requirements[j] as RequirementExpressionBase;
                        if (requirementJ == null)
                            continue;

                        {
                            var intersect = requirementJ.LogicalIntersect(conditionI, condition);
                            if (intersect != null)
                            {
                                requirements[j] = intersect;
                                requirements.RemoveAt(i);
                                updated = true;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // complex condition. only merge if it's an exact match
                    for (int j = 0; j < requirements.Count - 1; j++)
                    {
                        if (i == j)
                            continue;

                        if (requirements[j] == requirements[i])
                        {
                            requirements.RemoveAt(i);
                            updated = true;
                            break;
                        }
                    }
                }
            }

            return updated;
        }

        private static bool FlattenClauses(IList<ExpressionBase> requirements, ConditionalOperation condition)
        {
            bool updated = false;
            for (int i = requirements.Count - 1; i >= 0; i--)
            {
                var requirementI = requirements[i] as RequirementClauseExpression;
                if (requirementI == null || requirementI.Operation != condition)
                    continue;

                requirements.RemoveAt(i);
                for (int j = requirementI._conditions.Count - 1; j >= 0; j--)
                    requirements.Insert(i, requirementI._conditions[j]);
            }

            return updated;
        }

        private static bool GetAddresses(HashSet<uint> addresses, ExpressionBase expression, bool populate)
        {
            switch (expression.Type)
            {
                case ExpressionType.Requirement:
                    var clause = expression as RequirementClauseExpression;
                    if (clause != null)
                    {
                        foreach (var condition in clause.Conditions)
                        {
                            if (!GetAddresses(addresses, condition, populate))
                                return false;
                        }
                    }
                    var conditionRequirement = expression as RequirementConditionExpression;
                    if (conditionRequirement != null)
                        return GetAddresses(addresses, conditionRequirement.Left, populate) &&
                               GetAddresses(addresses, conditionRequirement.Right, populate);
                    break;

                case ExpressionType.MemoryValue:
                    var memoryValue = (MemoryValueExpression)expression;
                    foreach (var accessor in memoryValue.MemoryAccessors)
                    {
                        if (!GetAddresses(addresses, accessor, populate))
                            return false;
                    }
                    break;

                case ExpressionType.ModifiedMemoryAccessor:
                    expression = ((ModifiedMemoryAccessorExpression)expression).MemoryAccessor;
                    goto case ExpressionType.MemoryAccessor;

                case ExpressionType.MemoryAccessor:
                    var memoryAccessor = (MemoryAccessorExpression)expression;
                    var address = memoryAccessor.Field.Value;
                    if (memoryAccessor.HasPointerChain)
                        address = memoryAccessor.PointerChain.First().Left.Value;
                    else if (!memoryAccessor.Field.IsMemoryReference)
                        break;

                    if (populate)
                        addresses.Add(address);
                    else if (!addresses.Contains(address))
                        return false;

                    break;
            }

            return true;
        }

        public override RequirementExpressionBase LogicalIntersect(RequirementExpressionBase that, ConditionalOperation condition)
        {
            var thatClause = that as RequirementClauseExpression;
            if (thatClause != null)
            {
                if (thatClause.Operation != Operation)
                    return null;

                if (_conditions == null || thatClause._conditions == null)
                    return null;

                if (thatClause._conditions.Count > _conditions.Count)
                    return thatClause.LogicalIntersect(this, condition);

                // ASSERT: _conditions.Count >= thatClause._conditions.Count;
                var sharedConditions = new List<ExpressionBase>(_conditions.Count);
                var unsharedConditions = new List<ExpressionBase>();
                bool updated = false;

                for (int i = 0; i < _conditions.Count; i++)
                    sharedConditions.Add(null);

                // both sides use the same set of addresses, see if their logics can be combined
                // (A || B || C) && (A || B) => (A || B)
                // (A || B || C) || (A || B) => (A || B || C)
                // (A && B && C) && (A && B) => (A && B && C)
                // (A && B && C) || (A && B) => (A && B)
                // (A && B) || (A && C) => A && (B || C)

                // first pass - look for exact matches
                foreach (var thatCondition in thatClause.Conditions)
                {
                    var found = false;
                    for (int i = 0; i < sharedConditions.Count; i++)
                    {
                        if (sharedConditions[i] == null && thatCondition == _conditions[i])
                        {
                            sharedConditions[i] = _conditions[i];
                            updated = true;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        unsharedConditions.Add(thatCondition);
                }

                // second pass - if there's a single unmatched item, try to merge it
                if (unsharedConditions.Count == 1)
                {
                    var unsharedExpr = unsharedConditions[0] as RequirementExpressionBase;
                    if (unsharedExpr != null)
                    {
                        for (int i = 0; i < sharedConditions.Count; i++)
                        {
                            if (sharedConditions[i] != null)
                                continue;

                            var exprI = _conditions[i] as RequirementExpressionBase;
                            if (exprI != null)
                            {
                                var merged = exprI.LogicalIntersect(unsharedExpr, condition);
                                if (merged != null)
                                {
                                    sharedConditions[i] = merged;
                                    unsharedConditions.Clear();
                                    updated = true;
                                    break;
                                }
                            }

                            if (unsharedConditions.Count == 0)
                                break;
                        }
                    }
                }

                // if no expressions were merged, we're done
                if (!updated)
                    return null;

                if (unsharedConditions.Count == 0)
                {
                    // if all items were matched, copy over the remaining items from this
                    for (int i = 0; i < sharedConditions.Count; i++)
                    {
                        if (sharedConditions[i] == null)
                            sharedConditions[i] = _conditions[i];
                    }
                }
                else
                {
                    //   (A && B) || (A && C) => A && (B || C)
                    //   (A || B) && (A || C) => A || (B && C)
                    //    A = sharedConditions.Where( ! null )
                    //    B = conditions.Where(sharedCondition == null)
                    //    C = unsharedConditions
                    var bConditions = new List<ExpressionBase>();
                    for (int i = 0; i < sharedConditions.Count; i++)
                    {
                        if (sharedConditions[i] == null)
                            bConditions.Add(_conditions[i]);
                    }

                    if (bConditions.Count == 0)
                    {
                        // if all items were matched, copy over the remaining items from that
                        sharedConditions.AddRange(unsharedConditions);
                        updated = true;
                    }
                    else
                    {
                        ExpressionBase b, c;
                        sharedConditions.RemoveAll(c => c == null);

                        if (unsharedConditions.Count > 1)
                        {
                            c = new RequirementClauseExpression
                            {
                                Operation = ConditionalOperation.And,
                                _conditions = unsharedConditions
                            };
                        }
                        else
                        {
                            c = unsharedConditions[0];
                        }

                        if (bConditions.Count > 1)
                        {
                            b = new RequirementClauseExpression
                            {
                                Operation = ConditionalOperation.And,
                                _conditions = bConditions
                            };
                        }
                        else
                        {
                            b = bConditions[0];
                        }

                        var bOrC = new RequirementClauseExpression
                        {
                            Operation = ConditionalOperation.Or,
                            _conditions = new List<ExpressionBase>() { b, c }
                        };

                        var a = new RequirementClauseExpression { Operation = ConditionalOperation.And };
                        a._conditions = sharedConditions;
                        a._conditions.Add(bOrC);
                        return a;
                    }
                }

                var newClause = new RequirementClauseExpression { Operation = Operation };
                newClause._conditions = sharedConditions;
                return newClause;
            }

            if (_conditions.OfType<RequirementExpressionBase>().Any(c => c == that))
            {
                // (A && B) && B  =>  A && B
                // (A || B) || B  =>  A || B
                // (A || B) && B  =>  B
                // (A && B) || B  =>  B 
                return (condition == Operation) ? this : that;
            }

            return base.LogicalIntersect(that, condition);
        }

        /// <summary>
        /// Combines the current expression with the <paramref name="right"/> expression using the <paramref name="operation"/> operator.
        /// </summary>
        /// <param name="right">The expression to combine with the current expression.</param>
        /// <param name="operation">How to combine the expressions.</param>
        /// <returns>
        /// An expression representing the combined values on success, or <c>null</c> if the expressions could not be combined.
        /// </returns>
        public override ExpressionBase Combine(ExpressionBase right, ConditionalOperation operation)
        {
            if (operation == ConditionalOperation.Not)
                return InvertLogic();

            var requirement = right as RequirementExpressionBase;
            if (requirement == null)
                return null;

            RequirementClauseExpression newClause;
            if (operation == Operation)
            {
                newClause = Clone();
            }
            else
            {
                newClause = new RequirementClauseExpression();
                newClause.Operation = operation;
                newClause.AddCondition(this);
            }

            var clause = right as RequirementClauseExpression;
            if (clause != null && clause.Operation == operation)
            {
                foreach (var condition in clause.Conditions)
                    newClause.AddCondition(condition);
            }
            else
            {
                newClause.AddCondition(requirement);
            }

            return newClause;
        }

        public override RequirementExpressionBase InvertLogic()
        {
            var clause = new RequirementClauseExpression();
            clause.Operation = Operation;
            clause.InvertOperator();

            if (_conditions != null)
            {
                clause._conditions = new List<ExpressionBase>();
                foreach (var condition in _conditions)
                {
                    var requirement = condition as RequirementExpressionBase;
                    if (requirement == null)
                        return null;

                    var inverted = requirement.InvertLogic();
                    if (inverted == null)
                        return null;

                    clause._conditions.Add(inverted);
                }
            }

            return clause;
        }

        public void InvertOperator()
        {
            switch (Operation)
            {
                case ConditionalOperation.And:
                    Operation = ConditionalOperation.Or;
                    break;

                case ConditionalOperation.Or:
                    Operation = ConditionalOperation.And;
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
