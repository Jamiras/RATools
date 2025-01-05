using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class AllOfFunction : IterableJoiningFunction
    {
        public AllOfFunction()
            : base("all_of")
        {
        }

        protected override ExpressionBase Combine(ExpressionBase accumulator, ExpressionBase predicateResult, ExpressionBase predicateInput)
        {
            if (accumulator == null)
                return predicateResult;

            var booleanRight = predicateResult as BooleanConstantExpression;
            if (booleanRight != null)
            {
                var booleanLeft = accumulator as BooleanConstantExpression;
                if (booleanLeft == null)
                    return new BooleanConstantExpression(booleanRight.Value);

                return new BooleanConstantExpression(booleanLeft.Value && booleanRight.Value);
            }

            predicateResult.IsLogicalUnit = true;

            var clause = accumulator as RequirementClauseExpression;
            if (clause == null || clause.Operation != ConditionalOperation.And)
            {
                clause = new RequirementClauseExpression { Operation = ConditionalOperation.And };

                var leftRequirement = accumulator as RequirementExpressionBase;
                if (leftRequirement == null)
                    return new ErrorExpression("condition is not a requirement", accumulator);

                clause.AddCondition(leftRequirement);
            }

            var rightRequirement = predicateResult as RequirementExpressionBase;
            if (rightRequirement == null)
                return new ErrorExpression("condition is not a requirement", predicateResult);

            clause.AddCondition(rightRequirement);
            return clause;
        }

        protected override ExpressionBase GenerateEmptyResult()
        {
            return new AlwaysTrueExpression();
        }
    }
}
