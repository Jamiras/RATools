using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class RepeatedFunction : FunctionDefinitionExpression
    {
        public RepeatedFunction()
            : base("repeated")
        {
            Parameters.Add(new VariableDefinitionExpression("count"));
            Parameters.Add(new VariableDefinitionExpression("comparison"));
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

            if (count.Value <= 0)
            {
                result = new ErrorExpression("count must be greater than or equal to zero", count);
                return false;
            }

            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            if (!CanBeTallied(comparison, out result))
                return false;

            var tally = new TalliedRequirementExpression { HitTarget = (uint)count.Value };
            tally.AddTalliedCondition(comparison);

            result = tally;
            CopyLocation(result);
            return true;
        }

        public static bool CanBeTallied(ExpressionBase comparison, out ExpressionBase result)
        {
            var clause = comparison as RequirementClauseExpression;
            if (clause != null)
            {
                foreach (var condition in clause.Conditions)
                {
                    if (!CanBeTallied(condition, out result))
                        return false;
                }

                result = null;
                return true;
            }

            if (comparison is RequirementConditionExpression ||
                comparison is AlwaysTrueExpression ||
                comparison is AlwaysFalseExpression)
            {
                result = null;
                return true;
            }

            string functionName = "unknown";

            var tallied = comparison as TalliedRequirementExpression;
            if (tallied != null)
            {
                if (tallied.Conditions.Count() < 2)
                {
                    result = null;
                    return true;
                }

                functionName = "tally";
            }

            var behavioral = comparison as BehavioralRequirementExpression;
            if (behavioral != null)
                functionName = BehavioralRequirementExpression.GetFunctionName(behavioral.Behavior);

            result = new ErrorExpression(functionName + " not allowed in subclause", comparison);
            return false;
        }

        internal class OrNextWrapperFunction : FunctionDefinitionExpression
        {
            public OrNextWrapperFunction()
                : base("__ornext")
            {
            }

            public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
            {
                var comparison = GetParameter(scope, "comparison", out result);
                if (comparison == null)
                    return false;

                // cannot directly access FunctionDefinitionExpression.ReplaceVariables, so mimic it
                result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { comparison });
                CopyLocation(result);
                return true;
            }

            //public override ErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
            //{
            //    var comparison = functionCall.Parameters.ElementAt(0);

            //    // last requirement hit target will implicitly be left at 0
            //    return BuildTriggerCondition(context, scope, comparison);
            //}
        }
    }
}
