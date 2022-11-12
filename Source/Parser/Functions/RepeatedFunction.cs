﻿using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Linq;
using static System.Formats.Asn1.AsnWriter;

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

            if (count.Value < 0)
            {
                result = new ErrorExpression("count must be greater than or equal to zero", count);
                return false;
            }

            var comparison = GetParameter(scope, "comparison", out result);
            if (comparison == null)
                return false;

            if (!CreateTallyExpression(comparison, (uint)count.Value, out result))
                return false;

            CopyLocation(result);
            return true;
        }

        internal static bool CreateTallyExpression(ExpressionBase comparison, uint count, out ExpressionBase result)
        {
            if (!CanBeTallied(comparison, RequirementType.ResetIf, out result))
                return false;

            var tally = new TalliedRequirementExpression { HitTarget = count };

            var reqClause = comparison as RequirementClauseExpression;
            if (reqClause != null && reqClause.Operation == ConditionalOperation.And &&
                reqClause.Conditions.OfType<BehavioralRequirementExpression>().Any(c => c.Behavior == RequirementType.ResetIf))
            {
                // split the reset conditions out
                var newClause = new RequirementClauseExpression { Operation = ConditionalOperation.And };
                foreach (var c in reqClause.Conditions)
                {
                    var behavioral = c as BehavioralRequirementExpression;
                    if (behavioral != null && behavioral.Behavior == RequirementType.ResetIf)
                        tally.AddResetCondition(behavioral.Condition);
                    else
                        newClause.AddCondition(c);
                }

                tally.AddTalliedCondition(newClause);
            }
            else
            {
                tally.AddTalliedCondition(comparison);
            }

            result = tally;
            return true;
        }

        public static bool CanBeTallied(ExpressionBase comparison, RequirementType allowedNever, out ExpressionBase result)
        {
            var clause = comparison as RequirementClauseExpression;
            if (clause != null)
            {
                if (clause.Operation == ConditionalOperation.And && allowedNever == RequirementType.ResetNextIf)
                {
                    bool hasReset = false;
                    bool hasNonReset = false;
                    foreach (var condition in clause.Conditions)
                    {
                        if (!CanBeTallied(condition, RequirementType.ResetIf, out result))
                            return false;

                        var behavioralReq = condition as BehavioralRequirementExpression;
                        if (behavioralReq != null && behavioralReq.Behavior == RequirementType.ResetIf)
                            hasReset = true;
                        else
                            hasNonReset = true;
                    }

                    if (hasReset && !hasNonReset)
                    {
                        result = new ErrorExpression("subclause must have at least one non-never expression", comparison);
                        return false;
                    }
                }
                else
                {
                    foreach (var condition in clause.Conditions)
                    {
                        if (!CanBeTallied(condition, allowedNever, out result))
                            return false;
                    }
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

            string functionName = null;

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
            {
                if (behavioral.Behavior == allowedNever)
                {
                    result = null;
                    return true;
                }

                functionName = BehavioralRequirementExpression.GetFunctionName(behavioral.Behavior);
            }

            if (comparison is MeasuredRequirementExpression)
                functionName = "measured";

            if (functionName != null)
                result = new ErrorExpression(functionName + " not allowed in subclause", comparison);
            else
                result = new ErrorExpression("comparison did not evaluate to a valid comparison", comparison);

            return false;
        }
    }
}
