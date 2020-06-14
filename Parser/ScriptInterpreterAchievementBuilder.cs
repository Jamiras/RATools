using RATools.Data;
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

        /// <summary>
        /// Begins an new alt group.
        /// </summary>
        /// <returns>The requirement collection for the new alt group.</returns>
        private void BeginAlt(TriggerBuilderContext context)
        {
            if (ReferenceEquals(context.Trigger, CoreRequirements) || context.Trigger.Count > 0)
            {
                var newAlt = new List<Requirement>();
                context.Trigger = newAlt;
                AlternateRequirements.Add(newAlt);
            }
        }

        private static bool NormalizeNots(ref ExpressionBase expression, out ParseErrorExpression error)
        {
            error = null;

            // not a condition - don't need to worry about it
            var condition = expression as ConditionalExpression;
            if (condition == null)
                return true;

            // not a not, just recurse
            if (condition.Operation != ConditionalOperation.Not)
            {
                var left = condition.Left;
                if (!NormalizeNots(ref left, out error))
                    return false;

                var right = condition.Right;
                if (!NormalizeNots(ref right, out error))
                    return false;

                if (!ReferenceEquals(left, condition.Left) || !ReferenceEquals(right, condition.Right))
                    expression = new ConditionalExpression(left, condition.Operation, right);

                return true;
            }

            // found a not - eliminate it
            var operand = ((ConditionalExpression)expression).Right;

            // logical inversion
            condition = operand as ConditionalExpression;
            if (condition != null)
            {
                switch (condition.Operation)
                {
                    case ConditionalOperation.Not:
                        // !(!A) => A
                        expression = condition.Right;
                        break;

                    case ConditionalOperation.And:
                        // !(A && B) => !A || !B
                        expression = new ConditionalExpression(
                            new ConditionalExpression(null, ConditionalOperation.Not, condition.Left),
                            ConditionalOperation.Or,
                            new ConditionalExpression(null, ConditionalOperation.Not, condition.Right));
                        break;

                    case ConditionalOperation.Or:
                        // !(A || B) => !A && !B
                        expression = new ConditionalExpression(
                            new ConditionalExpression(null, ConditionalOperation.Not, condition.Left),
                            ConditionalOperation.And,
                            new ConditionalExpression(null, ConditionalOperation.Not, condition.Right));
                        break;

                    default:
                        throw new NotImplementedException("Unsupported condition operation");
                }

                return NormalizeNots(ref expression, out error);
            }

            // comparative inversion
            var comparison = operand as ComparisonExpression;
            if (comparison != null)
            {
                // !(A == B) => A != B, !(A < B) => A >= B, ...
                expression = new ComparisonExpression(
                    comparison.Left,
                    ComparisonExpression.GetOppositeComparisonOperation(comparison.Operation),
                    comparison.Right);

                return NormalizeNots(ref expression, out error);
            }

            var function = operand as FunctionCallExpression;
            if (function != null)
            {
                if (function.FunctionName.Name == "always_true")
                {
                    expression = new FunctionCallExpression("always_false", function.Parameters);
                    return true;
                }

                if (function.FunctionName.Name == "always_false")
                {
                    expression = new FunctionCallExpression("always_true", function.Parameters);
                    return true;
                }
            }

            // unsupported inversion
            error = new ParseErrorExpression("! operator cannot be applied to " + operand.Type, operand);
            return false;
        }

        private static void FlattenOrClause(ExpressionBase clause, List<ExpressionBase> flattened)
        {
            var condition = clause as ConditionalExpression;
            if (condition != null && condition.Operation == ConditionalOperation.Or)
            {
                FlattenOrClause(condition.Left, flattened);
                FlattenOrClause(condition.Right, flattened);
            }
            else
            {
                flattened.Add(clause);
            }
        }

        private static ExpressionBase CrossMultiplyOrConditions(List<ExpressionBase> orConditions)
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
            var flattenedClauses = new List<List<ExpressionBase>>();
            var expansionSize = 1;
            foreach (var clause in orConditions)
            {
                var flattened = new List<ExpressionBase>();
                FlattenOrClause(clause, flattened);
                flattenedClauses.Add(flattened);

                expansionSize *= flattened.Count();
            }

            if (expansionSize >= 20)
            {
                foreach (var flattened in flattenedClauses)
                {
                    var orNextChain = BuildOrNextChain(flattened);
                    if (orNextChain != null)
                    {
                        expansionSize /= flattened.Count();

                        flattened.Clear();
                        flattened.Add(orNextChain);
                    }
                }
            }

            const int MAX_EXPANSION_SIZE = 10000;
            if (expansionSize > MAX_EXPANSION_SIZE)
                return new ParseErrorExpression(String.Format("Expansion of complex clause would result in {0} alt groups (exceeds {1} limit)", expansionSize, MAX_EXPANSION_SIZE));

            // then, create an alt group for every possible combination of items from each of the flattened lists
            var numFlattenedClauses = flattenedClauses.Count();
            var partIndex = new int[numFlattenedClauses];
            var parts = new List<ExpressionBase>();
            do
            {
                var andPart = flattenedClauses[numFlattenedClauses - 1][partIndex[numFlattenedClauses - 1]];
                for (int clauseIndex = numFlattenedClauses - 2; clauseIndex >= 0; clauseIndex--)
                {
                    var expression = flattenedClauses[clauseIndex][partIndex[clauseIndex]];
                    andPart = new ConditionalExpression(expression, ConditionalOperation.And, andPart);
                }

                parts.Add(andPart);

                int i = numFlattenedClauses - 1;
                do
                {
                    if (++partIndex[i] < flattenedClauses[i].Count)
                        break;

                    if (i == 0)
                    {
                        var orPart = parts[parts.Count - 1];
                        for (i = parts.Count - 2; i >= 0; i--)
                            orPart = new ConditionalExpression(parts[i], ConditionalOperation.Or, orPart);

                        return orPart;
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

                default:
                    return false;
            }
        }

        private static ExpressionBase BuildOrNextChain(List<ExpressionBase> clause)
        {
            if (clause.Count == 1)
                return clause[0];

            foreach (var condition in clause)
            {
                if (!IsValidInOrNextChain(condition))
                    return null;
            }

            ExpressionBase result = null;
            foreach (var condition in clause)
            {
                if (result == null)
                    result = condition;
                else
                    result = new ConditionalExpression(result, ConditionalOperation.Or, condition);
            }

            result = new FunctionCallExpression("repeated", new ExpressionBase[]
            {
                new IntegerConstantExpression(0),
                result
            });

            return result;
        }

        private static ConditionalExpression BubbleUpOrs(ConditionalExpression condition)
        {
            bool modified = false;
            bool hasChildOr = false;

            ConditionalExpression left = condition.Left as ConditionalExpression;
            if (left != null)
            {
                left = BubbleUpOrs(left);
                modified |= !ReferenceEquals(left, condition.Left);
                hasChildOr |= (left.Operation == ConditionalOperation.Or);
            }

            ConditionalExpression right = condition.Right as ConditionalExpression;
            if (right != null)
            {
                right = BubbleUpOrs(right);
                modified |= !ReferenceEquals(right, condition.Right);
                hasChildOr |= (right.Operation == ConditionalOperation.Or);
            }

            if (modified)
            {
                var newCondition = new ConditionalExpression(
                    left ?? condition.Left, condition.Operation, right ?? condition.Right);
                condition.CopyLocation(newCondition);
                condition = newCondition;
            }

            if (condition.Operation == ConditionalOperation.And && hasChildOr)
            {
                var orConditions = new List<ExpressionBase>();
                orConditions.Add(left ?? condition.Left);
                orConditions.Add(right ?? condition.Right);

                var expression = CrossMultiplyOrConditions(orConditions);
                return (ConditionalExpression)expression;
            }

            return condition;
        }

        private static bool SortConditions(ExpressionBase expression, List<ExpressionBase> andedConditions, List<ExpressionBase> orConditions, out ParseErrorExpression error)
        {
            var condition = expression as ConditionalExpression;
            if (condition == null)
            {
                andedConditions.Add(expression);
                error = null;
                return true;
            }

            switch (condition.Operation)
            {
                case ConditionalOperation.And:
                    if (!SortConditions(condition.Left, andedConditions, orConditions, out error))
                        return false;
                    if (!SortConditions(condition.Right, andedConditions, orConditions, out error))
                        return false;
                    break;

                case ConditionalOperation.Or:
                    condition = BubbleUpOrs(condition);
                    orConditions.Add(condition);
                    break;

                default:
                    error = new ParseErrorExpression("Unexpected condition: " + condition.Operation, condition);
                    return false;
            }

            error = null;
            return true;
        }

        internal bool PopulateFromExpression(ExpressionBase expression, InterpreterScope scope, out ParseErrorExpression error)
        {
            if (!NormalizeNots(ref expression, out error))
                return false;

            var andedConditions = new List<ExpressionBase>();
            var orConditions = new List<ExpressionBase>();
            if (!SortConditions(expression, andedConditions, orConditions, out error))
                return false;

            if (orConditions.Count() > 1)
            {
                var altPart = CrossMultiplyOrConditions(orConditions);
                if (altPart.Type == ExpressionType.ParseError)
                {
                    expression.CopyLocation(altPart);
                    error = (ParseErrorExpression)altPart;
                    return false;
                }

                andedConditions.Add(altPart);
            }
            else if (orConditions.Count() == 1)
            {
                andedConditions.Add(orConditions[0]);
            }

            var context = new TriggerBuilderContext { Trigger = CoreRequirements };
            var innerScope = new InterpreterScope(scope) { Context = context };
            foreach (var condition in andedConditions)
            {
                error = ExecuteAchievementClause(condition, innerScope);

                if (error != null)
                {
                    if (error.InnerError != null)
                        error = new ParseErrorExpression(error.InnermostError.Message, error.Line, error.Column, error.EndLine, error.EndColumn);
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

            ExpressionBase result;
            if (!expression.ReplaceVariables(scope, out result))
                return ((ParseErrorExpression)result).Message;

            ParseErrorExpression error;
            if (!PopulateFromExpression(result, scope, out error))
                return error.Message;

            return null;
        }

        private ParseErrorExpression ExecuteAchievementExpression(ExpressionBase expression, InterpreterScope scope)
        {
            ExpressionBase operand;

            switch (expression.Type)
            {
                case ExpressionType.FunctionCall:
                    return ExecuteAchievementFunction((FunctionCallExpression)expression, scope);

                case ExpressionType.Conditional:
                    return ExecuteAchievementConditional((ConditionalExpression)expression, scope);

                case ExpressionType.Comparison:
                    return ExecuteAchievementComparison((ComparisonExpression)expression, scope);

                case ExpressionType.Mathematic:
                    return ExecuteAchievementMathematic((MathematicExpression)expression, scope);

                case ExpressionType.Variable:
                    if (!((VariableExpression)expression).ReplaceVariables(scope, out operand))
                        return new ParseErrorExpression(operand, expression);

                    return ExecuteAchievementExpression(operand, scope);
            }

            return new ParseErrorExpression("Cannot generate trigger from " + expression.Type, expression);
        }

        private ParseErrorExpression ExecuteAchievementClause(ExpressionBase expression, InterpreterScope scope)
        {
            var error = ExecuteAchievementExpression(expression, scope);
            if (error != null)
                return error;

            var context = scope.GetContext<TriggerBuilderContext>();
            if (context.LastRequirement == null || context.LastRequirement.Operator == RequirementOperator.None)
                return new ParseErrorExpression("Incomplete trigger condition", expression);

            return null;
        }

        private ParseErrorExpression ExecuteAchievementMathematic(MathematicExpression mathematic, InterpreterScope scope)
        {
            var operation = mathematic.Operation;
            if (operation != MathematicOperation.Add && operation != MathematicOperation.Subtract)
                return new ParseErrorExpression("Cannot normalize expression to eliminate " + MathematicExpression.GetOperatorType(operation), mathematic);

            var left = mathematic.Left;
            var context = scope.GetContext<TriggerBuilderContext>();
            ParseErrorExpression error;

            ExpressionBase right;
            if (!mathematic.Right.ReplaceVariables(scope, out right))
                return (ParseErrorExpression)right;

            var integerOperand = right as IntegerConstantExpression;
            if (integerOperand != null)
            {
                context.Trigger.Add(new Requirement
                {
                    Type = (operation == MathematicOperation.Add) ? RequirementType.AddSource : RequirementType.SubSource,
                    Left = new Field { Type = FieldType.Value, Value = (uint)integerOperand.Value },
                    Operator = RequirementOperator.None,
                    Right = new Field()
                });
            }

            if (operation == MathematicOperation.Subtract && (right is FunctionCallExpression || right is MathematicExpression))
            {
                // if subtracting a non-integer, swap the order to perform a SubSource
                var requirements = new List<Requirement>();
                var innerContext = new TriggerBuilderContext() { Trigger = requirements };
                var innerScope = new InterpreterScope(scope) { Context = innerContext };

                // generate the condition for the right side
                error = ExecuteAchievementExpression(right, innerScope);
                if (error != null)
                    return error;

                foreach (var requirement in requirements)
                {
                    switch (requirement.Type)
                    {
                        case RequirementType.None:
                        case RequirementType.AddSource:
                            requirement.Type = RequirementType.SubSource;
                            break;
                        case RequirementType.SubSource:
                            requirement.Type = RequirementType.AddSource;
                            break;
                        case RequirementType.AddAddress:
                            // AddAddress is allowed as long as it's not the last requirement
                            if (ReferenceEquals(requirement, requirements.Last()))
                                return new ParseErrorExpression("Cannot normalize expression for negation", mathematic);
                            break;
                        default:
                            return new ParseErrorExpression("Cannot normalize expression for negation", mathematic);
                    }

                    context.Trigger.Add(requirement);
                }

                right = mathematic.Left;
                operation = MathematicOperation.Add;
            }
            else
            {
                // generate the condition for the first expression
                error = ExecuteAchievementExpression(left, scope);
                if (error != null)
                    return error;
            }

            if (integerOperand != null)
                return null;

            if (operation == MathematicOperation.Add)
            {
                // adding two memory accessors - make sure previous is AddSource or SubSource
                if (context.LastRequirement.Type != RequirementType.SubSource)
                {
                    context.LastRequirement.Type = RequirementType.AddSource;
                    context.LastRequirement.Operator = RequirementOperator.None;
                    context.LastRequirement.Right = new Field();
                }
            }
            else
            {
                return new ParseErrorExpression(String.Format("Cannot normalize expression to eliminate {0}", MathematicExpression.GetOperatorType(mathematic.Operation)), mathematic);
            }

            var integerConstant = right as IntegerConstantExpression;
            if (integerConstant != null)
            {
                context.Trigger.Add(new Requirement
                {
                    Type = RequirementType.None,
                    Left = new Field { Type = FieldType.Value, Value = (uint)integerConstant.Value },
                    Operator = RequirementOperator.None,
                    Right = new Field()
                });
                return null;
            }

            // generate the condition for the second expression
            error = ExecuteAchievementExpression(right, scope);
            if (error != null)
                error = new ParseErrorExpression(error.Message, mathematic);
            return error;
        }

        private ParseErrorExpression ExecuteAchievementConditional(ConditionalExpression condition, InterpreterScope scope)
        {
            ParseErrorExpression error;
            var context = scope.GetContext<TriggerBuilderContext>();

            switch (condition.Operation)
            {
                case ConditionalOperation.Not:
                    return new ParseErrorExpression("! operator should have been normalized out", condition);

                case ConditionalOperation.And:
                    error = ExecuteAchievementClause(condition.Left, scope);
                    if (error != null)
                        return error;

                    error = ExecuteAchievementClause(condition.Right, scope);
                    if (error != null)
                        return error;

                    return null;

                case ConditionalOperation.Or:
                    BeginAlt(context);
                    error = ExecuteAchievementClause(condition.Left, scope);
                    if (error != null)
                        return error;

                    BeginAlt(context);
                    error = ExecuteAchievementClause(condition.Right, scope);
                    if (error != null)
                        return error;

                    return null;
            }

            return new ParseErrorExpression("Unsupported conditional", condition);
        }

        private ParseErrorExpression ExecuteAchievementComparison(ComparisonExpression comparison, InterpreterScope scope)
        {
            var context = scope.GetContext<TriggerBuilderContext>();
            var left = comparison.Left;
            var right = comparison.Right;

            var op = GetRequirementOperator(comparison.Operation);

            if (left.Type == ExpressionType.IntegerConstant)
            {
                if (right.Type == ExpressionType.IntegerConstant)
                {
                    // comparing two constants, convert to always_true or always_false
                    var requirement = new Requirement
                    {
                        Left = new Field { Type = FieldType.Value, Value = (uint)((IntegerConstantExpression)left).Value },
                        Operator = op,
                        Right = new Field { Type = FieldType.Value, Value = (uint)((IntegerConstantExpression)right).Value },
                    };

                    if (requirement.Evaluate() == true)
                        context.Trigger.Add(AlwaysTrueFunction.CreateAlwaysTrueRequirement());
                    else
                        context.Trigger.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());

                    return null;
                }

                // swap the operands and operator so the constant is on the right
                var temp = left;
                left = right;
                right = temp;

                op = Requirement.GetReversedRequirementOperator(op);
            }

            var error = ExecuteAchievementExpression(left, scope);
            if (error != null)
                return error;

            var integerRight = right as IntegerConstantExpression;
            if (integerRight != null)
            {
                int newValue = integerRight.Value;

                var requirement = context.LastRequirement;
                requirement.Operator = op;
                requirement.Right = new Field { Size = requirement.Left.Size, Type = FieldType.Value, Value = (uint)newValue };
            }
            else
            {
                error = ExecuteAchievementExpression(right, scope);
                if (error != null)
                    return error;

                var extraRequirement = context.LastRequirement;
                ((IList<Requirement>)context.Trigger).RemoveAt(context.Trigger.Count - 1);

                var requirement = context.LastRequirement;
                if (requirement != null)
                {
                    if (requirement.Type == RequirementType.AddAddress)
                    {
                        // if right side is an AddAddress chain, it must match the left side
                        var addAddressRequirements = new List<Requirement>();
                        do
                        {
                            addAddressRequirements.Add(requirement);
                            ((IList<Requirement>)context.Trigger).RemoveAt(context.Trigger.Count - 1);

                            requirement = context.LastRequirement;
                        } while (requirement.Type == RequirementType.AddAddress);

                        if (context.Trigger.Count <= addAddressRequirements.Count)
                            return new ParseErrorExpression("Indirect memory addresses must match on both sides of a comparison", comparison);

                        for (int i = 0; i < addAddressRequirements.Count; i++)
                        {
                            var previousRequirement = context.Trigger.ElementAt(context.Trigger.Count - addAddressRequirements.Count - 1 + i);
                            if (previousRequirement != addAddressRequirements[i])
                                return new ParseErrorExpression("Indirect memory addresses must match on both sides of a comparison", comparison);
                        }

                        // AddAddress chains match, merge the conditions
                        requirement.Operator = op;
                        requirement.Right = extraRequirement.Left;
                    }
                    else if (context.Trigger.Count > 1 && context.Trigger.ElementAt(context.Trigger.Count - 2).Type == RequirementType.AddAddress)
                    {
                        // if left side is an AddAddress chain, but right side is a not, we have to keep the
                        // dummy condition to prevent the AddAddress from modifying the memory address on the
                        // right side. integers are handled above.
                        requirement.Type = RequirementType.AddSource;
                        extraRequirement.Right = extraRequirement.Left;
                        extraRequirement.Left = new Field { Type = FieldType.Value, Value = 0 };
                        extraRequirement.Operator = op;
                        context.Trigger.Add(extraRequirement);
                    }
                    else
                    {
                        // no AddAddress on either side, just merge the conditions
                        requirement.Operator = op;
                        requirement.Right = extraRequirement.Left;
                    }
                }
            }

            return null;
        }

        private static RequirementOperator GetRequirementOperator(ComparisonOperation comparisonOperation)
        {
            switch (comparisonOperation)
            {
                case ComparisonOperation.Equal: return RequirementOperator.Equal;
                case ComparisonOperation.NotEqual: return RequirementOperator.NotEqual;
                case ComparisonOperation.LessThan: return RequirementOperator.LessThan;
                case ComparisonOperation.LessThanOrEqual: return RequirementOperator.LessThanOrEqual;
                case ComparisonOperation.GreaterThan: return RequirementOperator.GreaterThan;
                case ComparisonOperation.GreaterThanOrEqual: return RequirementOperator.GreaterThanOrEqual;
                default: return RequirementOperator.None;
            }
        }

        private ParseErrorExpression ExecuteAchievementFunction(FunctionCallExpression functionCall, InterpreterScope scope)
        {
            ExpressionBase evaluated;
            if (!functionCall.ReplaceVariables(scope, out evaluated))
                return (ParseErrorExpression)evaluated;

            functionCall = evaluated as FunctionCallExpression;
            if (functionCall != null)
            {
                var context = scope.GetContext<TriggerBuilderContext>();
                return context.CallFunction(functionCall, scope);
            }

            return ExecuteAchievementClause(evaluated, scope);
        }
    }
}