using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Jamiras.Components;
using Jamiras.IO.Serialization;
using RATools.Data;
using RATools.Parser.Internal;

namespace RATools.Parser
{
    public class AchievementScriptInterpreter
    {
        public AchievementScriptInterpreter()
        {
            _achievements = new List<Achievement>();
        }

        public IEnumerable<Achievement> Achievements
        {
            get { return _achievements; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<Achievement> _achievements;

        public IEnumerable<Achievement> LocalAchievements
        {
            get { return _localAchievements.Achievements; }
        }
        private LocalAchievements _localAchievements;

        public string ErrorMessage { get; private set; }

        public int GameId { get; private set; }

        public bool Run(Tokenizer input, string outputDirectory)
        {
            if (!Run(input))
                return false;

            foreach (var achievement in _achievements)
                achievement.IsDifferentThanPublished = achievement.IsDifferentThanLocal = true;

            MergePublished(outputDirectory);

            MergeLocal(outputDirectory);

            return true;
        }

        private bool Run(Tokenizer input)
        {
            var scope = new InterpreterScope();
            var tokenizer = new PositionalTokenizer(input);
            tokenizer.SkipWhitespace();

            do
            {
                if (tokenizer.Match("//"))
                {
                    var line = tokenizer.ReadTo('\n').Trim();
                    if (line.StartsWith("#ID"))
                        ExtractGameId(line);

                    tokenizer.SkipWhitespace();
                    continue;
                }

                var expression = ExpressionBase.Parse(tokenizer);
                switch (expression.Type)
                {
                    case ExpressionType.ParseError:
                        ErrorMessage = ((ParseErrorExpression)expression).Message;
                        return false;

                    case ExpressionType.FunctionDefinition:
                        scope.AddFunction((FunctionDefinitionExpression)expression);
                        break;

                    case ExpressionType.Assignment:
                        var assignment = (AssignmentExpression)expression;
                        scope.AssignVariable(assignment.Variable, assignment.Value);
                        break;

                    default:
                        if (!Evaluate(expression, scope))
                            return false;

                        break;
                }

                tokenizer.SkipWhitespace();
            } while (tokenizer.NextChar != '\0');

            return true;
        }

        private void ExtractGameId(Token line)
        {
            var tokens = line.Split('=');
            if (tokens.Length > 1)
            {
                int gameId;
                if (Int32.TryParse(tokens[1].ToString(), out gameId))
                    GameId = gameId;
            }
        }

        private bool Evaluate(ExpressionBase expression, InterpreterScope scope)
        {
            var assignmentExpression = expression as AssignmentExpression;
            if (assignmentExpression != null)
            {
                ExpressionBase result;
                if (!assignmentExpression.Value.ReplaceVariables(scope, out result))
                    return false;

                scope.AssignVariable(assignmentExpression.Variable, result);
                return true;
            }

            var functionCallExpression = expression as FunctionCallExpression;
            if (functionCallExpression != null)
                return CallFunction(functionCallExpression, scope);

            return EvaluationError(expression, "Only assignment statements, function calls and function definitions allowed at outer scope");
        }


        private bool CallFunction(FunctionCallExpression expression, InterpreterScope scope)
        {
            var function = scope.GetFunction(expression.FunctionName);
            if (function != null)
                return ExecuteFunction(function, expression, scope);

            if (expression.FunctionName == "achievement")
                return ExecuteFunctionAchievement(expression, scope);

            return EvaluationError(expression, "Unknown function: " + expression.FunctionName);
        }

        private bool ExecuteFunction(FunctionDefinitionExpression function, FunctionCallExpression functionCall, InterpreterScope scope)
        {
            scope = GetParameters(function, functionCall, scope);
            if (scope == null)
                return false;

            foreach (var expression in function.Expressions)
            {
                switch (expression.Type)
                {
                    case ExpressionType.Assignment:
                        ExpressionBase value;
                        var assignment = (AssignmentExpression)expression;
                        if (!assignment.Value.ReplaceVariables(scope, out value))
                        {
                            EvaluationError(assignment.Value, ((ParseErrorExpression)value).Message);
                            return false;
                        }

                        scope.AssignVariable(assignment.Variable, value);
                        break;

                    default:
                        if (!CallFunction((FunctionCallExpression)expression, scope))
                            return false;

                        break;
                }
            }

            return true;
        }

        private static FunctionDefinitionExpression _achievementFunction;

        private bool ExecuteFunctionAchievement(FunctionCallExpression expression, InterpreterScope scope)
        {
            if (_achievementFunction == null)
            {
                _achievementFunction = new FunctionDefinitionExpression("achievement");
                _achievementFunction.Parameters.Add("title");
                _achievementFunction.Parameters.Add("description");
                _achievementFunction.Parameters.Add("points");
                _achievementFunction.Parameters.Add("trigger");
            }

            var innerScope = GetParameters(_achievementFunction, expression, scope);
            if (innerScope == null)
                return false;

            var achievement = new AchievementBuilder();

            var stringExpression = innerScope.GetVariable("title") as StringConstantExpression;
            if (stringExpression != null)
                achievement.Title = stringExpression.Value;

            stringExpression = innerScope.GetVariable("description") as StringConstantExpression;
            if (stringExpression != null)
                achievement.Description = stringExpression.Value;

            var integerExpression = innerScope.GetVariable("points") as IntegerConstantExpression;
            if (integerExpression != null)
                achievement.Points = integerExpression.Value;

            if (!ExecuteAchievementExpression(achievement, innerScope.GetVariable("trigger"), scope))
                return false;

            achievement.Optimize();
            _achievements.Add(achievement.ToAchievement());
            return true;
        }

        private bool ExecuteAchievementExpression(AchievementBuilder achievement, ExpressionBase expression, InterpreterScope scope)
        {
            ExpressionBase operand;

            switch (expression.Type)
            {
                case ExpressionType.FunctionCall:
                    return ExecuteAchievementFunction(achievement, (FunctionCallExpression)expression, scope);

                case ExpressionType.Assignment:
                    var assignment = (AssignmentExpression)expression;
                    if (!assignment.Value.ReplaceVariables(scope, out operand))
                        return EvaluationError(assignment.Value, ((ParseErrorExpression)operand).Message);

                    scope.AssignVariable(assignment.Variable, operand);
                    return true;

                case ExpressionType.Conditional:
                    return ExecuteAchievementConditional(achievement, (ConditionalExpression)expression, scope);

                case ExpressionType.Comparison:
                    return ExecuteAchievementComparison(achievement, (ComparisonExpression)expression, scope);

                case ExpressionType.Return:
                    return ExecuteAchievementExpression(achievement, ((ReturnExpression)expression).Value, scope);

                case ExpressionType.Mathematic:
                    return ExecuteAchievementMathematic(achievement, (MathematicExpression)expression, scope);

                case ExpressionType.Variable:
                    if (!((VariableExpression)expression).ReplaceVariables(scope, out operand))
                        return EvaluationError(expression, ((ParseErrorExpression)operand).Message);

                    return ExecuteAchievementExpression(achievement, operand, scope);
            }

            return false;
        }

        private bool ExecuteAchievementMathematic(AchievementBuilder achievement, MathematicExpression mathematic, InterpreterScope scope)
        {
            if (!ExecuteAchievementExpression(achievement, mathematic.Left, scope))
                return false;

            ExpressionBase operand;
            if (!mathematic.Right.ReplaceVariables(scope, out operand))
                return false;
            var integerOperand = operand as IntegerConstantExpression;
            if (integerOperand == null)
                return EvaluationError(mathematic.Right, "expression does not evaluate to a constant");

            switch (mathematic.Operation)
            {
                case MathematicOperation.Add:
                    achievement.EqualityModifier -= integerOperand.Value;
                    return true;

                case MathematicOperation.Subtract:
                    achievement.EqualityModifier += integerOperand.Value;
                    return true;
            }

            return false;
        }

        private bool ExecuteAchievementConditional(AchievementBuilder achievement, ConditionalExpression condition, InterpreterScope scope)
        {
            switch (condition.Operation)
            {
                case ConditionalOperation.Not:
                    achievement.IsInNot = !achievement.IsInNot;
                    if (!ExecuteAchievementExpression(achievement, condition.Right, scope))
                        return false;
                    achievement.IsInNot = !achievement.IsInNot;
                    return true;

                case ConditionalOperation.And:
                    if (achievement.IsInNot)
                        achievement.BeginAlt();
                    if (!ExecuteAchievementExpression(achievement, condition.Left, scope))
                        return false;
                    if (achievement.IsInNot)
                        achievement.BeginAlt();
                    if (!ExecuteAchievementExpression(achievement, condition.Right, scope))
                        return false;
                    return true;

                case ConditionalOperation.Or:
                    if (!achievement.IsInNot)
                        achievement.BeginAlt();
                    if (!ExecuteAchievementExpression(achievement, condition.Left, scope))
                        return false;
                    if (!achievement.IsInNot)
                        achievement.BeginAlt();
                    if (!ExecuteAchievementExpression(achievement, condition.Right, scope))
                        return false;
                    return true;
            }

            return false;
        }

        private bool ExecuteAchievementComparison(AchievementBuilder achievement, ComparisonExpression comparison, InterpreterScope scope)
        {
            achievement.EqualityModifier = 0;

            ExpressionBase left;
            if (!comparison.Left.ReplaceVariables(scope, out left))
                return false;
            if (!ExecuteAchievementExpression(achievement, left, scope))
                return false;

            ExpressionBase right;
            if (!comparison.Right.ReplaceVariables(scope, out right))
                return false;

            var op = GetRequirementOperator(comparison.Operation);
            if (achievement.IsInNot)
                op = GetOppositeRequirementOperator(op);

            var integerRight = right as IntegerConstantExpression;
            if (integerRight != null)
            {
                var requirement = achievement.LastRequirement;
                requirement.Operator = op;
                requirement.Right = new Field { Size = requirement.Left.Size, Type = FieldType.Value, Value = (uint)(integerRight.Value + achievement.EqualityModifier) };
                achievement.EqualityModifier = 0;
            }
            else
            {
                achievement.EqualityModifier = -achievement.EqualityModifier;
                if (!ExecuteAchievementExpression(achievement, right, scope))
                    return false;

                if (achievement.EqualityModifier != 0)
                    return EvaluationError(right, "expansion of function calls results in non-zero modifier when comparing multiple memory addresses");

                var extraRequirement = achievement.LastRequirement;
                achievement.Current.RemoveAt(achievement.Current.Count - 1);

                var requirement = achievement.LastRequirement;
                requirement.Operator = op;
                requirement.Right = extraRequirement.Left;
            }

            return true;
        }

        private RequirementOperator GetRequirementOperator(ComparisonOperation comparisonOperation)
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

        private RequirementOperator GetOppositeRequirementOperator(RequirementOperator op)
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

        private bool ExecuteAchievementFunction(AchievementBuilder achievement, FunctionCallExpression functionCall, InterpreterScope scope)
        {
            var function = scope.GetFunction(functionCall.FunctionName);
            if (function != null)
            {
                var innerScope = GetParameters(function, functionCall, scope);
                if (innerScope == null)
                    return false;

                foreach (var expression in function.Expressions)
                {
                    if (!ExecuteAchievementExpression(achievement, expression, innerScope))
                        return false;
                }

                return true;
            }

            var fieldSize = GetMemoryLookupFunctionSize(functionCall.FunctionName);
            if (fieldSize != FieldSize.None)
            {
                ExpressionBase address;
                if (!functionCall.Parameters.First().ReplaceVariables(scope, out address))
                    return EvaluationError(functionCall.Parameters.First(), ((ParseErrorExpression)address).Message);

                var addressInteger = address as IntegerConstantExpression;
                if (addressInteger == null)
                    return EvaluationError(functionCall.Parameters.First(), "address did not resolve to a constant");

                var requirement = new Requirement { Operator = achievement.IsInNot ? RequirementOperator.Equal : RequirementOperator.NotEqual };
                requirement.Left = new Field { Size = fieldSize, Type = FieldType.MemoryAddress, Value = (uint)addressInteger.Value };
                requirement.Right = new Field { Size = fieldSize, Type = FieldType.Value, Value = 0 };
                achievement.Current.Add(requirement);
                return true;
            }

            if (functionCall.FunctionName == "once")
            {
                if (!ExecuteAchievementExpression(achievement, functionCall.Parameters.First(), scope))
                    return false;

                var requirement = achievement.LastRequirement;
                requirement.HitCount = 1;
                return true;
            }

            if (functionCall.FunctionName == "never")
            {
                if (!ExecuteAchievementExpression(achievement, functionCall.Parameters.First(), scope))
                    return false;

                var requirement = achievement.LastRequirement;
                requirement.Type = RequirementType.ResetIf;
                return true;
            }

            return EvaluationError(functionCall, "unsupported function within achievement");
        }

        private FieldSize GetMemoryLookupFunctionSize(string name)
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

        private InterpreterScope GetParameters(FunctionDefinitionExpression function, FunctionCallExpression functionCall, InterpreterScope scope)
        {
            var innerScope = new InterpreterScope(scope);

            int index = 0;
            bool namedParameters = false;
            foreach (var parameter in functionCall.Parameters)
            {
                var assignedParameter = parameter as AssignmentExpression;
                if (assignedParameter != null)
                {
                    if (!function.Parameters.Contains(assignedParameter.Variable))
                    {
                        EvaluationError(parameter, String.Format("{0} does not have a {1} parameter", function.Name, assignedParameter.Variable));
                        return null;
                    }

                    ExpressionBase value;
                    if (!assignedParameter.Value.ReplaceVariables(scope, out value))
                    {
                        EvaluationError(assignedParameter.Value, ((ParseErrorExpression)value).Message);
                        return null;
                    }

                    innerScope.AssignVariable(assignedParameter.Variable, value);
                    namedParameters = true;
                }
                else
                {
                    if (namedParameters)
                    {
                        EvaluationError(parameter, "non-named parameter following named parameter");
                        return null;
                    }

                    if (index == function.Parameters.Count)
                    {
                        EvaluationError(parameter, "too many parameters passed to function");
                        return null;
                    }

                    ExpressionBase value;
                    if (!parameter.ReplaceVariables(scope, out value))
                    {
                        EvaluationError(parameter, ((ParseErrorExpression)value).Message);
                        return null;
                    }

                    innerScope.AssignVariable(function.Parameters.ElementAt(index), value);
                }

                ++index;
            }

            return innerScope;
        }

        private bool EvaluationError(ExpressionBase expression, string message)
        {
            ErrorMessage = String.Format("{0}:{1} {2}", expression.Line, expression.Column, message);
            return false;
        }

        internal static string ParseError(PositionalTokenizer tokenizer, string message)
        {
            return String.Format("{0}:{1} {2}", tokenizer.Line, tokenizer.Column, message);
        }

        private void MergePublished(string outputDirectory)
        {
            var fileName = Path.Combine(outputDirectory, GameId + ".txt");
            if (!File.Exists(fileName))
                return;

            using (var stream = File.OpenRead(fileName))
            {
                var publishedData = new JsonObject(stream);
                var publishedAchievements = publishedData.GetField("Achievements");
                foreach (var publishedAchievement in publishedAchievements.ObjectArrayValue)
                {
                    var title = publishedAchievement.GetField("Title").StringValue;
                    var achievement = _achievements.FirstOrDefault(a => a.Title == title);
                    if (achievement == null)
                        continue;

                    achievement.Id = publishedAchievement.GetField("ID").IntegerValue.GetValueOrDefault();
                    achievement.BadgeName = publishedAchievement.GetField("BadgeName").StringValue;

                    if (achievement.Points != publishedAchievement.GetField("Points").IntegerValue.GetValueOrDefault())
                    {
                        achievement.IsDifferentThanPublished = true;
                    }
                    else if (achievement.Description != publishedAchievement.GetField("Description").StringValue)
                    {
                        achievement.IsDifferentThanPublished = true;
                    }
                    else
                    {
                        var requirementsString = publishedAchievement.GetField("MemAddr").StringValue;
                        var cheev = new AchievementBuilder();
                        cheev.ParseRequirements(Tokenizer.CreateTokenizer(requirementsString));

                        achievement.IsDifferentThanPublished = cheev.ToAchievement().AreRequirementsSame(achievement);
                    }
                }
            }
        }

        private void MergeLocal(string outputDirectory)
        {
            var fileName = Path.Combine(outputDirectory, GameId + "-User.txt");
            _localAchievements = new LocalAchievements(fileName);

            foreach (var achievement in _achievements)
            {
                var localAchievement = _localAchievements.Achievements.FirstOrDefault(a => a.Title == achievement.Title);
                if (localAchievement == null)
                    continue;

                if (achievement.Points != localAchievement.Points)
                    achievement.IsDifferentThanLocal = true;
                else if (achievement.Description != localAchievement.Description)
                    achievement.IsDifferentThanLocal = true;
                else
                    achievement.IsDifferentThanLocal = achievement.AreRequirementsSame(localAchievement);
            }
        }
    }
}
