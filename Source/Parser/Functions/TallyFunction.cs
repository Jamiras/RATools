using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class TallyFunction : FunctionDefinitionExpression
    {
        public TallyFunction()
            : base("tally")
        {
            Parameters.Add(new VariableDefinitionExpression("count"));
            Parameters.Add(new VariableDefinitionExpression("..."));
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var count = GetIntegerParameter(scope, "count", out result);
            if (count == null)
                return false;

            if (count.Value < 0)
            {
                result = new ErrorExpression("count must be greater than or equal to zero", count);
                return false;
            }

            var varargs = GetVarArgsParameter(scope, out result, count, true);
            if (varargs == null)
                return false;

            var parameters = new List<ExpressionBase>();
            parameters.Add(count);

            foreach (var comparison in varargs.Entries)
            {
                if (!RepeatedFunction.CanBeTallied(comparison, RequirementType.ResetNextIf, out result))
                {
                    var behavioral = comparison as BehavioralRequirementExpression;
                    if (behavioral == null || behavioral.Behavior != RequirementType.SubHits)
                        return false;
                }
            }

            var tallyScope = new InterpreterScope(scope);
            tallyScope.Context = this;

            if (!BuildTalliedRequirementExpression((uint)count.Value, varargs, tallyScope, out result))
                return false;

            CopyLocation(result);
            return true;
        }

        public static bool BuildTalliedRequirementExpression(uint count, ArrayExpression varargs, InterpreterScope scope, out ExpressionBase result)
        {
            var tallyResult = new TalliedRequirementExpression { HitTarget = count };

            foreach (var entry in varargs.Entries)
            {
                if (!entry.ReplaceVariables(scope, out result))
                    return false;

                var functionCall = result as FunctionCallExpression;
                if (functionCall != null && functionCall.FunctionName.Name == "deduct")
                {
                    var requirement = functionCall.Parameters.First() as RequirementExpressionBase;
                    if (requirement == null)
                    {
                        result = new ErrorExpression("Cannot tally " + functionCall.Parameters.First().Type, functionCall.Parameters.First());
                        return false;
                    }
                    tallyResult.AddDeductedCondition(requirement);
                }
                else
                {
                    var requirement = result as RequirementExpressionBase;
                    if (requirement == null)
                    {
                        result = new ErrorExpression("Cannot tally " + result.Type, result);
                        return false;
                    }
                    tallyResult.AddTalliedCondition(requirement);
                }
            }

            result = tallyResult;
            return true;
        }
    }

    internal class DeductFunction : FunctionDefinitionExpression
    {
        public DeductFunction()
            : base("deduct")
        {
            Parameters.Add(new VariableDefinitionExpression("comparison"));
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var tally = scope.GetContext<TallyFunction>(); // explicitly in tally clause
            if (tally == null)
            {
                var assignment = scope.GetInterpreterContext<AssignmentExpression>(); // in generic assignment clause - may be used byte rich_presence_display - will determine later
                if (assignment == null)
                {
                    result = new ErrorExpression(Name.Name + " has no meaning outside of a tally call");
                    return false;
                }
            }

            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            if (!RepeatedFunction.CanBeTallied(comparison, RequirementType.ResetNextIf, out result))
                return false;

            result = new BehavioralRequirementExpression
            {
                Behavior = RequirementType.SubHits,
                Condition = (RequirementExpressionBase)comparison
            };

            CopyLocation(result);
            return true;
        }
    }
}
