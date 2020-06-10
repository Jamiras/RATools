using RATools.Data;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class FlagConditionFunction : ComparisonModificationFunction
    {
        public FlagConditionFunction(string name, RequirementType type)
            : base(name)
        {
            _type = type;
        }

        private readonly RequirementType _type;

        private static void GetOrConditions(List<ExpressionBase> conditions, ExpressionBase expression)
        {
            var condition = expression as ConditionalExpression;
            if (condition != null && condition.Operation == ConditionalOperation.Or)
            {
                GetOrConditions(conditions, condition.Left);
                GetOrConditions(conditions, condition.Right);
            }
            else
            {
                conditions.Add(expression);
            }
        }

        private bool ConvertToAndChain(InterpreterScope scope, ConditionalExpression condition, out ExpressionBase result)
        {
            var conditions = new List<ExpressionBase>();
            GetOrConditions(conditions, condition.Left);
            GetOrConditions(conditions, condition.Right);

            if (!conditions[0].ReplaceVariables(scope, out result))
                return false;

            ExpressionBase andChain = new FunctionCallExpression(Name.Name, new ExpressionBase[] { result });
            for (int i = 1; i < conditions.Count; i++)
            {
                if (!conditions[i].ReplaceVariables(scope, out result))
                    return false;

                result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { result });
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

            var condition = comparison as ConditionalExpression;
            if (condition != null && condition.Operation == ConditionalOperation.Or)
            {
                if (!ConvertToAndChain(scope, condition, out result))
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
