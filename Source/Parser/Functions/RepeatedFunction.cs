﻿using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class RepeatedFunction : ComparisonModificationFunction
    {
        public RepeatedFunction()
            : base("repeated")
        {
            Parameters.Clear();
            Parameters.Add(new VariableDefinitionExpression("count"));
            Parameters.Add(new VariableDefinitionExpression("comparison"));
        }

        protected RepeatedFunction(string name)
            : base(name)
        {
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var count = GetIntegerParameter(scope, "count", out result);
            if (count == null)
                return false;

            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            var condition = comparison as RequirementConditionExpression;
            if (condition == null)
                return base.ReplaceVariables(scope, out result);

            result = AddHitCount(comparison, count, scope);
            return (result.Type != ExpressionType.Error);
        }

        protected ExpressionBase AddHitCount(ExpressionBase comparison, IntegerConstantExpression count, InterpreterScope scope)
        {
            if (count.Value < 0)
                return new ErrorExpression("count must be greater than or equal to zero", count);

            uint hitTarget = (uint)count.Value;
            if (hitTarget == 0)
            {
                // a repeated/tally expression with a count of 0 is unbounded. unbounded target
                // counts are invalid by themselves. make sure we're in a valid context.
                var functionContext = scope.GetContext<FunctionCallExpression>(f => f.FunctionName.Name == "measured");
                if (functionContext != null)
                {
                    // an unbounded count can be measured for a value expression
                    if (scope.GetContext<ValueBuilderContext>() != null)
                    {
                        // assign the maximum allowed value for now. it'll be removed by the serializer.
                        hitTarget = uint.MaxValue;
                    }
                }

                if (hitTarget == 0)
                    return new ErrorExpression("Unbounded count is only supported in measured value expressions", count);
            }

            var requirement = comparison as RequirementConditionExpression;
            if (requirement == null)
                return new ErrorExpression(Name.Name + " can ony be applied to requirement clauses", this);
        
            if (requirement.HitTarget != 0)
                return new ErrorExpression("Comparison already has a hit target", comparison);

            var newRequirement = requirement.Clone();
            newRequirement.HitTarget = hitTarget;
            return newRequirement;
        }

        protected override ErrorExpression ModifyRequirements(AchievementBuilder builder)
        {
            // not actually modifying requirements, but allows us to do some validation
            foreach (var requirement in builder.CoreRequirements)
            {
                if (requirement.Type == RequirementType.AddHits)
                    return new ErrorExpression("tally not allowed in subclause");
                if (requirement.Type == RequirementType.SubHits)
                    return new ErrorExpression("tally not allowed in subclause");
            }

            return null;
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var count = (IntegerConstantExpression)functionCall.Parameters.First();
            var comparison = functionCall.Parameters.ElementAt(1);

            return BuildTriggerConditions(context, scope, comparison, count);
        }

        protected ErrorExpression BuildTriggerConditions(TriggerBuilderContext context, InterpreterScope scope, ExpressionBase comparison, IntegerConstantExpression count)
        {
            ErrorExpression error;

            var condition = comparison as ConditionalExpression;
            if (condition != null)
            {
                // extract never() conditions from And sequence and build a ResetNextIf clause
                var nonNeverExpressions = new List<ExpressionBase>();
                FunctionCallExpression neverExpression = null;
                ExpressionBase lastNonHitTargetCondition = null;

                foreach (var clause in condition.Conditions)
                {
                    var functionCall = clause as FunctionCallExpression;
                    if (functionCall != null && functionCall.FunctionName.Name == "never")
                    {
                        if (neverExpression != null)
                        {
                            var conditional = neverExpression.Parameters.First() as ConditionalExpression;
                            if (conditional != null && conditional.Operation == ConditionalOperation.Or)
                            {
                                conditional.AddCondition(functionCall.Parameters.First());
                            }
                            else
                            {
                                var conditions = new List<ExpressionBase>() { neverExpression.Parameters.First(), functionCall.Parameters.First() };
                                conditional = new ConditionalExpression(ConditionalOperation.Or, conditions);
                                neverExpression = new FunctionCallExpression("never", new[] { conditional });
                            }
                        }
                        else
                        {
                            neverExpression = functionCall;
                        }
                    }
                    else
                    {
                        if (!RequirementClauseExpression.HasHitTarget(clause))
                            lastNonHitTargetCondition = clause;

                        nonNeverExpressions.Add(clause);
                    }
                }

                if (neverExpression != null && nonNeverExpressions.Count > 0)
                {
                    if (condition.Operation == ConditionalOperation.And)
                    {
                        // define a new scope with a nested context to prevent TriggerBuilderContext.ProcessAchievementConditions
                        // from optimizing out the ResetIf
                        var nestedContext = new TriggerBuilderContext();
                        nestedContext.Trigger = new List<Requirement>();
                        var innerScope = new InterpreterScope(scope);
                        innerScope.Context = nestedContext;

                        error = BuildTriggerCondition(nestedContext, innerScope, neverExpression);
                        if (error != null)
                        {
                            neverExpression.Parameters.First().CopyLocation(error);
                            return error;
                        }

                        nestedContext.LastRequirement.Type = RequirementType.ResetNextIf;
                        foreach (var requirement in nestedContext.Trigger)
                        {
                            if (requirement.Type == RequirementType.ResetIf)
                                requirement.Type = RequirementType.OrNext;
                            context.Trigger.Add(requirement);
                        }

                        comparison = new ConditionalExpression(ConditionalOperation.And, nonNeverExpressions);
                    }
                    else
                    {
                        nonNeverExpressions.Clear();
                        nonNeverExpressions.AddRange(condition.Conditions);
                    }
                }

                if (lastNonHitTargetCondition == null)
                {
                    if (condition.Operation == ConditionalOperation.And)
                        return MergeHitCounts(context, scope, count, comparison);

                    return WrapInTally(context, scope, count, comparison);
                }

                if (!ReferenceEquals(nonNeverExpressions.Last(), lastNonHitTargetCondition))
                {
                    nonNeverExpressions.Remove(lastNonHitTargetCondition);
                    nonNeverExpressions.Add(lastNonHitTargetCondition);
                    comparison = new ConditionalExpression(ConditionalOperation.And, nonNeverExpressions);
                }
            }
            else
            {
                var clause = comparison as RequirementClauseExpression;
                if (clause != null)
                {
                    comparison = clause.Optimize(new TallyBuilderContext());

                    clause = comparison as RequirementClauseExpression;
                    if (clause != null)
                    {
                        var rearranged = clause.EnsureLastConditionHasNoHitTarget();
                        if (rearranged == null)
                        {
                            if (clause.Operation == ConditionalOperation.And)
                                return MergeHitCounts(context, scope, count, clause);

                            return WrapInTally(context, scope, count, clause);
                        }

                        comparison = rearranged;
                    }
                }
            }

            error = BuildTriggerCondition(context, scope, comparison);
            if (error != null)
                return error;

            return AssignHitCount(context, scope, count, Name.Name);
        }

        private ErrorExpression MergeHitCounts(TriggerBuilderContext context,
            InterpreterScope scope, IntegerConstantExpression count, ExpressionBase comparison)
        {
            var error = BuildTriggerCondition(context, scope, comparison);
            if (error != null)
                return error;

            // once(once(A)) => once(A)
            // repeated(3, repeated(2, A)) => repeated(6, A)
            // once(A && once(B)) => A && once(B)
            // once(once(A) && once(B)) => once(A) && once(B)
            context.LastRequirement.HitCount *= (uint)count.Value;
            return null;
        }

        private static ErrorExpression WrapInTally(TriggerBuilderContext context,
            InterpreterScope scope, IntegerConstantExpression count, ExpressionBase comparison)
        {
            var tally = new FunctionCallExpression("tally", new ExpressionBase[]
            {
                count,
                comparison,
                new AlwaysFalseExpression()
            });

            return new TallyFunction().BuildTrigger(context, scope, tally);
        }

        protected static ErrorExpression AssignHitCount(TriggerBuilderContext context, InterpreterScope scope, IntegerConstantExpression count, string functionName)
        {
            if (count.Value < 0)
                return new ErrorExpression("count must be greater than or equal to zero", count);

            if (count.Value == 0)
            {
                // a repeated/tally expression with a count of 0 is unbounded. unbounded target
                // counts are invalid by themselves. make sure we're in a valid context.
                var functionContext = scope.GetContext<FunctionCallExpression>();
                if (functionContext != null && functionContext.FunctionName.Name == "measured")
                {
                    // an unbounded count can be measured for a value expression
                    if (scope.GetContext<ValueBuilderContext>() != null)
                    {
                        // assign the maximum allowed value for now. it'll be removed by the serializer.
                        context.LastRequirement.HitCount = uint.MaxValue;
                        return null;
                    }
                }

                return new ErrorExpression("Unbounded count is only supported in measured value expressions", count);
            }

            context.LastRequirement.HitCount = (uint)count.Value;
            return null;
        }

        private static ErrorExpression ProcessOrNextSubClause(ICollection<Requirement> requirements)
        {
            if (requirements.Count == 0)
                return null;

            int i = requirements.Count - 1;
            var requirement = requirements.ElementAt(i);
            if (requirement.Type != RequirementType.None)
                return new ErrorExpression("Modifier not allowed in multi-condition repeated clause");

            while (i > 0)
            {
                --i;
                requirement = requirements.ElementAt(i);
                switch (requirement.Type)
                {
                    case RequirementType.None:
                        // convert a chain of normal conditions into AndNexts so they're grouped within the AddHits
                        requirement.Type = RequirementType.AndNext;
                        break;

                    default:
                        // non-constructing conditions are not allowed within the AddHits clause
                        if (!requirement.IsCombining)
                            return new ErrorExpression("Modifier not allowed in multi-condition repeated clause");
                        break;
                }
            }

            return null;
        }

        protected static ErrorExpression EvaluateCondition(TriggerBuilderContext context, InterpreterScope scope, ConditionalExpression condition)
        {
            ExpressionBase result;

            var builder = new ScriptInterpreterAchievementBuilder();
            if (!TriggerBuilderContext.ProcessAchievementConditions(builder, condition, scope, out result))
                return (ErrorExpression)result;

            if (builder.CoreRequirements.Any())
            {
                var error = ProcessOrNextSubClause(builder.CoreRequirements);
                if (error != null)
                    return error;

                if (builder.AlternateRequirements.Count == 0)
                {
                    // everything was converted to an OrNext. convert the last back
                    builder.CoreRequirements.Last().Type = RequirementType.None;

                    // one of the alts was entirely promoted to Core. We only need to check for that.
                    foreach (var clause in builder.CoreRequirements)
                        context.Trigger.Add(clause);

                    return null;
                }

                // core requirements have to be injected into each subclause as a series of AndNext's
                builder.CoreRequirements.Last().Type = RequirementType.AndNext;
            }

            var requirements = new List<ICollection<Requirement>>();
            foreach (var altGroup in builder.AlternateRequirements)
            {
                var error = ProcessOrNextSubClause(altGroup);
                if (error != null)
                    return error;

                if (builder.CoreRequirements.Any())
                {
                    var merged = new List<Requirement>(builder.CoreRequirements);
                    merged.AddRange(altGroup);
                    requirements.Add(merged);
                }
                else
                {
                    requirements.Add(altGroup);
                }
            }

            // the last item cannot have its own HitCount as it will hold the HitCount for the group. 
            // if necessary, find one without a HitCount and make it the last. 
            int index = requirements.Count - 1;
            if (requirements[index].Last().HitCount > 0)
            {
                do
                {
                    index--;
                } while (index >= 0 && requirements[index].Last().HitCount > 0);

                if (index == -1)
                {
                    // all requirements had HitCount limits, add a dummy item that's never true for the total HitCount
                    requirements.Add(new Requirement[] { AlwaysFalseFunction.CreateAlwaysFalseRequirement() });
                }
                else
                {
                    // found a requirement without a HitCount limit, move it to the last spot for the total HitCount
                    var requirement = requirements[index];
                    requirements.RemoveAt(index);
                    requirements.Add(requirement);
                }
            }

            // if we can guarantee the individual requirements won't be true in the same frame, we can use AddHits
            // instead of OrNext to improve compatibility with older versions of RetroArch
            if (CanUseAddHits(requirements))
            {
                foreach (var requirement in requirements)
                {
                    foreach (var cond in requirement)
                    {
                        if (cond.Type == RequirementType.OrNext)
                            cond.Type = RequirementType.AddHits;
                    }
                }
            }
            else
            {
                // an AndNext in the first clause is acceptable, but once we see the first
                // OrNext, each clause must be a single logical condition as AndNext has the
                // same priority as OrNext and will not be processed first.
                for (int i = 1; i < requirements.Count; ++i)
                {
                    if (requirements[i].Any(r => r.Type == RequirementType.AndNext))
                        return new ErrorExpression("Cannot join multiple AndNext chains with OrNext");
                }
            }

            // everything was converted to an OrNext. convert the last back
            requirements.Last().Last().Type = RequirementType.None;

            // load the requirements into the trigger
            foreach (var requirement in requirements)
            {
                foreach (var clause in requirement)
                    context.Trigger.Add(clause);
            }

            return null;
        }

        private static bool CanUseAddHits(List<ICollection<Requirement>> requirements)
        {
            // make sure each clause ends with a value comparison
            foreach (var requirement in requirements)
            {
                if (requirement.Last().Right.Type != FieldType.Value)
                    return false;

                // cannot change OrNext to AddHits if AddHits already exists
                if (requirement.Any(r => r.Type == RequirementType.AddHits))
                    return false;
            }

            // find the first condition that doesn't evaluate to always_false
            List<RequirementEx> first;
            var firstIndex = 0;
            do
            {
                first = RequirementEx.Combine(requirements[firstIndex]);
                if (first.Count >= 1)
                    break;

                // condition is always_false, try next
                ++firstIndex;
            } while (firstIndex < requirements.Count);

            if (first.Count != 1)
                return false;

            var firstOperator = first[0].Requirements.Last().Operator;

            for (int i = firstIndex + 1; i < requirements.Count; ++i)
            {
                var requirementEx = RequirementEx.Combine(requirements[i]);
                if (requirementEx.Count == 0)
                    continue;
                if (requirementEx.Count > 1)
                    return false;

                var right = requirementEx[0];
                if (right.Evaluate() == false)
                    continue;

                if (right.Requirements.Last().Operator != firstOperator ||
                    right.Requirements.Last().Right.Type != FieldType.Value ||
                    !right.LeftEquals(first[0]))
                {
                    // if both sides are not making the same comparison to different values, they
                    // could occur in the same frame. don't change the OrNext to an AddHits
                    return false;
                }
            }

            return true;
        }

        internal class OrNextWrapperFunction : RepeatedFunction
        {
            public OrNextWrapperFunction()
                : base("__ornext")
            {
            }

            public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
            {
                var comparison = GetParameter(scope, "comparison", out result);
                if (comparison == null)
                    return false;

                // cannot directly access FunctionDefinitionExpression.ReplaceVariables, so mimic it
                result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { comparison });
                CopyLocation(result);
                return true;
            }

            public override ErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
            {
                var comparison = functionCall.Parameters.ElementAt(0);

                // last requirement hit target will implicitly be left at 0
                return BuildTriggerCondition(context, scope, comparison);
            }
        }
    }
}
