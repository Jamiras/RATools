using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class MeasuredFunction : FlagConditionFunction
    {
        public MeasuredFunction()
            : base("measured", RequirementType.Measured, ConditionalOperation.Or)
        {
            Parameters.Add(new VariableDefinitionExpression("when"));
            Parameters.Add(new VariableDefinitionExpression("format"));

            DefaultParameters["when"] = new FunctionCallExpression("always_true", new ExpressionBase[0]);
            DefaultParameters["format"] = new StringConstantExpression("raw");
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            if (!base.ReplaceVariables(scope, out result))
                return false;

            var func = result as FunctionCallExpression;
            if (func == null)
                return true;

            var when = GetParameter(scope, "when", out result);
            if (result != null)
                return false;

            var format = GetStringParameter(scope, "format", out result);
            if (result != null)
                return false;

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { func.Parameters.First(), when, format });
            CopyLocation(result);

            return true;
        }

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var error = base.BuildTrigger(context, scope, functionCall);
            if (error != null)
                return error;

            ExpressionBase result;
            var format = functionCall.Parameters.ElementAt(2);
            if (!format.ReplaceVariables(scope, out result))
                return (ParseErrorExpression)result;

            StringConstantExpression formatStr = result as StringConstantExpression;
            if (formatStr == null)
                return new ParseErrorExpression("format is not a string", format);

            if (formatStr.Value != "raw")
            {
                // TODO: restrict non-achievement uses to 'raw'

                if (formatStr.Value == "percent")
                {
                    context.LastRequirement.Type = RequirementType.MeasuredPercent;
                }
                else
                {
                    return new ParseErrorExpression("Unknown format: " + formatStr.Value, format);
                }
            }

            var when = functionCall.Parameters.ElementAt(1);

            var builder = new ScriptInterpreterAchievementBuilder();
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
