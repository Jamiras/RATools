using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class MeasuredFunction : FlagConditionFunction
    {
        public MeasuredFunction()
            : base("measured", RequirementType.Measured)
        {
            Parameters.Add(new VariableDefinitionExpression("when"));

            DefaultParameters["when"] = new FunctionCallExpression("always_true", new ExpressionBase[0]);
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            if (!base.ReplaceVariables(scope, out result))
                return false;

            var func = result as FunctionCallExpression;
            if (func == null)
                return true;

            var when = GetParameter(scope, "when", out result);
            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { func.Parameters.First(), when });
            CopyLocation(result);

            return true;
        }

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var error = base.BuildTrigger(context, scope, functionCall);
            if (error != null)
                return error;

            var when = functionCall.Parameters.ElementAt(1);

            var builder = new ScriptInterpreterAchievementBuilder();
            ExpressionBase result;
            if (!TriggerBuilderContext.ProcessAchievementConditions(builder, when, scope, out result))
                return new ParseErrorExpression("when did not evaluate to a valid comparison", when) { InnerError = (ParseErrorExpression)result };

            if (builder.AlternateRequirements.Count > 0)
                return new ParseErrorExpression(Name.Name + " does not support ||'d conditions", when);

            if (builder.CoreRequirements.Count != 1 || builder.CoreRequirements.First().Evaluate() != true)
            {
                foreach (var requirement in builder.CoreRequirements)
                {
                    if (requirement.Type == RequirementType.None)
                        requirement.Type = RequirementType.MeasuredIf;

                    context.Trigger.Add(requirement);
                }
            }

            return null;
        }
    }
}
