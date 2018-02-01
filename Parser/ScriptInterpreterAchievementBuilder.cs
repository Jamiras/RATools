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
            _current = CoreRequirements;
            _equalityModifiers = new Stack<ValueModifier>();
        }

        private ICollection<Requirement> _current;
        private bool _isInNot;
        private Stack<ValueModifier> _equalityModifiers;

        /// <summary>
        /// Begins an new alt group.
        /// </summary>
        /// <returns>The requirement collection for the new alt group.</returns>
        public void BeginAlt()
        {
            if (ReferenceEquals(_current, CoreRequirements) || _current.Count > 0)
            {
                var newAlt = new List<Requirement>();
                _current = newAlt;
                AlternateRequirements.Add(newAlt);
            }
        }

        /// <summary>
        /// Gets the last requirement added to the achievement.
        /// </summary>
        public Requirement LastRequirement
        {
            get { return _current.Last(); }
        }

        /// <summary>
        /// Populates the <see cref="AchievementBuilder"/> from an expression.
        /// </summary>
        /// <param name="expression">The expression to populate from.</param>
        /// <returns><c>null</c> if successful, otherwise an error message indicating why it failed.</returns>
        public string PopulateFromExpression(ExpressionBase expression)
        {
            ParseErrorExpression error;
            var scope = new InterpreterScope();
            if (!PopulateFromExpression(expression, scope, out error))
                return error.Message;

            return null;
        }

        internal bool PopulateFromExpression(ExpressionBase expression, InterpreterScope scope, out ParseErrorExpression error)
        {
            switch (expression.Type)
            {
                case ExpressionType.FunctionCall:
                    error = ExecuteAchievementFunction((FunctionCallExpression)expression, scope);
                    break;

                case ExpressionType.Conditional:
                    error = ExecuteAchievementConditional((ConditionalExpression)expression, scope);
                    break;

                case ExpressionType.Comparison:
                    error = ExecuteAchievementComparison((ComparisonExpression)expression, scope);
                    break;

                case ExpressionType.Mathematic:
                    error = ExecuteAchievementMathematic((MathematicExpression)expression, scope);
                    break;

                case ExpressionType.Variable:
                    ExpressionBase operand;
                    if (!((VariableExpression)expression).ReplaceVariables(scope, out operand))
                    {
                        error = (ParseErrorExpression)operand;
                        return false;
                    }

                    error = ExecuteAchievementExpression(operand, scope);
                    break;

                default:
                    error = new ParseErrorExpression("Unsupported trigger: " + expression.Type, expression);
                    break;
            }

            return (error == null);
        }

        private ParseErrorExpression ExecuteAchievementExpression(ExpressionBase expression, InterpreterScope scope)
        {
            ParseErrorExpression error;
            ExpressionBase operand;

            switch (expression.Type)
            {
                case ExpressionType.FunctionCall:
                    return ExecuteAchievementFunction((FunctionCallExpression)expression, scope);

                case ExpressionType.Assignment:
                    var assignment = (AssignmentExpression)expression;
                    if (!assignment.Value.ReplaceVariables(scope, out operand))
                        return new ParseErrorExpression(operand, assignment.Value);

                    scope.AssignVariable(assignment.Variable, operand);
                    return null;

                case ExpressionType.Conditional:
                    return ExecuteAchievementConditional((ConditionalExpression)expression, scope);

                case ExpressionType.Comparison:
                    return ExecuteAchievementComparison((ComparisonExpression)expression, scope);

                case ExpressionType.Return:
                    error = ExecuteAchievementExpression(((ReturnExpression)expression).Value, scope);
                    if (error != null)
                        return error;

                    scope.IsComplete = true;
                    return null;

                case ExpressionType.Mathematic:
                    return ExecuteAchievementMathematic((MathematicExpression)expression, scope);

                case ExpressionType.Variable:
                    if (!((VariableExpression)expression).ReplaceVariables(scope, out operand))
                        return new ParseErrorExpression(operand, expression);

                    return ExecuteAchievementExpression(operand, scope);

                case ExpressionType.If:
                    return ExecuteAchievementIf((IfExpression)expression, scope);
            }

            return new ParseErrorExpression("Unupported expression in achievement: " + expression.Type, expression);
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

        private ParseErrorExpression ExecuteAchievementIf(IfExpression ifExpression, InterpreterScope scope)
        {
            ParseErrorExpression error;
            bool result = ifExpression.Condition.IsTrue(scope, out error);
            if (error != null)
                return error;

            if (result)
                return ExecuteAchievementExpressions(ifExpression.Expressions, scope);
            else
                return ExecuteAchievementExpressions(ifExpression.ElseExpressions, scope);
        }

        private ParseErrorExpression ExecuteAchievementMathematic(MathematicExpression mathematic, InterpreterScope scope)
        {
            var error = ExecuteAchievementExpression(mathematic.Left, scope);
            if (error != null)
                return error;

            ExpressionBase operand;
            if (!mathematic.Right.ReplaceVariables(scope, out operand))
                return (ParseErrorExpression)operand;

            var integerOperand = operand as IntegerConstantExpression;
            if (integerOperand == null)
            {
                if (operand is FunctionCallExpression || operand is MathematicExpression)
                {
                    switch (mathematic.Operation)
                    {
                        case MathematicOperation.Add:
                            LastRequirement.Type = RequirementType.AddSource;
                            break;
                        case MathematicOperation.Subtract:
                            LastRequirement.Type = RequirementType.SubSource;
                            break;
                        default:
                            return new ParseErrorExpression("expression cannot be converted to an achievement", mathematic);
                    }

                    LastRequirement.Operator = RequirementOperator.None;
                    LastRequirement.Right = new Field();
                    return ExecuteAchievementExpression(operand, scope);
                }

                return new ParseErrorExpression("expression does not evaluate to a constant", mathematic.Right);
            }

            var oppositeOperation = MathematicExpression.GetOppositeOperation(mathematic.Operation);
            if (oppositeOperation == MathematicOperation.None)
                return new ParseErrorExpression("cannot transpose modification to result", mathematic);

            _equalityModifiers.Push(new ValueModifier(oppositeOperation, integerOperand.Value));
            return null;
        }

        private ParseErrorExpression ExecuteAchievementConditional(ConditionalExpression condition, InterpreterScope scope)
        {
            ParseErrorExpression error;

            switch (condition.Operation)
            {
                case ConditionalOperation.Not:
                    _isInNot = !_isInNot;
                    error = ExecuteAchievementExpression(condition.Right, scope);
                    if (error != null)
                        return error;
                    _isInNot = !_isInNot;
                    return null;

                case ConditionalOperation.And:
                    if (_isInNot)
                        BeginAlt();
                    error = ExecuteAchievementExpression(condition.Left, scope);
                    if (error != null)
                        return error;
                    if (_isInNot)
                        BeginAlt();
                    error = ExecuteAchievementExpression(condition.Right, scope);
                    if (error != null)
                        return error;
                    return null;

                case ConditionalOperation.Or:
                    if (!_isInNot)
                        BeginAlt();
                    error = ExecuteAchievementExpression(condition.Left, scope);
                    if (error != null)
                        return error;
                    if (!_isInNot)
                        BeginAlt();
                    error = ExecuteAchievementExpression(condition.Right, scope);
                    if (error != null)
                        return error;
                    return null;
            }

            return new ParseErrorExpression("unsupported conditional", condition);
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

            var op = GetRequirementOperator(comparison.Operation);
            if (_isInNot)
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

                var requirement = LastRequirement;
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
                        return new ParseErrorExpression("expansion of function calls results in non-zero modifier when comparing multiple memory addresses", right);
                }

                var extraRequirement = LastRequirement;
                _current.Remove(extraRequirement);

                var requirement = LastRequirement;
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
            var function = scope.GetFunction(functionCall.FunctionName.Name);
            if (function != null)
            {
                ExpressionBase error;
                var innerScope = functionCall.GetParameters(function, scope, out error);
                if (innerScope == null)
                    return (ParseErrorExpression)error;

                return ExecuteAchievementExpressions(function.Expressions, innerScope);
            }

            var fieldSize = GetMemoryLookupFunctionSize(functionCall.FunctionName.Name);
            if (fieldSize != FieldSize.None)
            {
                ExpressionBase address;
                if (!functionCall.Parameters.First().ReplaceVariables(scope, out address))
                    return (ParseErrorExpression)address;

                var addressInteger = address as IntegerConstantExpression;
                if (addressInteger == null)
                    return new ParseErrorExpression("address did not resolve to a constant", functionCall.Parameters.First());

                var requirement = new Requirement { Operator = _isInNot ? RequirementOperator.Equal : RequirementOperator.NotEqual };
                requirement.Left = new Field { Size = fieldSize, Type = FieldType.MemoryAddress, Value = (uint)addressInteger.Value };
                requirement.Right = new Field { Size = fieldSize, Type = FieldType.Value, Value = 0 };
                _current.Add(requirement);
                return null;
            }

            if (functionCall.FunctionName.Name == "once")
            {
                var error = ExecuteAchievementExpression(functionCall.Parameters.First(), scope);
                if (error != null)
                    return error;

                var requirement = LastRequirement;
                requirement.HitCount = 1;
                return null;
            }

            if (functionCall.FunctionName.Name == "repeated")
            {
                var error = ExecuteAchievementExpression(functionCall.Parameters.ElementAt(1), scope);
                if (error != null)
                    return error;

                ExpressionBase times;
                if (!functionCall.Parameters.First().ReplaceVariables(scope, out times))
                    return (ParseErrorExpression)times;

                if (times.Type != ExpressionType.IntegerConstant)
                    return new ParseErrorExpression("expression does not evaluate to an integer", functionCall.Parameters.First());

                var requirement = LastRequirement;
                requirement.HitCount = (ushort)((IntegerConstantExpression)times).Value;
                return null;
            }

            if (functionCall.FunctionName.Name == "never")
            {
                var temp = new ScriptInterpreterAchievementBuilder();
                var error = temp.ExecuteAchievementExpression(functionCall.Parameters.First(), scope);
                if (error != null)
                    return error;

                var temp2 = temp.ToAchievement();
                if (temp2.AlternateRequirements.Any())
                {
                    if (temp2.CoreRequirements.Any())
                        return new ParseErrorExpression("never does not support &&'d conditions", functionCall.Parameters.First());
                    foreach (var alt in temp2.AlternateRequirements)
                    {
                        if (alt.Count() > 1)
                            return new ParseErrorExpression("never does not support &&'d conditions", functionCall.Parameters.First());

                        var requirement = alt.First();
                        requirement.Type = RequirementType.ResetIf;
                        _current.Add(requirement);
                    }
                }
                else
                {
                    if (temp2.CoreRequirements.Count() > 1)
                        return new ParseErrorExpression("never does not support &&'d conditions", functionCall.Parameters.First());

                    var requirement = temp2.CoreRequirements.First();
                    requirement.Type = RequirementType.ResetIf;
                    _current.Add(requirement);
                }

                return null;
            }

            if (functionCall.FunctionName.Name == "unless")
            {
                var temp = new ScriptInterpreterAchievementBuilder();
                var error = temp.ExecuteAchievementExpression(functionCall.Parameters.First(), scope);
                if (error != null)
                    return error;

                var temp2 = temp.ToAchievement();
                if (temp2.AlternateRequirements.Any())
                {
                    if (temp2.CoreRequirements.Any())
                        return new ParseErrorExpression("unless does not support &&'d conditions", functionCall.Parameters.First());
                    foreach (var alt in temp2.AlternateRequirements)
                    {
                        if (alt.Count() > 1)
                            return new ParseErrorExpression("unless does not support &&'d conditions", functionCall.Parameters.First());

                        var requirement = alt.First();
                        requirement.Type = RequirementType.PauseIf;
                        _current.Add(requirement);
                    }
                }
                else
                {
                    if (temp2.CoreRequirements.Count() > 1)
                        return new ParseErrorExpression("unless does not support &&'d conditions", functionCall.Parameters.First());

                    var requirement = temp2.CoreRequirements.First();
                    requirement.Type = RequirementType.PauseIf;
                    _current.Add(requirement);
                }

                return null;
            }

            if (functionCall.FunctionName.Name == "prev")
            {
                var error = ExecuteAchievementExpression(functionCall.Parameters.First(), scope);
                if (error != null)
                    return error;

                var requirement = LastRequirement;
                requirement.Left = new Field { Size = requirement.Left.Size, Type = FieldType.PreviousValue, Value = requirement.Left.Value };
                return null;
            }

            return new ParseErrorExpression("unsupported function within achievement: " + functionCall.FunctionName, functionCall);
        }

        internal static FieldSize GetMemoryLookupFunctionSize(string name)
        {
            switch (name)
            {
                case "bit0": return FieldSize.Bit0;
                case "bit1": return FieldSize.Bit1;
                case "bit2": return FieldSize.Bit2;
                case "bit3": return FieldSize.Bit3;
                case "bit4": return FieldSize.Bit4;
                case "bit5": return FieldSize.Bit5;
                case "bit6": return FieldSize.Bit6;
                case "bit7": return FieldSize.Bit7;
                case "low4": return FieldSize.LowNibble;
                case "high4": return FieldSize.HighNibble;
                case "byte": return FieldSize.Byte;
                case "word": return FieldSize.Word;
                case "dword": return FieldSize.DWord;
                default: return FieldSize.None;
            }
        }

    }
}