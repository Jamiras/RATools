using RATools.Data;
using RATools.Parser.Internal;
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

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            var condition = comparison as ConditionalExpression;
            if (condition != null && condition.Operation == ConditionalOperation.Or)
            {
                ExpressionBase left = new FunctionCallExpression(Name.Name, new ExpressionBase[] { condition.Left });
                if (!left.ReplaceVariables(scope, out result))
                    return false;
                left = result;

                ExpressionBase right = new FunctionCallExpression(Name.Name, new ExpressionBase[] { condition.Right });
                if (!right.ReplaceVariables(scope, out result))
                    return false;
                right = result;

                result = new ConditionalExpression(left, ConditionalOperation.And, right);
                CopyLocation(result);
                return true;
            }

            return base.ReplaceVariables(scope, out result);
        }

        protected override ParseErrorExpression ModifyRequirements(AchievementBuilder builder)
        {
            builder.CoreRequirements.Last().Type = _type;
            return null;
        }
    }
}
