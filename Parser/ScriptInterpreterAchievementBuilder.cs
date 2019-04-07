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
        private ConditionalExpression _delayedOrClause;

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

        internal bool PopulateFromExpression(ExpressionBase expression, InterpreterScope scope, out ParseErrorExpression error)
        {
            var context = new TriggerBuilderContext { Trigger = CoreRequirements };
            var parentContext = scope.GetContext<TriggerBuilderContext>();
            if (parentContext != null)
                context.IsInNot = parentContext.IsInNot;

            var innerScope = new InterpreterScope(scope) { Context = context };
            error = ExecuteAchievementExpression(expression, innerScope);
            if (error != null)
            {
                if (error.InnerError != null)
                    error = new ParseErrorExpression(error.InnermostError.Message, error.Line, error.Column, error.EndLine, error.EndColumn);
                return false;
            }

            if (_delayedOrClause != null)
            {
                BeginAlt(context);
                error = ExecuteAchievementExpression(_delayedOrClause.Left, innerScope);
                if (error != null)
                    return false;
                BeginAlt(context);
                error = ExecuteAchievementExpression(_delayedOrClause.Right, innerScope);
                if (error != null)
                    return false;
            }

            return true;
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

        private ParseErrorExpression ExecuteAchievementExpressions(ICollection<ExpressionBase> expressions, InterpreterScope scope)
        {
            foreach (var expression in expressions)
            {
                var error = ExecuteAchievementExpression(expression, scope);
                if (error != null)
                    return error;

                if (scope.IsComplete)
                    break;
            }

            return null;
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
                    var innerScope = new InterpreterScope(scope) { Context = new TriggerBuilderContext { Trigger = context.Trigger, IsInNot = !context.IsInNot } };
                    error = ExecuteAchievementExpression(condition.Right, innerScope);
                    if (error != null)
                        return error;
                    return null;

                case ConditionalOperation.And:
                    if (context.IsInNot)
                        BeginAlt(context);
                    error = ExecuteAchievementExpression(condition.Left, scope);
                    if (error != null)
                        return error;
                    if (context.IsInNot)
                        BeginAlt(context);
                    error = ExecuteAchievementExpression(condition.Right, scope);
                    if (error != null)
                        return error;
                    return null;

                case ConditionalOperation.Or:
                    if (!context.IsInNot)
                    {
                        // make sure we have all the Core requirements defined before
                        // we create the first alt group
                        if (ReferenceEquals(context.Trigger, CoreRequirements))
                        {
                            if (_delayedOrClause != null)
                                return new ParseErrorExpression("Multiple OR clauses cannot be ANDed together", condition);
                            _delayedOrClause = condition;
                            return null;
                        }

                        BeginAlt(context);
                    }
                    error = ExecuteAchievementExpression(condition.Left, scope);
                    if (error != null)
                        return error;
                    if (!context.IsInNot)
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
            if (context.IsInNot)
                op = GetOppositeRequirementOperator(op);

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

        private static RequirementOperator GetOppositeRequirementOperator(RequirementOperator op)
        {
            switch (op)
            {
                case RequirementOperator.Equal: return RequirementOperator.NotEqual;
                case RequirementOperator.NotEqual: return RequirementOperator.Equal;
                case RequirementOperator.LessThan: return RequirementOperator.GreaterThanOrEqual;
                case RequirementOperator.LessThanOrEqual: return RequirementOperator.GreaterThan;
                case RequirementOperator.GreaterThan: return RequirementOperator.LessThanOrEqual;
                case RequirementOperator.GreaterThanOrEqual: return RequirementOperator.LessThan;
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