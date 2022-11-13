using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class FlagConditionFunction : FunctionDefinitionExpression
    {
        public FlagConditionFunction(string name, RequirementType type)
            : base(name)
        {
            Parameters.Add(new VariableDefinitionExpression("comparison"));

            _type = type;
        }

        private readonly RequirementType _type;

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var comparison = GetRequirementParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            var clause = comparison as RequirementClauseExpression;
            if (clause != null && clause.Operation == ConditionalOperation.Or)
            {
                if (clause.Conditions.OfType<RequirementClauseExpression>().Count(c => c.Conditions.Count() > 1) > 1)
                {
                    // if there's more than one complex subclause, split into separate clauses
                    if (_type == RequirementType.Trigger)
                    {
                        // trigger_when(A || B) => trigger_when(A) || trigger_when(B)
                        result = SplitClause(clause, ConditionalOperation.Or, _type);
                    }
                    else
                    {
                        // never(A || B) => never(A) && never(B)
                        // unless(A || B) => unless(A) && unless(B)
                        result = SplitClause(clause, ConditionalOperation.And, _type);
                    }

                    CopyLocation(result);
                    return true;
                }
            }

            result = new BehavioralRequirementExpression
            {
                Behavior = _type,
                Condition = comparison,
            };

            CopyLocation(result);
            return true;
        }

        private static RequirementClauseExpression SplitClause(RequirementClauseExpression clause, ConditionalOperation operation, RequirementType type)
        {
            var newClause = new RequirementClauseExpression { Operation = operation };
            foreach (var condition in clause.Conditions)
            {
                var expr = (RequirementExpressionBase)condition;

                newClause.AddCondition(new BehavioralRequirementExpression
                {
                    Behavior = type,
                    Condition = expr,
                    Location = expr.Location
                });
            }

            newClause.Location = clause.Location;
            return newClause;
        }
    }
}
