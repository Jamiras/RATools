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
            _equalityModifiers = new Stack<ValueModifier>();
        }

        private Stack<ValueModifier> _equalityModifiers;

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
            List<List<ExpressionBase>> flattenedClauses = new List<List<ExpressionBase>>();
            foreach (var clause in orConditions)
            {
                var flattened = new List<ExpressionBase>();
                FlattenOrClause(clause, flattened);
                flattenedClauses.Add(flattened);
            }

            // then, create an alt group for every possible combination of items from each of the flattened lists
            var numFlattendClauses = flattenedClauses.Count();
            var partIndex = new int[numFlattendClauses];
            var parts = new List<ExpressionBase>();
            do
            {
                var andPart = flattenedClauses[numFlattendClauses - 1][partIndex[numFlattendClauses - 1]];
                for (int clauseIndex = numFlattendClauses - 2; clauseIndex >= 0; clauseIndex--)
                {
                    var expression = flattenedClauses[clauseIndex][partIndex[clauseIndex]];
                    andPart = new ConditionalExpression(expression, ConditionalOperation.And, andPart);
                }

                parts.Add(andPart);

                int i = numFlattendClauses - 1;
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

            if (orConditions.Count() != 0)
            {
                var altPart = CrossMultiplyOrConditions(orConditions);
                andedConditions.Add(altPart);
            }

            var context = new TriggerBuilderContext { Trigger = CoreRequirements };
            var innerScope = new InterpreterScope(scope) { Context = context };
            foreach (var condition in andedConditions)
            {
                error = ExecuteAchievementExpression(condition, innerScope);
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

        private ParseErrorExpression ExecuteAchievementMathematic(MathematicExpression mathematic, InterpreterScope scope)
        {
            var left = mathematic.Left;
            var operation = mathematic.Operation;
            var context = scope.GetContext<TriggerBuilderContext>();
            ParseErrorExpression error;

            ExpressionBase right;
            if (!mathematic.Right.ReplaceVariables(scope, out right))
                return (ParseErrorExpression)right;

            if (operation == MathematicOperation.Subtract && (right is FunctionCallExpression || right is MathematicExpression))
            {
                // if subtracting a non-integer, swap the order to perform a SubSource
                var newEqualityModifiers = new Stack<ValueModifier>();
                var oldEqualityModifiers = _equalityModifiers;
                _equalityModifiers = newEqualityModifiers;

                var requirements = new List<Requirement>();
                var innerContext = new TriggerBuilderContext() { Trigger = requirements };
                var innerScope = new InterpreterScope(scope) { Context = innerContext };

                // generate the condition for the right side
                error = ExecuteAchievementExpression(right, innerScope);
                _equalityModifiers = oldEqualityModifiers;
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
                        default:
                            return new ParseErrorExpression("Cannot normalize expression for negation", mathematic);
                    }

                    context.Trigger.Add(requirement);
                }

                foreach (var modifier in newEqualityModifiers)
                {
                    switch (modifier.Operation)
                    {
                        case MathematicOperation.Add:
                            _equalityModifiers.Push(new ValueModifier(MathematicOperation.Subtract, modifier.Amount));
                            break;
                        case MathematicOperation.Subtract:
                            _equalityModifiers.Push(new ValueModifier(MathematicOperation.Add, modifier.Amount));
                            break;
                        default:
                            return new ParseErrorExpression("Cannot normalize expression for negation", mathematic);
                    }
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

            var integerOperand = right as IntegerConstantExpression;
            if (integerOperand != null)
            {
                var oppositeOperation = MathematicExpression.GetOppositeOperation(operation);
                if (oppositeOperation == MathematicOperation.None)
                    return new ParseErrorExpression(String.Format("Cannot normalize expression to eliminate {0}", MathematicExpression.GetOperatorType(mathematic.Operation)), mathematic);

                var priority = MathematicExpression.GetPriority(mathematic.Operation);
                if (priority != MathematicPriority.Add)
                {
                    if (context.Trigger.Count > 1)
                    {
                        var previousRequirementType = context.Trigger.ElementAt(context.Trigger.Count - 2).Type;
                        if (previousRequirementType == RequirementType.AddSource || previousRequirementType == RequirementType.SubSource)
                            return new ParseErrorExpression(String.Format("Cannot normalize expression to eliminate {0}", MathematicExpression.GetOperatorType(mathematic.Operation)), mathematic);
                    }

                    if (_equalityModifiers.Any(e => MathematicExpression.GetPriority(e.Operation) != priority))
                        return new ParseErrorExpression(String.Format("Cannot normalize expression to eliminate {0}", MathematicExpression.GetOperatorType(mathematic.Operation)), mathematic);
                }

                _equalityModifiers.Push(new ValueModifier(oppositeOperation, integerOperand.Value));
                return null;
            }

            if (operation == MathematicOperation.Add)
            {
                foreach (var modifier in _equalityModifiers)
                {
                    if (MathematicExpression.GetPriority(modifier.Operation) != MathematicPriority.Add)
                        return new ParseErrorExpression(String.Format("Cannot normalize expression to eliminate {0}", MathematicExpression.GetOperatorType(MathematicExpression.GetOppositeOperation(modifier.Operation))), mathematic);
                }

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
                    error = ExecuteAchievementExpression(condition.Left, scope);
                    if (error != null)
                        return error;

                    error = ExecuteAchievementExpression(condition.Right, scope);
                    if (error != null)
                        return error;

                    return null;

                case ConditionalOperation.Or:
                    BeginAlt(context);
                    error = ExecuteAchievementExpression(condition.Left, scope);
                    if (error != null)
                        return error;

                    BeginAlt(context);
                    error = ExecuteAchievementExpression(condition.Right, scope);
                    if (error != null)
                        return error;

                    return null;
            }

            return new ParseErrorExpression("Unsupported conditional", condition);
        }

        private ParseErrorExpression ExecuteAchievementComparison(ComparisonExpression comparison, InterpreterScope scope)
        {
            _equalityModifiers.Clear();

            var context = scope.GetContext<TriggerBuilderContext>();
            var insertIndex = context.Trigger.Count;

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

                    if (Evaluate(requirement))
                        context.Trigger.Add(AlwaysTrueFunction.CreateAlwaysTrueRequirement());
                    else
                        context.Trigger.Add(AlwaysFalseFunction.CreateAlwaysFalseRequirement());

                    return null;
                }

                // swap the operands and operator so the constant is on the right
                var temp = left;
                left = right;
                right = temp;

                op = GetReversedRequirementOperator(op);
            }

            var error = ExecuteAchievementExpression(left, scope);
            if (error != null)
                return error;

            var integerRight = right as IntegerConstantExpression;
            if (integerRight != null)
            {
                int newValue = integerRight.Value;

                // SubSource of a memory accessor may cause overflow - if comparing for less than,
                // modifiers should not be merged into the compare target
                if (_equalityModifiers.Count > 0 &&
                    (op == RequirementOperator.LessThan || op == RequirementOperator.LessThanOrEqual))
                {
                    bool hasSubSource = false;
                    for (int i = context.Trigger.Count - 2; i >= 0; i++)
                    {
                        var requirementType = context.Trigger.ElementAt(i).Type;
                        if (requirementType == RequirementType.SubSource)
                        {
                            hasSubSource = true;
                            break;
                        }
                        else if (requirementType != RequirementType.AddSource &&
                            requirementType != RequirementType.AddHits)
                        {
                            break;
                        }
                    }

                    if (hasSubSource)
                    {
                        var last = context.Trigger.Last();
                        context.Trigger.Remove(last);
                        foreach (var modifier in _equalityModifiers)
                        {
                            if (modifier.Operation != MathematicOperation.Subtract && modifier.Operation != MathematicOperation.Add)
                                return new ParseErrorExpression("Cannot normalize expression containing SubSource", left);

                            context.Trigger.Add(new Requirement
                            {
                                Type = (modifier.Operation == MathematicOperation.Subtract) ? RequirementType.AddSource : RequirementType.SubSource,
                                Left = new Field { Type = FieldType.Value, Value = (uint)modifier.Amount },
                                Operator = RequirementOperator.None,
                                Right = new Field()
                            });
                        }
                        context.Trigger.Add(last);

                        _equalityModifiers.Clear();
                    }
                }

                while (_equalityModifiers.Count > 0)
                {
                    var originalValue = newValue;
                    var modifier = _equalityModifiers.Pop();
                    newValue = modifier.Apply(newValue);

                    var restoredValue = modifier.Remove(newValue);
                    switch (op)
                    {
                        case RequirementOperator.Equal:
                            if (restoredValue != originalValue)
                                return new ParseErrorExpression("Result can never be true using integer math", comparison);
                            break;

                        case RequirementOperator.NotEqual:
                            if (restoredValue != originalValue)
                                return new ParseErrorExpression("Result is always true using integer math", comparison);
                            break;

                        case RequirementOperator.GreaterThanOrEqual:
                            if (restoredValue != originalValue)
                                op = RequirementOperator.GreaterThan;
                            break;
                    }
                }

                var requirement = context.LastRequirement;
                requirement.Operator = op;
                requirement.Right = new Field { Size = requirement.Left.Size, Type = FieldType.Value, Value = (uint)newValue };
            }
            else
            {
                var leftModifiers = new Stack<ValueModifier>(_equalityModifiers.Reverse());
                _equalityModifiers.Clear();

                error = ExecuteAchievementExpression(right, scope);
                if (error != null)
                    return error;

                if (leftModifiers.Count > 0 || _equalityModifiers.Count > 0)
                {
                    var rightValue = 1234567;
                    var leftValue = rightValue;
                    while (leftModifiers.Count > 0)
                    {
                        var modifier = leftModifiers.Pop();
                        leftValue = ValueModifier.Apply(leftValue, MathematicExpression.GetOppositeOperation(modifier.Operation), modifier.Amount);
                    }

                    while (_equalityModifiers.Count > 0)
                    {
                        var modifier = _equalityModifiers.Pop();
                        rightValue = ValueModifier.Apply(rightValue, MathematicExpression.GetOppositeOperation(modifier.Operation), modifier.Amount);
                    }

                    var diff = leftValue - rightValue;
                    if (diff != 0)
                    {
                        var modifier = new Requirement();

                        if (diff < 0)
                        {
                            modifier.Left = new Field { Type = FieldType.Value, Value = (uint)(-diff) };
                            modifier.Type = RequirementType.SubSource;
                        }
                        else
                        {
                            modifier.Left = new Field { Type = FieldType.Value, Value = (uint)diff };
                            modifier.Type = RequirementType.AddSource;
                        }

                        ((IList<Requirement>)context.Trigger).Insert(insertIndex, modifier);
                    }
                }

                var extraRequirement = context.LastRequirement;
                context.Trigger.Remove(extraRequirement);

                var requirement = context.LastRequirement;
                if (requirement != null)
                {
                    requirement.Operator = op;
                    requirement.Right = extraRequirement.Left;
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

        private static RequirementOperator GetReversedRequirementOperator(RequirementOperator op)
        {
            switch (op)
            {
                case RequirementOperator.Equal: return RequirementOperator.Equal;
                case RequirementOperator.NotEqual: return RequirementOperator.NotEqual;
                case RequirementOperator.LessThan: return RequirementOperator.GreaterThan;
                case RequirementOperator.LessThanOrEqual: return RequirementOperator.GreaterThanOrEqual;
                case RequirementOperator.GreaterThan: return RequirementOperator.LessThan;
                case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.LessThanOrEqual;
                default: return RequirementOperator.None;
            }
        }

        private ParseErrorExpression ExecuteAchievementFunction(FunctionCallExpression functionCall, InterpreterScope scope)
        {
            var context = scope.GetContext<TriggerBuilderContext>();
            return context.CallFunction(functionCall, scope);
        }
    }
}