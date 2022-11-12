﻿using RATools.Data;
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
        private static readonly int PreferAltsThreshold = 20;

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
            }

            // don't explicitly add AndNext joiners. they will be inserted by CollapseForSubClause if necessary
            return BuildTrigger(context, _conditions, RequirementType.None);
        }

        private ErrorExpression BuildTrigger(TriggerBuilderContext context, 
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
                {
                    if (clause is OrNextRequirementClauseExpression && joinBehavior == RequirementType.None)
                        subclauses.Add(condition);
                    else
                        complexSubclauses.Add(clause);
                }
                else
                {
                    subclauses.Add(condition);
                }
            }

            // only complex subclauses were empty or OrNexts, just dump everything
            if (complexSubclauses.Count == 0)
                return AppendSubclauses(context, conditions, joinBehavior);

            // if we're attempting to AND one or more OR subclauses at the top level, put them into alts
            if (joinBehavior == RequirementType.None && complexSubclauses.All(c => c.Operation == ConditionalOperation.Or))
            {
                if (complexSubclauses.Count == 1 && complexSubclauses[0] is OrNextRequirementClauseExpression)
                {
                    // single explicit OrNext should be joined to any other conditions via AndNext
                    joinBehavior = RequirementType.AndNext;
                }
                else
                {
                    // no explicit OrNexts. if building an achievement, put the ORs into alts
                    var achievementContext = context as AchievementBuilderContext;
                    if (achievementContext != null && achievementContext.Achievement.AlternateRequirements.Count == 0)
                    {
                        BubbleUpOrs(complexSubclauses);
                        CheckExpansionThreshold(complexSubclauses);

                        var error = AppendSubclauses(context, subclauses, joinBehavior);
                        if (error != null)
                            return error;

                        if (complexSubclauses.Count == 1)
                        {
                            var clause = complexSubclauses[0];
                            if (clause.Operation == ConditionalOperation.Or)
                                return clause.BuildAlts(achievementContext);
                        }

                        return CrossMultiplyOrs(achievementContext, complexSubclauses);
                    }
                }
            }

            if (joinBehavior == RequirementType.AndNext || joinBehavior == RequirementType.OrNext)
            {
                // one complex clause can be joined to as many non-complex clauses as desired
                // as long as it's the first in the chain. if there are multiple, they can't
                // be joined.
                if (complexSubclauses.Count > 1)
                    return new ErrorExpression("Cannot logically join multiple subclauses", this);

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
                    Operation = complexSubclause.Operation,
                    Location = complexSubclause.Location
                };

                bool updated = false;
                foreach (var condition in complexSubclause._conditions)
                    updated |= BubbleUpOrs(newSubclause, condition);

                if (updated)
                    complexSubclauses[i] = newSubclause;
            }
        }

        private static bool BubbleUpOrs(RequirementClauseExpression newSubclause, ExpressionBase condition)
        {
            var clause = condition as RequirementClauseExpression;
            if (clause == null || !clause.Conditions.Any(c => c is RequirementClauseExpression))
            {
                // no subclauses, nothing to bubble up
                newSubclause.AddCondition(condition);
                return false;
            }

            if (clause._conditions.Count == 2 && clause._conditions.Any(c => c is RequirementConditionExpression))
            {
                var secondaryClause = clause._conditions.OfType<RequirementClauseExpression>().First();
                if (secondaryClause != null && !secondaryClause.Conditions.Any(c => c is RequirementClauseExpression))
                {
                    // only two subclauses. one is just a single condition, and the other is made
                    // up entirely of single conditions. they can be joined using AndNext/OrNext
                    newSubclause.AddCondition(condition);
                    return false;
                }
            }

            var expanded = new List<ExpressionBase>();
            foreach (var subclause in clause._conditions)
            {
                var subclauseClause = subclause as RequirementClauseExpression;
                if (subclauseClause != null && subclauseClause.Operation == ConditionalOperation.Or &&
                    subclauseClause.Conditions.OfType<RequirementClauseExpression>().Any(c => 
                        c.Operation == ConditionalOperation.And && c.Conditions.OfType<RequirementClauseExpression>().Any(c2 => c2.Operation == ConditionalOperation.Or)))
                {
                    // at least one nested Or clause, need to bubble it up
                    var newClause = new RequirementClauseExpression() { Operation = ConditionalOperation.Or };
                    foreach (var subclauseCondition in subclauseClause.Conditions)
                        BubbleUpOrs(newClause, subclauseCondition);

                    expanded.Add(newClause);
                }
                else
                {
                    var requirement = subclause as RequirementExpressionBase;
                    if (requirement != null)
                    {
                        bool foundIntersect = false;
                        for (int i = 0; i < expanded.Count; i++)
                        {
                            var requirementI = expanded[i] as RequirementExpressionBase;
                            if (requirementI != null)
                            {
                                var intersect = requirement.LogicalIntersect(requirementI, ConditionalOperation.And);
                                if (intersect != null)
                                {
                                    expanded[i] = intersect;
                                    foundIntersect = true;
                                    break;
                                }
                            }
                        }

                        if (foundIntersect)
                            continue;
                    }

                    expanded.Add(subclause);
                }
            }

            CheckExpansionThreshold(expanded);

            // clause = (B && (C || D)) => (B && C) || (B && D)
            var indices = new int[expanded.Count];
            int k;
            do
            {
                var group = new List<ExpressionBase>();
                for (int j = 0; j < indices.Length; j++)
                {
                    var subclause = expanded[j];
                    var subclauseClause = subclause as RequirementClauseExpression;
                    if (subclauseClause == null || subclause is OrNextRequirementClauseExpression)
                    {
                        group.Add(subclause); // B
                    }
                    else
                    {
                        // capture the appropriate subclause for this iteration of the expansion
                        var loopSubclause = subclauseClause._conditions[indices[j]]; // C or D (depending on iteration)

                        subclauseClause = loopSubclause as RequirementClauseExpression;
                        if (subclauseClause != null && subclauseClause.Operation == ConditionalOperation.And)
                        {
                            // subclause is another set of ANDed conditions, merge instead of nesting it
                            group.AddRange(subclauseClause.Conditions);
                        }
                        else
                        {
                            group.Add(loopSubclause);
                        }
                    }
                }

                newSubclause.AddCondition(new RequirementClauseExpression
                {
                    _conditions = group,
                    Operation = ConditionalOperation.And,
                    Location = group[0].Location
                });

                k = indices.Length - 1;
                do
                {
                    var subclause = expanded[k] as RequirementClauseExpression;
                    if (subclause != null && subclause is not OrNextRequirementClauseExpression)
                    {
                        indices[k]++;
                        if (indices[k] < subclause._conditions.Count)
                            break;
                    }

                    indices[k--] = 0;
                } while (k >= 0);
            } while (k >= 0);

            return true;
        }

        private static void CheckExpansionThreshold<T>(List<T> clauses)
            where T : ExpressionBase
        {
            long expansionSize = 1;
            foreach (var subclause in clauses.OfType<RequirementClauseExpression>())
                expansionSize *= subclause._conditions.Count;

            if (expansionSize >= PreferAltsThreshold)
            {
                for (int i = 0; i < clauses.Count; i++)
                {
                    var subclause = clauses[i] as RequirementClauseExpression;
                    if (subclause == null || subclause is OrNextRequirementClauseExpression)
                        continue;

                    if (HasMultipleComplexSubclauses(subclause))
                        continue;

                    var orNext = new OrNextRequirementClauseExpression { Location = subclause.Location };
                    foreach (var c in subclause.Conditions)
                        orNext.AddCondition(c);

                    clauses[i] = orNext as T;
                    expansionSize /= subclause.Conditions.Count();
                }
            }
        }

        private static bool HasMultipleComplexSubclauses(RequirementClauseExpression clause)
        {
            bool hasComplexSubclause = false;
            foreach (var condition in clause.Conditions.OfType<RequirementExpressionBase>())
            {
                if (HasComplexSubclause(condition))
                {
                    if (hasComplexSubclause)
                        return true;

                    hasComplexSubclause = true;
                }
            }

            return false;
        }

        private static bool HasComplexSubclause(RequirementExpressionBase expression)
        {
            var clause = expression as RequirementClauseExpression;
            if (clause != null)
                return clause.Conditions.Count() > 1;

            var behavioral = expression as BehavioralRequirementExpression;
            if (behavioral != null)
                return HasComplexSubclause(behavioral.Condition);

            var tallied = expression as TalliedRequirementExpression;
            if (tallied != null)
                return tallied.Conditions.OfType<RequirementExpressionBase>().Any(c => HasComplexSubclause(c));

            return false;
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
                    {
                        return new ErrorExpression("Cannot apply " +
                            BehavioralRequirementExpression.GetFunctionName(joinBehavior) +
                            " to condition already flagged with " +
                            BehavioralRequirementExpression.GetFunctionName(context.LastRequirement.Type), subclauses[i]);
                    }

                    context.LastRequirement.Type = joinBehavior;
                }
            }

            var lastClause = subclauses.Last();
            return BuildSubclauseTrigger(lastClause, context);
        }

        private ErrorExpression CrossMultiplyOrs(AchievementBuilderContext context,
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

                var resetRequirements = new List<ExpressionBase>();
                var conditions = new List<ExpressionBase>(_conditions.Count);
                foreach (var condition in _conditions)
                {
                    var behavioral = condition as BehavioralRequirementExpression;
                    if (behavioral != null && behavioral.Behavior == RequirementType.ResetIf)
                    {
                        resetRequirements.Add(new BehavioralRequirementExpression
                        {
                            Behavior = RequirementType.ResetNextIf,
                            Condition = behavioral.Condition,
                            Location = behavioral.Location
                        });
                    }
                    else
                    {
                        conditions.Add(condition);
                    }
                }

                if (resetRequirements.Count > 0)
                {
                    error = BuildTrigger(achievementContext, resetRequirements, RequirementType.None);
                    if (error != null)
                        return error;
                }

                error = BuildTrigger(achievementContext, conditions, splitBehavior);
                if (error != null)
                    return error;

                if (splitBehavior != RequirementType.None && Operation == splitCondition && achievementContext.LastRequirement.Type == RequirementType.None)
                    achievementContext.LastRequirement.Type = splitBehavior;
            }

            if (error != null)
                return error;

            var errorMessage = achievementContext.Achievement.OptimizeForSubClause();
            if (errorMessage != null)
                return new ErrorExpression(errorMessage, this);

            error = achievementContext.Achievement.CollapseForSubClause();
            if (error != null)
                return new ErrorExpression(error.Message, this);

            if (achievementContext.Achievement.AlternateRequirements.Count > 0)
                return new ErrorExpression("Combination of &&s and ||s is too complex for subclause", this);

            foreach (var requirement in achievementContext.Achievement.CoreRequirements)
                subclauseContext.Trigger.Add(requirement);

            return null;
        }

        private static bool NeedAltsForOr(IEnumerable<ExpressionBase> conditions)
        {
            bool seenAndNext = false;
            foreach (var condition in conditions)
            {
                var clause = condition as RequirementClauseExpression;
                if (clause == null)
                {
                    var behavior = condition as BehavioralRequirementExpression;
                    if (behavior != null)
                        clause = behavior.Condition as RequirementClauseExpression;

                    var tallied = condition as TalliedRequirementExpression;
                    if (tallied != null)
                    {
                        if (NeedAltsForOr(tallied.Conditions))
                            return true;

                        clause = tallied.Conditions.FirstOrDefault() as RequirementClauseExpression;
                    }
                }

                if (clause != null)
                {
                    // if more than one AND is present, we need to use alts
                    if (seenAndNext)
                        return true;

                    seenAndNext = (clause.Operation == ConditionalOperation.And);
                }
            }

            return false;
        }

        private ErrorExpression BuildAlts(AchievementBuilderContext context)
        {
            // (A || (B && (C || D)))  =>  (A || (B && C) || (B && D))
            var newClause = new RequirementClauseExpression() { Operation = ConditionalOperation.Or };
            newClause.Location = Location;
            foreach (var condition in Conditions)
                BubbleUpOrs(newClause, condition);

            newClause._conditions.RemoveAll(c => c is AlwaysFalseExpression);
            if (newClause._conditions.Count == 1)
            {
                var requirement = newClause._conditions[0] as RequirementExpressionBase;
                if (requirement != null && context.Achievement.AlternateRequirements.Count == 0)
                {
                    if (!context.Achievement.CoreRequirements.Any(r => r.Type == RequirementType.PauseIf) &&
                        !HasBehavior(requirement, RequirementType.PauseIf))
                    {
                        // singular alt group can be appended to the core if it doesn't have
                        // any Pauses and the core doesn't have any Pauses
                        var subclause = requirement as RequirementClauseExpression;
                        if (subclause == null || !NeedAltsForOr(subclause.Conditions))
                        {
                            if (requirement is TalliedRequirementExpression ||
                                requirement is BehavioralRequirementExpression)
                            {
                                return requirement.BuildSubclauseTrigger(context);
                            }

                            return requirement.BuildTrigger(context);
                        }
                    }
                }
            }

            var triggerContext = new TriggerBuilderContext();
            foreach (var condition in newClause.Conditions)
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
                else
                {
                    var conditional = condition as ConditionalExpression;
                    if (conditional != null)
                    {
                        var builder = new ScriptInterpreterAchievementBuilder();
                        var scope = new InterpreterScope();

                        ErrorExpression error;
                        if (!builder.PopulateFromExpression(conditional, scope, out error))
                            return error;

                        if (builder.AlternateRequirements.Count > 0)
                            return new ErrorExpression("Oops", conditional);

                        foreach (var subclauseRequirement in builder.CoreRequirements)
                            context.Trigger.Add(subclauseRequirement);
                    }
                }
            }

            return null;
        }

        private static bool HasBehavior(ExpressionBase expression, RequirementType behavior)
        {
            var behavioral = expression as BehavioralRequirementExpression;
            if (behavioral != null && behavioral.Behavior == behavior)
                return true;

            var clause = expression as RequirementClauseExpression;
            if (clause != null)
                return clause.Conditions.Any(c => HasBehavior(c, behavior));

            return false;
        }

        public override RequirementExpressionBase Optimize(TriggerBuilderContext context)
        {
            if (_conditions == null || _conditions.Count == 0)
                return this;

            var achievementContext = context as AchievementBuilderContext;
            if (achievementContext != null && achievementContext.HasPauseIf == null)
            {
                if (achievementContext.Achievement.AlternateRequirements.Count == 0)
                {
                    achievementContext.HasPauseIf = achievementContext.Achievement.CoreRequirements.Any(r => r.Type == RequirementType.PauseIf);
                    if (achievementContext.HasPauseIf == false)
                        achievementContext.HasPauseIf = HasBehavior(this, RequirementType.PauseIf);
                }
                else
                {
                    achievementContext.HasPauseIf = false;
                }
            }

            bool updated = false;
            var newRequirements = new List<ExpressionBase>(_conditions.Count);
            var scope = new InterpreterScope();
            bool seenHitCount = false;
            ExpressionBase alwaysFalseCondition = null;
            ExpressionBase alwaysTrueCondition = null;

            foreach (var requirement in _conditions)
            {
                var optimized = requirement;

                var requirementExpression = requirement as RequirementExpressionBase;
                if (requirementExpression != null)
                {
                    optimized = requirementExpression.Optimize(context);
                    updated |= !ReferenceEquals(optimized, requirement);
                }

                ErrorExpression error;
                var result = optimized.IsTrue(scope, out error);
                if (result == true)
                {
                    if (Operation == ConditionalOperation.Or)
                    {
                        if (alwaysTrueCondition == null)
                        {
                            alwaysTrueCondition = optimized;
                            newRequirements.Add(optimized);
                            updated = true;
                        }
                        continue;
                    }

                    if (!seenHitCount || context is not TallyBuilderContext)
                    {
                        updated = true;
                        continue;
                    }
                }
                else if (result == false)
                {
                    if (Operation == ConditionalOperation.And)
                    {
                        if (alwaysFalseCondition == null)
                        {
                            alwaysFalseCondition = optimized;
                            newRequirements.Add(optimized);
                            updated = true;
                        }
                        continue;
                    }

                    if (context is not AchievementBuilderContext)
                    {
                        if (!seenHitCount || context is not TallyBuilderContext)
                        {
                            updated = true;
                            continue;
                        }
                    }
                }

                if (!seenHitCount && context is TallyBuilderContext)
                    seenHitCount = HasHitTarget(optimized);

                newRequirements.Add(optimized);
            }

            if (FlattenClauses(newRequirements, Operation))
            {
                updated = true;

                if (Operation == ConditionalOperation.And)
                {
                    if (alwaysFalseCondition == null)
                        alwaysFalseCondition = newRequirements.FirstOrDefault(r => r is AlwaysFalseExpression);

                    if (alwaysTrueCondition == null)
                        alwaysTrueCondition = newRequirements.FirstOrDefault(r => r is AlwaysTrueExpression);
                }
            }

            if (alwaysTrueCondition != null)
            {
                // this clause is always true. only keep the subclauses containing ResetIfs
                // also need to keep the alwaysTrueCondition if all the ResetIfs are in clauses that can never be true
                bool needsAlwaysTrueAlt = true;
                for (int i = newRequirements.Count - 1; i >= 0; i--)
                {
                    if (HasBehavior(newRequirements[i], RequirementType.ResetIf))
                    {
                        var clause = newRequirements[i] as RequirementClauseExpression;
                        if (clause == null)
                        {
                            // not a clause, expect singular Reset condition
                            Debug.Assert(newRequirements[i] is BehavioralRequirementExpression);
                            needsAlwaysTrueAlt = false;
                        }
                        else if (!clause.Conditions.Any(c => c is AlwaysFalseExpression))
                        {
                            // clause is implicitly true. if it can't be paused, it can serve as the required alt
                            if (!HasBehavior(newRequirements[i], RequirementType.PauseIf))
                                needsAlwaysTrueAlt = false;
                        }

                        continue;
                    }

                    if (!ReferenceEquals(newRequirements[i], alwaysTrueCondition))
                        newRequirements.RemoveAt(i);
                }

                if (!needsAlwaysTrueAlt && newRequirements.Count > 1)
                {
                    if (newRequirements.Count == 2 && achievementContext != null && achievementContext.HasPauseIf == true)
                    {
                        // if there's a pause in the core group, this reset is probably be being segregated.
                        // keep the always_true() so the reset doesn't get appended to the core group.
                    }
                    else
                    {
                        newRequirements.Remove(alwaysTrueCondition);
                    }
                }
            }
            else if (alwaysFalseCondition != null)
            {
                // this clause can never be true. assume it is be being used to segregate ResetIf
                // and PauseIf conditions into an alt. discard everything but the always_false()
                // condition and the ResetIf/PauseIf conditions.
                for (int i = newRequirements.Count - 1; i >= 0; i--)
                {
                    if (HasBehavior(newRequirements[i], RequirementType.ResetIf) ||
                        HasBehavior(newRequirements[i], RequirementType.PauseIf))
                    {
                        continue;
                    }

                    if (!ReferenceEquals(newRequirements[i], alwaysFalseCondition))
                        newRequirements.RemoveAt(i);
                }
            }
            else
            {
                if (newRequirements.Count < 100)
                    updated |= EliminateRedundantConditions(newRequirements, Operation, context);
            }

            if (newRequirements.Count == 1 && newRequirements[0] is RequirementExpressionBase)
                return (RequirementExpressionBase)newRequirements[0];

            if (newRequirements.Count > 1 && newRequirements.All(r => r is RequirementClauseExpression))
            {
                var rebalanced = RebalanceSubclauses(newRequirements.OfType<RequirementClauseExpression>().ToList(), context, Operation);
                if (rebalanced != null)
                    return rebalanced;
            }

            if (!updated)
                return this;

            var newClause = new RequirementClauseExpression { Operation = Operation };
            newClause._conditions = newRequirements;
            CopyLocation(newClause);
            return newClause;
        }

        private static bool EliminateRedundantConditions(IList<ExpressionBase> requirements,
            ConditionalOperation condition, TriggerBuilderContext context)
        {
            bool updated = false;

            for (int i = requirements.Count - 1; i >= 0; i--)
            {
                var requirementI = requirements[i];

                var conditionI = requirementI as RequirementConditionExpression;
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

                        var intersect = requirementJ.LogicalIntersect(conditionI, condition);
                        if (intersect != null)
                        {
                            requirements[j] = intersect;
                            requirements.RemoveAt(i);

                            // make sure to process the intersect too
                            if (j == i + 1)
                                i++;

                            updated = true;
                            break;
                        }
                    }
                }
                else if (requirementI is AlwaysTrueExpression)
                {
                    if (condition == ConditionalOperation.Or)
                    {
                        requirements.Clear();
                        requirements.Add(requirementI);
                        return true;
                    }

                    if (context is not TallyBuilderContext)
                    {
                        requirements.RemoveAt(i);
                        updated = true;
                        continue;
                    }
                }
                else if (requirementI is AlwaysFalseExpression)
                {
                    if (condition == ConditionalOperation.And)
                    {
                        requirements.Clear();
                        requirements.Add(requirementI);
                        return true;
                    }

                    if (context is not TallyBuilderContext &&
                        context is not AchievementBuilderContext)
                    {
                        requirements.RemoveAt(i);
                        updated = true;
                        continue;
                    }
                }
                else
                {
                    // complex condition. only merge if it's an exact match
                    for (int j = 0; j < requirements.Count - 1; j++)
                    {
                        if (i == j)
                            continue;

                        if (requirements[j] == requirementI)
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
                if (requirementI != null && requirementI.Operation == condition)
                {
                    requirements.RemoveAt(i);
                    for (int j = requirementI._conditions.Count - 1; j >= 0; j--)
                        requirements.Insert(i, requirementI._conditions[j]);

                    updated = true;
                }
            }

            return updated;
        }

        /// <summary>
        /// attempts to move common clauses out of complex subclauses to create something that can be turned into an AndNext/OrNext chain
        /// </summary>
        private static RequirementClauseExpression RebalanceSubclauses(List<RequirementClauseExpression> subclauses,
            TriggerBuilderContext context, ConditionalOperation operation)
        {
            var subclauseSize = subclauses[0]._conditions.Count;
            for (int i = 1; i < subclauses.Count; i++)
            {
                if (subclauses[i]._conditions.Count != subclauseSize)
                    return null;

                if (HasBehavior(subclauses[i], RequirementType.PauseIf))
                    return null;

                if (HasBehavior(subclauses[i], RequirementType.ResetIf))
                    return null;
            }

            var newClause = new RequirementClauseExpression
            {
                Operation = (operation == ConditionalOperation.And) ? ConditionalOperation.Or : ConditionalOperation.And
            };

            bool hasUnshared = false;
            for (int i = 0; i < subclauseSize; i++)
            {
                bool shared = true;
                var search = subclauses[0]._conditions[i];
                for (int j = 1; j < subclauses.Count; j++)
                {
                    if (!subclauses[j]._conditions.Any(c => c == search))
                    {
                        shared = false;
                        break;
                    }
                }

                if (shared)
                {
                    newClause.AddCondition(search);
                }
                else
                {
                    if (hasUnshared)
                        return null;

                    hasUnshared = true;
                }
            }

            Debug.Assert(newClause._conditions.Count == subclauseSize - 1);

            RequirementExpressionBase intersected = null;
            int intersectIndex = -1;
            var newSubclause = new RequirementClauseExpression { Operation = operation };
            foreach (var subclause in subclauses)
            {
                foreach (var condition in subclause._conditions)
                {
                    if (!newClause.Conditions.Any(c => c == condition))
                    {
                        newSubclause.AddCondition(condition);

                        if (intersectIndex == -1)
                        {
                            intersected = (RequirementExpressionBase)condition;
                            intersectIndex = subclause._conditions.IndexOf(condition);
                        }
                        else if (intersected != null)
                        {
                            intersected = ((RequirementExpressionBase)condition).LogicalIntersect(intersected, operation);
                        }

                        break;
                    }
                }
            }

            if (intersected != null)
                newClause._conditions.Insert(intersectIndex, intersected);
            else
                newClause.AddCondition(newSubclause.Optimize(context));

            return newClause;
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
                    if (condition == Operation)
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
                        sharedConditions.RemoveAll(c => c == null);
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
                                Operation = Operation,
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
                                Operation = Operation,
                                _conditions = bConditions
                            };
                        }
                        else
                        {
                            b = bConditions[0];
                        }

                        var bOrC = new RequirementClauseExpression
                        {
                            Operation = (Operation == ConditionalOperation.And) ? ConditionalOperation.Or : ConditionalOperation.And,
                            _conditions = new List<ExpressionBase>() { b, c }
                        };

                        var a = new RequirementClauseExpression { Operation = Operation };
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

        /// <summary>
        /// If the last condition of the clause has a hit count, create a new clause and rearrange
        /// the conditions so the last condition does not have a hit count.
        /// </summary>
        /// <returns>
        /// Original clause if the last condition does not have a hit count, a new target where the
        /// last condition does not have a hit target, or <c>null</c> if all conditions have hit targets.
        /// </returns>
        public RequirementClauseExpression EnsureLastConditionHasNoHitTarget()
        {
            if (_conditions != null)
            {
                var lastCondition = _conditions.Last();

                // if last condition already has no hit target, return self
                if (!HasHitTarget(lastCondition))
                    return this;

                // create a copy where we can move an item without a hit target to the end
                var clone = Clone();
                for (int i = clone._conditions.Count - 2; i >= 0; i--)
                {
                    var condition = clone._conditions[i];
                    if (!HasHitTarget(condition))
                    {
                        clone._conditions.RemoveAt(i);
                        clone._conditions.Add(condition);
                        return clone;
                    }
                }

                // all conditions had hit targets, return null
            }

            return null;
        }

        /// <summary>
        /// Returns <c>true</c> if any subclause in the provided expression has a hit target.
        /// </summary>
        internal static bool HasHitTarget(ExpressionBase expression)
        {
            var condition = expression as TalliedRequirementExpression;
            if (condition != null)
                return (condition.HitTarget != 0);

            var clause = expression as RequirementClauseExpression;
            if (clause != null)
                return clause.Conditions.Any(c => HasHitTarget(c));

            var behavioral = expression as BehavioralRequirementExpression;
            if (behavioral != null)
                return HasHitTarget(behavioral.Condition);

            return false;
        }

        /// <summary>
        /// Returns <c>true</c> if all subclauses in the provided expression have a hit target.
        /// </summary>
        internal static bool AllClausesHaveHitTargets(ExpressionBase expression)
        {
            var condition = expression as TalliedRequirementExpression;
            if (condition != null)
                return (condition.HitTarget != 0);

            var clause = expression as RequirementClauseExpression;
            if (clause != null)
                return clause.Conditions.All(c => AllClausesHaveHitTargets(c));

            var behavioral = expression as BehavioralRequirementExpression;
            if (behavioral != null)
                return AllClausesHaveHitTargets(behavioral.Condition);

            return false;
        }

        internal static bool LastClauseHasHitTarget(ExpressionBase expression)
        {
            var condition = expression as TalliedRequirementExpression;
            if (condition != null)
                return (condition.HitTarget != 0);

            var clause = expression as RequirementClauseExpression;
            if (clause != null)
                return LastClauseHasHitTarget(clause.Conditions.LastOrDefault());

            var behavioral = expression as BehavioralRequirementExpression;
            if (behavioral != null)
                return LastClauseHasHitTarget(behavioral.Condition);

            return false;
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

            clause.Location = Location;
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

        /// <summary>
        /// Returns an expression where any 'never(A)'s have been converted to '!A's
        /// </summary>
        /// <returns>New requirement, or <c>null</c> if the requirement cannot be inverted.</returns>
        /// <remarks>May return the original expression if nothing needed to be converted</remarks>
        public override RequirementExpressionBase InvertResetsAndPauses()
        {
            bool updated = false;
            var newConditions = new List<ExpressionBase>(_conditions.Count);
            foreach (var condition in _conditions)
            {
                var expr = condition as RequirementExpressionBase;
                if (expr == null)
                    return null;

                var inverted = expr.InvertResetsAndPauses();
                if (inverted == null)
                    return null;

                newConditions.Add(inverted);
                updated |= !ReferenceEquals(inverted, expr);
            }

            if (!updated)
                return this;

            return new RequirementClauseExpression
            {
                Operation = Operation,
                _conditions = newConditions,
                Location = Location
            };
        }

        public class OrNextRequirementClauseExpression 
            : RequirementClauseExpression, ICloneableExpression
        {
            public OrNextRequirementClauseExpression()
                : base()
            {
                Operation = ConditionalOperation.Or;
            }

            public OrNextRequirementClauseExpression(OrNextRequirementClauseExpression source)
                : base(source)
            {
                Operation = ConditionalOperation.Or;
            }

            ExpressionBase ICloneableExpression.Clone()
            {
                return Clone();
            }

            public new OrNextRequirementClauseExpression Clone()
            {
                return new OrNextRequirementClauseExpression(this);
            }

            internal override void AppendString(StringBuilder builder)
            {
                builder.Append("__ornext(");
                base.AppendString(builder);
                builder.Append(')');
            }

            protected override bool Equals(ExpressionBase obj)
            {
                return obj is OrNextRequirementClauseExpression && base.Equals(obj);
            }

            public override RequirementExpressionBase Optimize(TriggerBuilderContext context)
            {
                var optimized = base.Optimize(context);
                var orClause = optimized as RequirementClauseExpression;
                if (orClause != null && orClause.Operation == ConditionalOperation.Or)
                {
                    if (orClause._conditions.Any(c => c is AlwaysFalseExpression))
                    {
                        var orNextClause = new OrNextRequirementClauseExpression { Location = orClause.Location };
                        foreach (var condition in orClause.Conditions.Where(c => c is not AlwaysFalseExpression))
                            orNextClause.AddCondition(condition);

                        if (orNextClause._conditions.Count == 1)
                            return (orNextClause._conditions[0] as RequirementExpressionBase) ?? orNextClause;

                        return orNextClause;
                    }
                }

                if (!ReferenceEquals(this, optimized))
                {
                    if (orClause != null && orClause.Operation == ConditionalOperation.Or)
                    { 
                        var orNextClause = new OrNextRequirementClauseExpression { Location = orClause.Location };
                        foreach (var condition in orClause.Conditions)
                            orNextClause.AddCondition(condition);

                        optimized = orNextClause;
                    }
                }

                return optimized;
            }

            public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
            {
                if (_conditions == null || _conditions.Count == 0)
                    return null;

                return BuildTrigger(context, _conditions, RequirementType.OrNext);
            }
        }
    }
}
