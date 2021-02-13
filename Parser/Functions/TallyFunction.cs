using RATools.Data;
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
            if (!IsInTriggerClause(scope, out result))
                return false;

            var count = GetIntegerParameter(scope, "count", out result);
            if (count == null)
                return false;

            var varargs = GetParameter(scope, "varargs", out result) as ArrayExpression;
            if (varargs == null)
            {
                if (!(result is ParseErrorExpression))
                    result = new ParseErrorExpression("unexpected varargs", count);
                return false;
            }

            var parameters = new List<ExpressionBase>();
            parameters.Add(count);

            // special case - if there's a single array parameter, assume it's a list of conditions
            if (varargs.Entries.Count == 1 && varargs.Entries[0] is ArrayExpression)
                varargs = (ArrayExpression)varargs.Entries[0];

            foreach (var entry in varargs.Entries)
            {
                if (!entry.ReplaceVariables(scope, out result))
                    return false;

                parameters.Add(result);
            }

            result = new FunctionCallExpression(Name.Name, parameters.ToArray());
            CopyLocation(result);
            return true;
        }

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var requirements = new List<ICollection<Requirement>>();
            for (int i = 1; i < functionCall.Parameters.Count; ++i)
            {
                var condition = functionCall.Parameters.ElementAt(i);
                var conditionRequirements = new List<Requirement>();
                var nestedContext = new TriggerBuilderContext() { Trigger = conditionRequirements };

                var error = BuildTriggerCondition(nestedContext, scope, condition);
                if (error != null)
                    return error;

                conditionRequirements.Last().Type = RequirementType.AddHits;
                requirements.Add(conditionRequirements);
            }

            // if no requirements were generated, we're done
            if (requirements.Count == 0)
                return null;

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
            context.LastRequirement.HitCount = (uint)count.Value;

            return null;
        }
    }
}
