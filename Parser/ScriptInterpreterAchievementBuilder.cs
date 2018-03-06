using RATools.Data;
using RATools.Parser.Internal;
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

        /// <summary>
        /// Populates the <see cref="AchievementBuilder"/> from an expression.
        /// </summary>
        /// <param name="expression">The expression to populate from.</param>
        /// <returns><c>null</c> if successful, otherwise an error message indicating why it failed.</returns>
        public string PopulateFromExpression(ExpressionBase expression)
        {
            ParseErrorExpression error;
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            if (!PopulateFromExpression(expression, scope, out error))
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
            return (error == null);
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
            var error = ExecuteAchievementExpression(mathematic.Left, scope);
            if (error != null)
                return error;

            ExpressionBase operand;
            if (!mathematic.Right.ReplaceVariables(scope, out operand))
                return (ParseErrorExpression)operand;

            var context = scope.GetContext<TriggerBuilderContext>();

            var integerOperand = operand as IntegerConstantExpression;
            if (integerOperand == null)
            {
                if (operand is FunctionCallExpression || operand is MathematicExpression)
                {
                    switch (mathematic.Operation)
                    {
                        case MathematicOperation.Add:
                            context.LastRequirement.Type = RequirementType.AddSource;
                            break;
                        case MathematicOperation.Subtract:
                            context.LastRequirement.Type = RequirementType.SubSource;
                            break;
                        default:
                            return new ParseErrorExpression("Expression cannot be converted to an achievement", mathematic);
                    }

                    context.LastRequirement.Operator = RequirementOperator.None;
                    context.LastRequirement.Right = new Field();
                    return ExecuteAchievementExpression(operand, scope);
                }

                return new ParseErrorExpression("Expression does not evaluate to a constant", mathematic.Right);
            }

            var oppositeOperation = MathematicExpression.GetOppositeOperation(mathematic.Operation);
            if (oppositeOperation == MathematicOperation.None)
                return new ParseErrorExpression("Cannot transpose modification to result", mathematic);

            _equalityModifiers.Push(new ValueModifier(oppositeOperation, integerOperand.Value));
            return null;
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
                        BeginAlt(context);
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

            ExpressionBase left;
            if (!comparison.Left.ReplaceVariables(scope, out left))
                return (ParseErrorExpression)left;
            var error = ExecuteAchievementExpression(left, scope);
            if (error != null)
                return error;

            ExpressionBase right;
            if (!comparison.Right.ReplaceVariables(scope, out right))
                return (ParseErrorExpression)right;

            var context = scope.GetContext<TriggerBuilderContext>();
            var op = GetRequirementOperator(comparison.Operation);
            if (context.IsInNot)
                op = GetOppositeRequirementOperator(op);

            var integerRight = right as IntegerConstantExpression;
            if (integerRight != null)
            {
                int newValue = integerRight.Value;
                while (_equalityModifiers.Count > 0)
                {
                    var modifier = _equalityModifiers.Pop();
                    newValue = modifier.Apply(newValue);
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

                    if (leftValue != rightValue)
                        return new ParseErrorExpression("Expansion of function calls results in non-zero modifier when comparing multiple memory addresses", right);
                }

                var extraRequirement = context.LastRequirement;
                context.Trigger.Remove(extraRequirement);

                var requirement = context.LastRequirement;
                requirement.Operator = op;
                requirement.Right = extraRequirement.Left;
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

        private ParseErrorExpression ExecuteAchievementFunction(FunctionCallExpression functionCall, InterpreterScope scope)
        {
            // call the function
            ExpressionBase result;
            if (!functionCall.Evaluate(scope, out result, false))
                return (ParseErrorExpression)result;

            // void function won't have a return value. also, some built-in functions modify the context without returning a value.
            if (result == null)               
                return null;

            // process the return value
            var innerScope = new InterpreterScope(scope);
            if (innerScope.Depth == 100)
                return new ParseErrorExpression("Maximum recursion depth exceeded", functionCall);

            ParseErrorExpression error = ExecuteAchievementExpression(result, innerScope);
            if (error == null)
                return null;

            // prevent recursive error stacking
            var message = "Function call did not resolve to a valid trigger condition";
            if (error.Message == message)
                return error;

            return new ParseErrorExpression(message, functionCall) { InnerError = error };
        }
    }
}