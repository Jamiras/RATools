using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class TriggerWhenFunction : FlagConditionFunction
    {
        public TriggerWhenFunction()
            : base("trigger_when", RequirementType.Trigger)
        {
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            // add another TriggerBuilderContext scope to prevent optimizing the expression at this time
            var nestedScope = new InterpreterScope(scope) { Context = new TriggerBuilderContext() };
            return base.BuildTrigger(context, nestedScope, functionCall);
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            // trigger_when(A || B) => trigger_when(A) || trigger_when(B)
            // trigger_when(A && B) => trigger_when(A) && trigger_when(B)
            var condition = comparison as ConditionalExpression;
            if (condition != null && !condition.IsLogicalUnit)
            {
                if (condition.Operation == ConditionalOperation.Or)
                {
                    // OR within an AND must be kept as an OrNext
                    if (scope.GetContext<TriggerWhenFunction>() != null)
                    {
                        condition.IsLogicalUnit = true;
                        return base.ReplaceVariables(scope, out result);
                    }

                    return SplitConditions(scope, condition, condition.Operation, ConditionalOperation.Or, out result);
                }

                if (condition.Operation == ConditionalOperation.And)
                {
                    var newScope = new InterpreterScope(scope) { Context = this };
                    return SplitConditions(newScope, condition, condition.Operation, ConditionalOperation.And, out result);
                }
            }

            var clause = comparison as RequirementClauseExpression;
            if (clause != null && !clause.IsLogicalUnit)
            {
                if (clause.Operation == ConditionalOperation.Or)
                {
                    // OR within an AND must be kept as an OrNext
                    if (scope.GetContext<TriggerWhenFunction>() != null)
                    {
                        clause.IsLogicalUnit = true;
                        return base.ReplaceVariables(scope, out result);
                    }

                    return SplitConditions(scope, clause, clause.Operation, ConditionalOperation.Or, out result);
                }

                if (clause.Operation == ConditionalOperation.And)
                {
                    var newScope = new InterpreterScope(scope) { Context = this };
                    return SplitConditions(newScope, clause, clause.Operation, ConditionalOperation.And, out result);
                }
            }

            return base.ReplaceVariables(scope, out result);
        }
    }
}
