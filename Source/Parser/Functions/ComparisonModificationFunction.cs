using RATools.Parser.Expressions;
using RATools.Parser.Internal;
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

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var comparison = functionCall.Parameters.First();
            return BuildTriggerCondition(context, scope, comparison);
        }

        protected ErrorExpression BuildTriggerCondition(TriggerBuilderContext context, InterpreterScope scope, ExpressionBase condition, bool optimize = false)
        { 
            var builder = new ScriptInterpreterAchievementBuilder();
            ExpressionBase result;
            if (!TriggerBuilderContext.ProcessAchievementConditions(builder, condition, scope, out result))
            {
                switch (condition.Type)
                {
                    case ExpressionType.Conditional:
                    case ExpressionType.Comparison:
                        // allowed constructs should only report the inner error
                        return (ErrorExpression)result;

                    default:
                        // non-allowed construct
                        return new ErrorExpression("comparison did not evaluate to a valid comparison", condition) { InnerError = (ErrorExpression)result };
                }
            }

            if (optimize)
            {
                var message = builder.OptimizeForSubClause();
                if (message != null)
                    return new ErrorExpression(message, condition);
            }

            var error = builder.CollapseForSubClause();
            if (error != null)
                return new ErrorExpression(error.Message, condition);

            error = ModifyRequirements(builder);
            if (error != null)
                return new ErrorExpression(error.Message, condition);

            foreach (var requirement in builder.CoreRequirements)
                context.Trigger.Add(requirement);

            return null;
        }

        protected abstract ErrorExpression ModifyRequirements(AchievementBuilder builder);
    }
}
