using RATools.Data;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class FlagConditionFunction : ComparisonModificationFunction
    {
        public FlagConditionFunction(string name, RequirementType type, ConditionalOperation splitOn)
            : base(name)
        {
            _type = type;
            _splitOn = splitOn;
        }

        private readonly RequirementType _type;
        private readonly ConditionalOperation _splitOn;

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

        private bool SplitConditions(InterpreterScope scope, ConditionalExpression condition, out ExpressionBase result)
        {
            var conditions = new List<ExpressionBase>();
            SplitConditions(conditions, condition.Left, _splitOn);
            SplitConditions(conditions, condition.Right, _splitOn);

            ExpressionBase andChain = null;
            for (int i = 0; i < conditions.Count; i++)
            {
                if (!conditions[i].ReplaceVariables(scope, out result))
                    return false;

                bool wrap = true;
                if (_splitOn == ConditionalOperation.And)
                {
                    var functionCallExpression = result as FunctionCallExpression;
                    if (functionCallExpression != null)
                    {
                        var functionDefinition = scope.GetFunction(functionCallExpression.FunctionName.Name);
                        if (functionDefinition is FlagConditionFunction)
                            wrap = false;
                    }
                }

                if (wrap)
                    result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { result });

                if (andChain == null)
                    andChain = result;
                else
                    andChain = new ConditionalExpression(andChain, ConditionalOperation.And, result);
            }

            result = andChain;
            return true;
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            // never(A || B) => never(A) && never(B)
            // unless(A || B) => unless(A) && unless(B)
            // trigger_when(A && B) => trigger_when(A) && trigger_when(B)
            var condition = comparison as ConditionalExpression;
            if (condition != null && condition.Operation == _splitOn)
            {
                if (!SplitConditions(scope, condition, out result))
                    return false;

                CopyLocation(result);
                return true;
            }

            return base.ReplaceVariables(scope, out result);
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
