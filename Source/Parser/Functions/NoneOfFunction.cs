using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class NoneOfFunction : IterableJoiningFunction
    {
        public NoneOfFunction()
            : base("none_of")
        {
        }

        protected override ExpressionBase Combine(ExpressionBase left, ExpressionBase right)
        {
            var booleanRight = right as BooleanConstantExpression;
            if (booleanRight != null)
            {
                var booleanLeft = left as BooleanConstantExpression;
                if (booleanLeft == null)
                    return new BooleanConstantExpression(!booleanRight.Value);

                return new BooleanConstantExpression(booleanLeft.Value && !booleanRight.Value);
            }

            var rightRequirement = right as RequirementExpressionBase;
            if (rightRequirement == null)
                return new ErrorExpression("condition is not a requirement", right);

            rightRequirement = rightRequirement.InvertLogic();
            if (rightRequirement == null)
                return new ErrorExpression("Could not invert logic", right);

            rightRequirement.IsLogicalUnit = true;

            if (left == null)
                return rightRequirement;

            var clause = left as RequirementClauseExpression;
            if (clause == null || clause.Operation != ConditionalOperation.And)
            {
                clause = new RequirementClauseExpression { Operation = ConditionalOperation.And };

                var leftRequirement = left as RequirementExpressionBase;
                if (leftRequirement == null)
                    return new ErrorExpression("condition is not a requirement", left);

                clause.AddCondition(leftRequirement);
            }

            clause.AddCondition(rightRequirement);
            return clause;
        }

        protected override ExpressionBase GenerateEmptyResult()
        {
            return new AlwaysTrueExpression();
        }
    }
}
