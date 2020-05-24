﻿using RATools.Data;
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

        protected RequirementType _orNextFlag = RequirementType.OrNext;

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            if (!IsInTriggerClause(scope, out result))
                return false;

            var count = GetIntegerParameter(scope, "count", out result);
            if (count == null)
                return false;

            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { count, comparison });
            CopyLocation(result);
            return true;
        }

        protected override ParseErrorExpression ModifyRequirements(AchievementBuilder builder)
        {
            // not actually called because we override BuildTrigger, but required because its abstract in the base class
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

            var logicalComparison = comparison as ConditionalExpression;
            if (logicalComparison != null && logicalComparison.Operation == ConditionalOperation.Or)
                error = EvaluateAddHits(context, scope, logicalComparison);
            else
                error = BuildTriggerCondition(context, scope, comparison);

            if (error != null)
                return error;

            context.LastRequirement.HitCount = (ushort)count;
            return null;
        }

        private ParseErrorExpression ProcessOrNextSubClause(ICollection<Requirement> requirements)
        {
            int i = requirements.Count - 1;
            var requirement = requirements.ElementAt(i);
            if (requirement.Type != RequirementType.None)
                return new ParseErrorExpression("modifier not allowed in multi-condition repeated clause");

            // all but the last clause need to be converted to OrNext.
            // we'll change the last one back after we've processed all the clauses.
            requirement.Type = _orNextFlag;

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

                    case RequirementType.AddHits:
                        if (_orNextFlag == RequirementType.AddHits)
                        {
                            // AddHits is a combining flag, but cannot be nested in another AddHits
                            return new ParseErrorExpression("modifier not allowed in multi-condition repeated clause");
                        }
                        break;

                    default:
                        // non-constructing conditions are not allowed within the AddHits clause
                        if (!requirement.IsCombining)
                            return new ParseErrorExpression("modifier not allowed in multi-condition repeated clause");
                        break;
                }
            }

            return null;
        }

        private ParseErrorExpression EvaluateAddHits(TriggerBuilderContext context, InterpreterScope scope, ConditionalExpression condition)
        {
            ExpressionBase result;

            var builder = new ScriptInterpreterAchievementBuilder();
            if (!TriggerBuilderContext.ProcessAchievementConditions(builder, condition, scope, out result))
                return (ParseErrorExpression)result;

            if (builder.AlternateRequirements.Count == 0)
                return BuildTriggerCondition(context, scope, condition);

            if (builder.CoreRequirements.Any())
            {
                var error = ProcessOrNextSubClause(builder.CoreRequirements);
                if (error != null)
                    return error;

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
            if (_orNextFlag == RequirementType.OrNext && CanUseAddHits(requirements))
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
