using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser
{
    internal class ScriptInterpreterAchievementBuilder : AchievementBuilder
    {
        public ScriptInterpreterAchievementBuilder() : base()
        {
        }

        private static void FlattenOrClause(RequirementExpressionBase clause, List<RequirementExpressionBase> flattened)
        {
            var reqClause = clause as RequirementClauseExpression;
            if (reqClause != null && reqClause.Operation == ConditionalOperation.Or)
            {
                foreach (var condition in reqClause.Conditions)
                    FlattenOrClause(condition, flattened);
            }
            else
            {
                flattened.Add(clause);
            }
        }

        private static ExpressionBase CrossMultiplyOrConditions(List<RequirementExpressionBase> orConditions, List<RequirementExpressionBase> andConditions = null)
        {
            // This creates a combinatorial collection from one or more collections of OR'd conditions.
            // Redundancies will be optimized out later.
            //
            // (A || B)                         => (A || B)
            //
            // (A || B) && C                    => (A && C) || (B && C)
            //
            // (A || B) && (C || D)             => (A && C) || (A && D) || (B && C) || (B && D)
            //
            // (A || B || C) && (D || E || F)   => (A && D) || (A && E) || (A && F) ||
            //                                     (B && D) || (B && E) || (B && F) ||
            //                                     (C && D) || (C && E) || (C && F)
            //
            // (A || B) && (C || D) && (E || F) => (A && C && E) ||
            //                                     (A && C && F) ||
            //                                     (A && D && E) ||
            //                                     (A && D && F) ||
            //                                     (B && C && E) ||
            //                                     (B && C && F) ||
            //                                     (B && D && E) ||
            //                                     (B && D && F)
            // ...

            // first turn the OR trees into flat lists -- (A || (B || (C || D))) -> A, B, C, D
            var flattenedClauses = new List<List<RequirementExpressionBase>>();
            long expansionSize = 1;
            foreach (var clause in orConditions)
            {
                var flattened = new List<RequirementExpressionBase>();
                FlattenOrClause(clause, flattened);
                flattenedClauses.Add(flattened);

                expansionSize *= flattened.Count();
            }

            if (expansionSize >= 20)
            {
                int insertAt = (andConditions != null) ? andConditions.Count : 0;
                for (int i = flattenedClauses.Count -1; i >= 0; i--)
                {
                    var flattened = flattenedClauses[i];
                    var orNextChain = BuildOrNextChain(flattened);
                    if (orNextChain != null)
                    {
                        expansionSize /= flattened.Count();

                        if (andConditions != null)
                        {
                            flattenedClauses.RemoveAt(i);
                            andConditions.Insert(insertAt, orNextChain);
                        }
                        else
                        {
                            flattened.Clear();
                            flattened.Add(orNextChain);
                        }
                    }
                }
            }

            if (flattenedClauses.Count == 0)
                return null;

            const int MAX_EXPANSION_SIZE = 10000;
            if (expansionSize > MAX_EXPANSION_SIZE)
                return new ErrorExpression(String.Format("Expansion of complex clause would result in {0} alt groups (exceeds {1} limit)", expansionSize, MAX_EXPANSION_SIZE));

            // then, create an alt group for every possible combination of items from each of the flattened lists
            var context = new TriggerBuilderContext();
            var numFlattenedClauses = flattenedClauses.Count();
            var partIndex = new int[numFlattenedClauses];
            var parts = new List<RequirementExpressionBase>();
            do
            {
                var andClause = new RequirementClauseExpression
                { 
                    Operation = ConditionalOperation.And,
                    Location = flattenedClauses[0][partIndex[0]].Location
                };
                for (int clauseIndex = 0; clauseIndex < numFlattenedClauses; clauseIndex++)
                {
                    var expression = flattenedClauses[clauseIndex][partIndex[clauseIndex]];
                    andClause.AddCondition(expression);
                }

                parts.Add(andClause.Optimize(context));

                int i = numFlattenedClauses - 1;
                do
                {
                    if (++partIndex[i] < flattenedClauses[i].Count)
                        break;

                    if (i == 0)
                    {
                        var orClause = new RequirementClauseExpression
                        {
                            Operation = ConditionalOperation.Or,
                            Location = parts[0].Location
                        };
                        for (i = 0; i < parts.Count; i++)
                            orClause.AddCondition(parts[i]);

                        return orClause;
                    }

                    partIndex[i--] = 0;
                } while (true);
            } while (true);
        }

        private static bool IsValidInOrNextChain(ExpressionBase expression)
        {
            switch (expression.Type)
            {
                case ExpressionType.IntegerConstant:
                    return true;

                case ExpressionType.Comparison:
                    var comparison = (ComparisonExpression)expression;
                    return IsValidInOrNextChain(comparison.Left) && IsValidInOrNextChain(comparison.Right);

                case ExpressionType.Requirement:
                    var clause = expression as RequirementConditionExpression;
                    if (clause != null)
                        return IsValidInOrNextChain(clause.Left) && IsValidInOrNextChain(clause.Right);

                    var tallied = expression as TalliedRequirementExpression;
                    if (tallied != null)
                    {
                        if (tallied.HitTarget > 0)
                            return false;
                        foreach (var condition in tallied.Conditions)
                        {
                            if (!IsValidInOrNextChain(condition))
                                return false;
                        }
                    }

                    if (expression is BehavioralRequirementExpression)
                        return false;

                    return true;

                case ExpressionType.FunctionCall:
                    var funcCall = (FunctionCallExpression)expression;

                    // only memory accessor functions are allowed. all other functions
                    // are assumed to flag the condition or set a hitcount on it.
                    var globalScope = AchievementScriptInterpreter.GetGlobalScope();
                    var funcDef = globalScope.GetFunction(funcCall.FunctionName.Name);
                    if (funcDef is MemoryAccessorFunction)
                        return true;

                    if (funcDef is AlwaysFalseFunction)
                        return true;

                    if (funcDef is AlwaysTrueFunction)
                        return true;

                    return false;

                case ExpressionType.MemoryAccessor:
                    return true;

                default:
                    return false;
            }
        }

        private static RequirementExpressionBase BuildOrNextChain(List<RequirementExpressionBase> clause)
        {
            if (clause.Count == 1)
                return clause[0];

            foreach (var condition in clause)
            {
                if (!IsValidInOrNextChain(condition))
                    return null;
            }

            var newClause = new RequirementClauseExpression.OrNextRequirementClauseExpression { Location = clause[0].Location };
            RequirementClauseExpression andNextClause = null;
            foreach (var condition in clause)
            {
                var reqClause = condition as RequirementClauseExpression;
                if (reqClause != null && reqClause.Operation == ConditionalOperation.And)
                {
                    // if there's more than one AndNext subclause, we can't convert it to an OrNext chain
                    if (andNextClause != null)
                        return null;
                    andNextClause = reqClause;
                }

                newClause.AddCondition(condition);
            }

            return newClause;
        }

        private static bool SortConditions(RequirementExpressionBase expression,
            List<RequirementExpressionBase> andedConditions, List<RequirementExpressionBase> orConditions, out ErrorExpression error)
        {
            var reqClause = expression as RequirementClauseExpression;
            if (reqClause != null)
            {
                switch (reqClause.Operation)
                {
                    case ConditionalOperation.And:
                        foreach (var clause in reqClause.Conditions)
                        {
                            if (!SortConditions(clause, andedConditions, orConditions, out error))
                                return false;
                        }
                        break;

                    case ConditionalOperation.Or:
                        orConditions.Add(reqClause);
                        break;

                    default:
                        error = new ErrorExpression("Unexpected condition: " + reqClause.Operation, reqClause);
                        return false;
                }

                error = null;
                return true;
            }

            andedConditions.Add(expression);
            error = null;
            return true;
        }

        internal bool PopulateFromExpression(RequirementExpressionBase expression, InterpreterScope scope, out ErrorExpression error)
        {
            var andedConditions = new List<RequirementExpressionBase>();
            var orConditions = new List<RequirementExpressionBase>();
            if (!SortConditions(expression, andedConditions, orConditions, out error))
                return false;

            if (orConditions.Count() > 1)
            {
                var altPart = CrossMultiplyOrConditions(orConditions, andedConditions);
                if (altPart != null)
                {
                    if (altPart.Type == ExpressionType.Error)
                    {
                        expression.CopyLocation(altPart);
                        error = (ErrorExpression)altPart;
                        return false;
                    }

                    andedConditions.Add((RequirementExpressionBase)altPart);
                }
            }
            else if (orConditions.Count() == 1)
            {
                andedConditions.Add(orConditions[0]);
            }

            var context = new AchievementBuilderContext(this);
            foreach (var condition in andedConditions)
            {
                var triggerExpression = condition as ITriggerExpression;
                if (triggerExpression == null)
                {
                    error = new ErrorExpression("Cannot build trigger from " + condition, condition);
                    return false;
                }    

                error = triggerExpression.BuildTrigger(context);
                if (error != null)
                {
                    switch (condition.Type)
                    {
                        case ExpressionType.Comparison:
                        case ExpressionType.Conditional:
                            break;

                        default:
                            error = ErrorExpression.WrapError(error, "Invalid condition", condition);
                            break;
                    }
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Populates the <see cref="AchievementBuilder"/> from an expression.
        /// </summary>
        /// <param name="expression">The expression to populate from.</param>
        /// <returns><c>null</c> if successful, otherwise an error message indicating why it failed.</returns>
        public string PopulateFromExpression(ExpressionBase expression)
        {
            var context = new TriggerBuilderContext { Trigger = CoreRequirements };
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope()) { Context = context };
            ErrorExpression error;

            ExpressionBase result;
            if (!expression.ReplaceVariables(scope, out result))
            {
                error = (ErrorExpression)result;
                if (error.InnerError != null)
                    return error.InnermostError.Message;

                return error.Message;
            }

            var requirement = result as RequirementExpressionBase;
            if (requirement == null)
                return "expression is not a requirement expression";

            if (!PopulateFromExpression(requirement, scope, out error))
                return error.Message;

            return null;
        }
    }
}