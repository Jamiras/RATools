using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal abstract class ComparisonModificationFunction : FunctionDefinitionExpression
    {
        public ComparisonModificationFunction(string name)
            : base(name)
        {
            Parameters.Add(new VariableExpression("comparison"));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var context = scope.GetContext<TriggerBuilderContext>();
            if (context == null)
            {
                result = new ParseErrorExpression(Name.Name + " has no meaning outside of a trigger clause");
                return false;
            }

            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            var builder = new ScriptInterpreterAchievementBuilder();
            if (!TriggerBuilderContext.ProcessAchievementConditions(builder, comparison, scope, out result))
                return false;

            if (builder.AlternateRequirements.Count > 0)
            {
                result = new ParseErrorExpression(Name.Name + " does not support ||'d conditions", comparison);
                return false;
            }

            if (builder.CoreRequirements.Count == 0 ||
                builder.CoreRequirements.Last().Operator == RequirementOperator.None)
            {
                result = new ParseErrorExpression("comparison did not evaluate to a valid comparison", comparison);
                return false;
            }

            if (builder.CoreRequirements.Count(r => r.Operator != RequirementOperator.None) != 1)
            {
                result = new ParseErrorExpression(Name.Name + " does not support &&'d conditions", comparison);
                return false;
            }

            ModifyRequirements(builder);

            foreach (var requirement in builder.CoreRequirements)
                context.Trigger.Add(requirement);

            return true;
        }

        protected abstract void ModifyRequirements(ScriptInterpreterAchievementBuilder builder);
    }
}
