using RATools.Data;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal abstract class ComparisonModificationFunction : TriggerBuilderContext.FunctionDefinition
    {
        public ComparisonModificationFunction(string name)
            : base(name)
        {
            Parameters.Add(new VariableDefinitionExpression("comparison"));
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            if (!IsInTriggerClause(scope, out result))
                return false;

            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { comparison });
            return true;
        }

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var comparison = functionCall.Parameters.First();
            return BuildTriggerCondition(context, scope, comparison);
        }

        protected ParseErrorExpression BuildTriggerCondition(TriggerBuilderContext context, InterpreterScope scope, ExpressionBase condition)
        { 
            var builder = new ScriptInterpreterAchievementBuilder();
            ExpressionBase result;
            if (!TriggerBuilderContext.ProcessAchievementConditions(builder, condition, scope, out result))
                return (ParseErrorExpression)result;

            if (builder.AlternateRequirements.Count > 0)
                return new ParseErrorExpression(Name.Name + " does not support ||'d conditions", condition);

            ParseErrorExpression error = ValidateSingleCondition(builder.CoreRequirements);
            if (error != null)
                return new ParseErrorExpression(error, condition);

            error = ModifyRequirements(builder);
            if (error != null)
                return error;

            foreach (var requirement in builder.CoreRequirements)
                context.Trigger.Add(requirement);

            return null;
        }

        protected ParseErrorExpression ValidateSingleCondition(ICollection<Requirement> requirements)
        {
            if (requirements.Count == 0 ||
                requirements.Last().Operator == RequirementOperator.None)
            {
                return new ParseErrorExpression("comparison did not evaluate to a valid comparison");
            }

            if (requirements.Count(r => r.Operator != RequirementOperator.None) != 1)
                return new ParseErrorExpression(Name.Name + " does not support &&'d conditions");

            return null;
        }

        protected abstract ParseErrorExpression ModifyRequirements(AchievementBuilder builder);
    }
}
