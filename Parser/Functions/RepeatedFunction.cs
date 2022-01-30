using RATools.Data;
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

        protected override ParseErrorExpression ModifyRequirements(AchievementBuilder builder)
        {
            // not actually modifying requirements, but allows us to do some validation
            foreach (var requirement in builder.CoreRequirements)
            {
                if (requirement.Type == RequirementType.AddHits)
                    return new ParseErrorExpression("tally not allowed in subclause");
                if (requirement.Type == RequirementType.SubHits)
                    return new ParseErrorExpression("tally not allowed in subclause");
            }

            return null;
        }

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var count = (IntegerConstantExpression)functionCall.Parameters.First();
            var comparison = functionCall.Parameters.ElementAt(1);

            return BuildTriggerConditions(context, scope, comparison, count.Value);
        }

        protected ParseErrorExpression BuildTriggerConditions(TriggerBuilderContext context, InterpreterScope scope, ExpressionBase comparison, int count)
        {
            ParseErrorExpression error;

            var condition = comparison as ConditionalExpression;
            if (condition != null && condition.Operation == ConditionalOperation.And)
            {
                // extract never() conditions from And sequence and build a ResetNextIf clause
                var nonNeverExpressions = new List<ExpressionBase>();
                ExpressionBase neverExpression = null;

                foreach (var clause in condition.Conditions)
                {
                    var functionCall = clause as FunctionCallExpression;
                    if (functionCall != null && functionCall.FunctionName.Name == "never")
                    {
                        if (neverExpression != null)
                            return new ParseErrorExpression("Only one never() clause allowed inside " + Name.Name + "()", clause);

                        neverExpression = clause;
                    }
                    else
                    {
                        nonNeverExpressions.Add(clause);
                    }
                }

                if (neverExpression != null && nonNeverExpressions.Count > 0)
                {
                    // define a new scope with a nested context to prevent TriggerBuilderContext.ProcessAchievementConditions
                    // from optimizing out the ResetIf
                    var nestedContext = new TriggerBuilderContext();
                    nestedContext.Trigger = new List<Requirement>();
                    var innerScope = new InterpreterScope(scope);
                    innerScope.Context = nestedContext;

                    error = BuildTriggerCondition(nestedContext, innerScope, neverExpression);
                    if (error != null)
                        return error;

                    nestedContext.LastRequirement.Type = RequirementType.ResetNextIf;
                    foreach (var requirement in nestedContext.Trigger)
                        context.Trigger.Add(requirement);

                    comparison = new ConditionalExpression(ConditionalOperation.And, nonNeverExpressions.ToArray());
                }
            }

            error = BuildTriggerCondition(context, scope, comparison);
            if (error != null)
                return error;

            context.LastRequirement.HitCount = (uint)count;
            return null;
        }

        private static ParseErrorExpression ProcessOrNextSubClause(ICollection<Requirement> requirements)
        {
            if (requirements.Count == 0)
                return null;

            int i = requirements.Count - 1;
            var requirement = requirements.ElementAt(i);
            if (requirement.Type != RequirementType.None)
                return new ParseErrorExpression("Modifier not allowed in multi-condition repeated clause");

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
                            return new ParseErrorExpression("Modifier not allowed in multi-condition repeated clause");
                        break;
                }
            }

            return null;
        }

        protected ParseErrorExpression EvaluateCondition(TriggerBuilderContext context, InterpreterScope scope, ConditionalExpression condition)
        {
            ExpressionBase result;

            var builder = new ScriptInterpreterAchievementBuilder();
            if (!TriggerBuilderContext.ProcessAchievementConditions(builder, condition, scope, out result))
                return (ParseErrorExpression)result;

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
                        return new ParseErrorExpression("Cannot join multiple AndNext chains with OrNext");
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
    }
}
