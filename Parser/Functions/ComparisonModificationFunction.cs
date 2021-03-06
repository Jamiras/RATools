﻿using RATools.Parser.Internal;
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
            {
                switch (condition.Type)
                {
                    case ExpressionType.Conditional:
                    case ExpressionType.Comparison:
                        // allowed constructs should only report the inner error
                        return (ParseErrorExpression)result;

                    default:
                        // non-allowed construct
                        return new ParseErrorExpression("comparison did not evaluate to a valid comparison", condition) { InnerError = (ParseErrorExpression)result };
                }
            }

            var error = builder.CollapseForSubClause();
            if (error != null)
                return new ParseErrorExpression(error.Message, condition);

            error = ModifyRequirements(builder);
            if (error != null)
                return new ParseErrorExpression(error.Message, condition);

            foreach (var requirement in builder.CoreRequirements)
                context.Trigger.Add(requirement);

            return null;
        }

        protected abstract ParseErrorExpression ModifyRequirements(AchievementBuilder builder);
    }
}
