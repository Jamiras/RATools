using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class DisableWhenFunction : FlagConditionFunction
    {
        public DisableWhenFunction()
            : base("disable_when", RequirementType.PauseIf)
        {
            Parameters.Add(new VariableDefinitionExpression("until"));

            DefaultParameters["until"] = AlwaysFalseFunction.CreateAlwaysFalseFunctionCall();
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            if (!base.ReplaceVariables(scope, out result))
                return false;

            var func = result as FunctionCallExpression;
            if (func == null)
                return true;

            var until = GetParameter(scope, "until", out result);
            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { func.Parameters.First(), until });
            CopyLocation(result);

            return true;
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var until = functionCall.Parameters.ElementAt(1);

            // build the reset next clause
            var builder = new ScriptInterpreterAchievementBuilder();
            ExpressionBase result;
            if (!TriggerBuilderContext.ProcessAchievementConditions(builder, until, scope, out result))
                return new ErrorExpression("until did not evaluate to a valid comparison", until) { InnerError = (ErrorExpression)result };

            var error = builder.CollapseForSubClause();
            if (error != null)
                return new ErrorExpression(error.Message, until);

            var resetNextClause = new List<Requirement>();
            if (builder.CoreRequirements.Count > 0 && builder.CoreRequirements.First().Evaluate() != false)
            {
                foreach (var requirement in builder.CoreRequirements)
                {
                    if (requirement.Type == RequirementType.None)
                        requirement.Type = RequirementType.AndNext;

                    resetNextClause.Add(requirement);
                }

                resetNextClause.Last().Type = RequirementType.ResetNextIf;
            }

            // build the when clause
            var whenContext = new TriggerBuilderContext { Trigger = new List<Requirement>() };
            error = base.BuildTrigger(whenContext, scope, functionCall);
            if (error != null)
                return error;

            // 'reset next' clause first
            foreach (var resetRequirement in resetNextClause)
                context.Trigger.Add(resetRequirement);

            // then 'when' clause. make sure to insert the 'reset next' clause after each addhits/subhits
            // as they break up the 'reset next' scope.
            foreach (var whenRequirement in whenContext.Trigger)
            {
                context.Trigger.Add(whenRequirement);

                switch (whenRequirement.Type)
                {
                    case RequirementType.AddHits:
                    case RequirementType.SubHits:
                        foreach (var resetRequirement in resetNextClause)
                            context.Trigger.Add(resetRequirement);
                        break;

                    default:
                        break;
                }
            }

            // disable_when is a pause lock - if a hitcount was not specified assume the first hit is enough
            if (context.LastRequirement.HitCount == 0)
                context.LastRequirement.HitCount = 1;

            return null;
        }
    }
}
