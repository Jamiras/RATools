using RATools.Data;
using RATools.Parser.Expressions;
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
            Parameters.Add(new VariableDefinitionExpression("format"));

            DefaultParameters["when"] = AlwaysTrueFunction.CreateAlwaysTrueFunctionCall();
            DefaultParameters["format"] = new StringConstantExpression("raw");
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var error = base.BuildTrigger(context, scope, functionCall);
            if (error != null)
                return error;

            ExpressionBase result;
            var format = functionCall.Parameters.ElementAt(2);
            if (!format.ReplaceVariables(scope, out result))
                return (ErrorExpression)result;

            StringConstantExpression formatStr = result as StringConstantExpression;
            if (formatStr == null)
                return new ErrorExpression("format is not a string", format);

            if (formatStr.Value != "raw")
            {
                if (scope.GetContext<ValueBuilderContext>() != null)
                    return new ErrorExpression("Value fields only support raw measured values", format);

                if (formatStr.Value == "percent")
                    context.LastRequirement.Type = RequirementType.MeasuredPercent;
                else
                    return new ErrorExpression("Unknown format: " + formatStr.Value, format);
            }

            var when = functionCall.Parameters.ElementAt(1);

            var builder = new ScriptInterpreterAchievementBuilder();
            if (!TriggerBuilderContext.ProcessAchievementConditions(builder, when, scope, out result))
                return new ErrorExpression("when did not evaluate to a valid comparison", when) { InnerError = (ErrorExpression)result };

            error = builder.CollapseForSubClause();
            if (error != null)
                return error;

            if (builder.CoreRequirements.Count != 1 || builder.CoreRequirements.First().Evaluate() != true)
            {
                bool hasHitCount = builder.CoreRequirements.Last().HitCount != 0;
                foreach (var requirement in builder.CoreRequirements)
                {
                    if (requirement.Type == RequirementType.None)
                        requirement.Type = RequirementType.MeasuredIf;
                    else if (requirement.Type == RequirementType.AndNext && !hasHitCount)
                        requirement.Type = RequirementType.MeasuredIf;

                    context.Trigger.Add(requirement);
                }
            }

            return null;
        }

        protected override ErrorExpression ModifyRequirements(AchievementBuilder builder)
        {
            bool seenLogicalJoin = false;
            foreach (var requirement in builder.CoreRequirements)
            {
                switch (requirement.Type)
                {
                    case RequirementType.AndNext:
                    case RequirementType.OrNext:
                        seenLogicalJoin = true;
                        break;

                    case RequirementType.AddHits:
                    case RequirementType.SubHits:
                    case RequirementType.ResetNextIf:
                        seenLogicalJoin = false;
                        break;

                    default:
                        if (seenLogicalJoin && !requirement.IsCombining)
                        {
                            if (requirement.HitCount == 0 && requirement == builder.CoreRequirements.Last())
                                return new ErrorExpression("measured comparison can only have one logical clause");
                        }

                        seenLogicalJoin = false;
                        break;
                }
            }

            return base.ModifyRequirements(builder);
        }
    }
}
