using RATools.Data;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal abstract class FlagConditionFunction : ComparisonModificationFunction
    {
        public FlagConditionFunction(string name, RequirementType type)
            : base(name)
        {
            _type = type;
        }

        private readonly RequirementType _type;

        private static void SplitConditions(List<ExpressionBase> conditions, ExpressionBase expression, ConditionalOperation op)
        {
            var condition = expression as ConditionalExpression;
            if (condition != null && condition.Operation == op)
            {
                SplitConditions(conditions, condition.Left, op);
                SplitConditions(conditions, condition.Right, op);
            }
            else
            {
                conditions.Add(expression);
            }
        }

        protected bool SplitConditions(InterpreterScope scope, ConditionalExpression condition,
            ConditionalOperation joiningOperation, out ExpressionBase result)
        {
            var conditions = new List<ExpressionBase>();
            SplitConditions(conditions, condition, condition.Operation);

            ExpressionBase newChain = null;
            for (int i = 0; i < conditions.Count; i++)
            {
                if (!conditions[i].ReplaceVariables(scope, out result))
                    return false;

                bool wrap = true;
                var functionCallExpression = result as FunctionCallExpression;
                if (functionCallExpression != null)
                {
                    var functionDefinition = scope.GetFunction(functionCallExpression.FunctionName.Name);
                    if (functionDefinition is FlagConditionFunction)
                        wrap = false;
                }

                if (wrap)
                {
                    result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { result });
                    if (!result.ReplaceVariables(scope, out result))
                        return false;
                }

                if (newChain == null)
                    newChain = result;
                else if (wrap)
                    newChain = new ConditionalExpression(newChain, joiningOperation, result);
                else
                    newChain = new ConditionalExpression(newChain, condition.Operation, result);
            }

            result = newChain;
            CopyLocation(result);
            return true;
        }

        protected override ParseErrorExpression ModifyRequirements(AchievementBuilder builder)
        {
            var requirementsEx = RequirementEx.Combine(builder.CoreRequirements);
            foreach (var requirementEx in requirementsEx)
            {
                var lastCondition = requirementEx.Requirements.Last();
                if (lastCondition.Type != RequirementType.None)
                    return new ParseErrorExpression(string.Format("Cannot apply '{0}' to condition already flagged with {1}", Name.Name, lastCondition.Type));

                lastCondition.Type = _type;
            }

            return null;
        }
    }
}
