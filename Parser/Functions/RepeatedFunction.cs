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

        private static ParseErrorExpression ProcessAddHitsSubClause(ICollection<Requirement> requirements)
        {
            int i = requirements.Count - 1;
            var requirement = requirements.ElementAt(i);
            if (requirement.Type != RequirementType.None)
                return new ParseErrorExpression("modifier not allowed in multi-condition repeated clause");

            // all but the last clause need to be converted to AddHits.
            // we'll change the last one back after we've processed all the clauses.
            requirement.Type = RequirementType.AddHits;

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
                        // AddHits is a combining flag, but cannot be nested in another AddHits
                        return new ParseErrorExpression("modifier not allowed in multi-condition repeated clause");

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
                var error = ProcessAddHitsSubClause(builder.CoreRequirements);
                if (error != null)
                    return error;

                // core requirements have to be injected into each subclause as a series of AndNext's
                builder.CoreRequirements.Last().Type = RequirementType.AndNext;
            }

            var requirements = new List<ICollection<Requirement>>();
            foreach (var altGroup in builder.AlternateRequirements)
            {
                var error = ProcessAddHitsSubClause(altGroup);
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

            // everything was converted to an AddHits. convert the last back
            requirements.Last().Last().Type = RequirementType.None;

            // load the requirements into the trigger
            foreach (var requirement in requirements)
            {
                foreach (var clause in requirement)
                    context.Trigger.Add(clause);
            }

            return null;
        }
    }
}
