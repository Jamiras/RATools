using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class DisableWhenFunction : FlagConditionFunction
    {
        public DisableWhenFunction()
            : base("disable_when", RequirementType.PauseIf, ConditionalOperation.None)
        {
            Parameters.Add(new VariableDefinitionExpression("until"));

            DefaultParameters["until"] = new FunctionCallExpression("always_false", new ExpressionBase[0]);
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

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var until = functionCall.Parameters.ElementAt(1);

            var builder = new ScriptInterpreterAchievementBuilder();
            ExpressionBase result;
            if (!TriggerBuilderContext.ProcessAchievementConditions(builder, until, scope, out result))
                return new ParseErrorExpression("until did not evaluate to a valid comparison", until) { InnerError = (ParseErrorExpression)result };

            var error = builder.CollapseForSubClause();
            if (error != null)
                return new ParseErrorExpression(error.Message, until);

            if (builder.CoreRequirements.Count > 0 && builder.CoreRequirements.First().Evaluate() != false)
            {
                foreach (var requirement in builder.CoreRequirements)
                {
                    if (requirement.Type == RequirementType.None)
                        requirement.Type = RequirementType.AndNext;

                    context.Trigger.Add(requirement);
                }

                context.LastRequirement.Type = RequirementType.ResetNextIf;
            }

            error = base.BuildTrigger(context, scope, functionCall);
            if (error != null)
                return error;

            if (context.LastRequirement.HitCount == 0)
                context.LastRequirement.HitCount = 1;

            return null;
        }
    }
}
