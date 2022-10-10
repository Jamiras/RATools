﻿using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class TallyFunction : RepeatedFunction
    {
        public TallyFunction()
            : base("tally")
        {
            Parameters.Clear();
            Parameters.Add(new VariableDefinitionExpression("count"));
            Parameters.Add(new VariableDefinitionExpression("..."));
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var count = GetIntegerParameter(scope, "count", out result);
            if (count == null)
                return false;

            var varargs = GetParameter(scope, "varargs", out result) as ArrayExpression;
            if (varargs == null)
            {
                if (!(result is ErrorExpression))
                    result = new ErrorExpression("unexpected varargs", count);
                return false;
            }

            var parameters = new List<ExpressionBase>();
            parameters.Add(count);

            // special case - if there's a single array parameter, assume it's a list of conditions
            if (varargs.Entries.Count == 1)
            {
                var arrayExpression = varargs.Entries[0] as ArrayExpression;
                if (arrayExpression == null)
                {
                    var referenceExpression = varargs.Entries[0] as VariableReferenceExpression;
                    if (referenceExpression != null)
                        arrayExpression = referenceExpression.Expression as ArrayExpression;
                }
                if (arrayExpression != null)
                    varargs = arrayExpression;
            }

            var tallyScope = new InterpreterScope(scope);
            tallyScope.Context = this;

            foreach (var entry in varargs.Entries)
            {
                if (!entry.ReplaceVariables(tallyScope, out result))
                    return false;

                parameters.Add(result);
            }

            result = new FunctionCallExpression(Name.Name, parameters.ToArray());
            CopyLocation(result);
            return true;
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            ExpressionBase result;
            int addHitsClauses = 0;
            int subHitsClauses = 0;

            var requirements = new List<ICollection<Requirement>>();
            for (int i = 1; i < functionCall.Parameters.Count; ++i)
            {
                var condition = functionCall.Parameters.ElementAt(i);

                // expression that can never be true cannot accumulate hits to be added or subtracted from the tally
                ErrorExpression error;
                var conditionRequirements = new List<Requirement>();
                var nestedContext = new TriggerBuilderContext() { Trigger = conditionRequirements };
                var modifier = RequirementType.AddHits;

                var funcCall = condition as FunctionCallExpression;
                if (funcCall != null && funcCall.FunctionName.Name == "deduct")
                {
                    var deductScope = funcCall.GetParameters(scope.GetFunction(funcCall.FunctionName.Name), scope, out result);
                    if (deductScope == null)
                        return (ErrorExpression)result;

                    condition = deductScope.GetVariable("comparison");

                    if (condition.IsTrue(deductScope, out error) == false)
                        continue;

                    modifier = RequirementType.SubHits;
                    ++subHitsClauses;
                }
                else
                {
                    if (condition.IsTrue(scope, out error) == false)
                        continue;

                    ++addHitsClauses;
                }

                if (error == null)
                {
                    // define a new scope with a nested context to prevent TriggerBuilderContext.ProcessAchievementConditions
                    // from optimizing out the ResetIf
                    var innerScope = new InterpreterScope(scope);
                    innerScope.Context = nestedContext;
                    error = BuildTriggerCondition(nestedContext, innerScope, condition, true);
                }
                if (error != null)
                    return error;

                conditionRequirements.Last().Type = modifier;
                requirements.Add(conditionRequirements);
            }

            // at least one condition has to be incrementing the tally
            if (addHitsClauses == 0)
                return new ErrorExpression("tally requires at least one non-deducted item", functionCall);

            // if there's any SubHits clauses, add a dummy clause for the final count, regardless of whether
            // the AddHits clauses have hit targets.
            if (subHitsClauses > 0)
                requirements.Add(new Requirement[] { AlwaysFalseFunction.CreateAlwaysFalseRequirement() });

            // the last item cannot have its own HitCount as it will hold the HitCount for the group.
            // if necessary, find one without a HitCount and make it the last.
            AchievementBuilder.EnsureLastGroupHasNoHitCount(requirements);

            // load the requirements into the trigger
            foreach (var requirement in requirements)
            {
                foreach (var clause in requirement)
                    context.Trigger.Add(clause);
            }

            // the last item of each clause was set to AddHits, change the absolute last back to None
            context.LastRequirement.Type = RequirementType.None;

            // set the target hitcount
            var count = (IntegerConstantExpression)functionCall.Parameters.First();
            return AssignHitCount(context, scope, count, Name.Name);
        }
    }

    internal class DeductFunction : ComparisonModificationFunction
    {
        public DeductFunction()
            : base("deduct")
        {
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var tally = scope.GetContext<TallyFunction>(); // explicitly in tally clause
            if (tally == null)
            {
                var assignment = scope.GetInterpreterContext<AssignmentExpression>(); // in generic assignment clause - may be used byte rich_presence_display - will determine later
                if (assignment == null)
                {
                    result = new ErrorExpression(Name.Name + " has no meaning outside of a tally call");
                    return false;
                }
            }

            return base.ReplaceVariables(scope, out result);
        }

        protected override ErrorExpression ModifyRequirements(AchievementBuilder builder)
        {
            var requirementsEx = RequirementEx.Combine(builder.CoreRequirements);
            foreach (var requirementEx in requirementsEx)
            {
                var lastCondition = requirementEx.Requirements.Last();
                if (lastCondition.Type != RequirementType.None)
                    return new ErrorExpression(string.Format("Cannot apply '{0}' to condition already flagged with {1}", Name.Name, lastCondition.Type));

                lastCondition.Type = RequirementType.SubHits;
            }

            return null;
        }
    }
}
