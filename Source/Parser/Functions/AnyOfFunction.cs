using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class AnyOfFunction : IterableJoiningFunction
    {
        public AnyOfFunction()
            : base("any_of")
        {
        }

        protected override ExpressionBase Combine(ExpressionBase left, ExpressionBase right)
        {
            if (left == null)
                return right;

            var booleanRight = right as BooleanConstantExpression;
            if (booleanRight != null)
            {
                var booleanLeft = left as BooleanConstantExpression;
                if (booleanLeft == null)
                    return new BooleanConstantExpression(booleanRight.Value);

                return new BooleanConstantExpression(booleanLeft.Value || booleanRight.Value);
            }

            right.IsLogicalUnit = true;

            var clause = left as RequirementClauseExpression;
            if (clause == null || clause.Operation != ConditionalOperation.Or)
            {
                clause = new RequirementClauseExpression { Operation = ConditionalOperation.Or };

                var leftRequirement = left as RequirementExpressionBase;
                if (leftRequirement == null)
                    return new ErrorExpression("condition is not a requirement", left);

                clause.AddCondition(leftRequirement);
            }

            var rightRequirement = right as RequirementExpressionBase;
            if (rightRequirement == null)
                return new ErrorExpression("condition is not a requirement", right);

            clause.AddCondition(rightRequirement);
            return clause;
        }

        protected override ExpressionBase GenerateEmptyResult()
        {
            return new AlwaysFalseExpression();
        }
    }
}
