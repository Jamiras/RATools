﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Internal;

namespace RATools.Parser
{
    public class AchievementScriptInterpreter
    {
        public AchievementScriptInterpreter()
        {
            _achievements = new List<Achievement>();
            _leaderboards = new List<Leaderboard>();
        }

        public IEnumerable<Achievement> Achievements
        {
            get { return _achievements; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<Achievement> _achievements;

        public string ErrorMessage { get; private set; }

        public int GameId { get; private set; }
        public string GameTitle { get; private set; }

        public string RichPresence { get; private set; }

        public IEnumerable<Leaderboard> Leaderboards
        {
            get { return _leaderboards; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<Leaderboard> _leaderboards;

        public bool Run(Tokenizer input)
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
                        return EvaluationError(expression, expression);

                    case ExpressionType.FunctionDefinition:
                        scope.AddFunction((FunctionDefinitionExpression)expression);
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

        private bool Evaluate(IEnumerable<ExpressionBase> expressions, InterpreterScope scope)
        {
            foreach (var expression in expressions)
            {
                if (!Evaluate(expression, scope))
                    return false;

                if (scope.IsComplete)
                    break;
            }

            return true;
        }

        private bool Evaluate(ExpressionBase expression, InterpreterScope scope)
        {
            switch (expression.Type)
            {
                case ExpressionType.Assignment:
                    var assignment = (AssignmentExpression)expression;
                    ExpressionBase result;
                    if (!assignment.Value.ReplaceVariables(scope, out result))
                        return EvaluationError(assignment.Value, result);

                    scope.AssignVariable(assignment.Variable, result);
                    return true;

                case ExpressionType.FunctionCall:
                    return CallFunction((FunctionCallExpression)expression, scope);

                case ExpressionType.For:
                    return EvaluateLoop((ForExpression)expression, scope);

                case ExpressionType.If:
                    return EvaluateIf((IfExpression)expression, scope);

                default:
                    return EvaluationError(expression, "Only assignment statements, function calls and function definitions allowed at outer scope");
            }
        }

        private bool EvaluateLoop(ForExpression forExpression, InterpreterScope scope)
        {
            ExpressionBase range;
            if (!forExpression.Range.ReplaceVariables(scope, out range))
                return EvaluationError(forExpression.Range, range);

            var dict = range as DictionaryExpression;
            if (dict != null)
            {
                var iterator = new VariableExpression(forExpression.IteratorName);
                foreach (var entry in dict.Entries)
                {
                    var loopScope = new InterpreterScope(scope);

                    ExpressionBase key;
                    if (!entry.Key.ReplaceVariables(scope, out key))
                        return EvaluationError(entry.Key, key);
                    
                    scope.AssignVariable(iterator, key);

                    if (!Evaluate(forExpression.Expressions, loopScope))
                        return false;

                    if (loopScope.IsComplete)
                        break;
                }

                return true;
            }

            return EvaluationError(forExpression.Range, "Cannot iterate over " + forExpression.Range.ToString());
        }

        private bool EvaluateIf(IfExpression ifExpression, InterpreterScope scope)
        {
            ParseErrorExpression error;
            bool result = ifExpression.Condition.IsTrue(scope, out error);
            if (error != null)
                return EvaluationError(ifExpression.Condition, error.Message);

            if (result)
                Evaluate(ifExpression.Expressions, scope);
            else
                Evaluate(ifExpression.ElseExpressions, scope);

            return true;
        }

        private bool CallFunction(FunctionCallExpression expression, InterpreterScope scope)
        {
            var function = scope.GetFunction(expression.FunctionName);
            if (function != null)
            {
                scope = GetParameters(function, expression, scope);
                if (scope == null)
                    return false;

                return Evaluate(function.Expressions, scope);
            }

            if (expression.FunctionName == "achievement")
                return ExecuteFunctionAchievement(expression, scope);

            if (expression.FunctionName == "rich_presence_display")
                return ExecuteRichPresenceDisplay(expression, scope);

            if (expression.FunctionName == "leaderboard")
                return ExecuteLeaderboard(expression, scope);

            return EvaluationError(expression, "Unknown function: " + expression.FunctionName);
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

                // ignored parameters generated by dumper
                _achievementFunction.Parameters.Add("id");
                _achievementFunction.Parameters.Add("published");
                _achievementFunction.Parameters.Add("modified");
            }

            var innerScope = GetParameters(_achievementFunction, expression, scope);
            if (innerScope == null)
                return false;

            var achievement = new AchievementBuilder();

            var stringExpression = innerScope.GetVariable("title") as StringConstantExpression;
            achievement.Title = (stringExpression != null) ? stringExpression.Value : String.Empty;

            stringExpression = innerScope.GetVariable("description") as StringConstantExpression;
            achievement.Description = (stringExpression != null) ? stringExpression.Value : String.Empty;

            var integerExpression = innerScope.GetVariable("points") as IntegerConstantExpression;
            if (integerExpression != null)
                achievement.Points = integerExpression.Value;

            integerExpression = innerScope.GetVariable("id") as IntegerConstantExpression;
            if (integerExpression != null)
                achievement.Id = integerExpression.Value;

            if (!ExecuteAchievementExpression(achievement, innerScope.GetVariable("trigger"), scope))
                return false;

            var message = achievement.Optimize();
            if (message != null)
                return EvaluationError(expression, message);

            _achievements.Add(achievement.ToAchievement());
            return true;
        }

        private bool ExecuteAchievementExpressions(AchievementBuilder achievement, ICollection<ExpressionBase> expressions, InterpreterScope scope)
        {
            foreach (var expression in expressions)
            {
                if (!ExecuteAchievementExpression(achievement, expression, scope))
                    return false;

                if (scope.IsComplete)
                    break;
            }

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
                        return EvaluationError(assignment.Value, operand);

                    scope.AssignVariable(assignment.Variable, operand);
                    return true;

                case ExpressionType.Conditional:
                    return ExecuteAchievementConditional(achievement, (ConditionalExpression)expression, scope);

                case ExpressionType.Comparison:
                    return ExecuteAchievementComparison(achievement, (ComparisonExpression)expression, scope);

                case ExpressionType.Return:
                    if (!ExecuteAchievementExpression(achievement, ((ReturnExpression)expression).Value, scope))
                        return false;

                    scope.IsComplete = true;
                    return true;

                case ExpressionType.Mathematic:
                    return ExecuteAchievementMathematic(achievement, (MathematicExpression)expression, scope);

                case ExpressionType.Variable:
                    if (!((VariableExpression)expression).ReplaceVariables(scope, out operand))
                        return EvaluationError(expression, operand);

                    return ExecuteAchievementExpression(achievement, operand, scope);

                case ExpressionType.If:
                    return ExecuteAchievementIf(achievement, (IfExpression)expression, scope);
            }

            return EvaluationError(expression, "Unupported expression in achievement: " + expression.Type);
        }

        private bool ExecuteAchievementIf(AchievementBuilder achievement, IfExpression ifExpression, InterpreterScope scope)
        {
            ParseErrorExpression error;
            bool result = ifExpression.Condition.IsTrue(scope, out error);
            if (error != null)
                return EvaluationError(ifExpression.Condition, error);

            if (result)
                return ExecuteAchievementExpressions(achievement, ifExpression.Expressions, scope);
            else
                return ExecuteAchievementExpressions(achievement, ifExpression.ElseExpressions, scope);
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
                return EvaluationError(comparison.Left, left);
            if (!ExecuteAchievementExpression(achievement, left, scope))
                return false;

            ExpressionBase right;
            if (!comparison.Right.ReplaceVariables(scope, out right))
                return EvaluationError(comparison.Right, right);

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

                return ExecuteAchievementExpressions(achievement, function.Expressions, innerScope);
            }

            var fieldSize = GetMemoryLookupFunctionSize(functionCall.FunctionName);
            if (fieldSize != FieldSize.None)
            {
                ExpressionBase address;
                if (!functionCall.Parameters.First().ReplaceVariables(scope, out address))
                    return EvaluationError(functionCall.Parameters.First(), address);

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

            if (functionCall.FunctionName == "repeated")
            {
                if (!ExecuteAchievementExpression(achievement, functionCall.Parameters.ElementAt(1), scope))
                    return false;

                ExpressionBase times;
                if (!functionCall.Parameters.First().ReplaceVariables(scope, out times))
                    return EvaluationError(functionCall.Parameters.First(), times);

                if (times.Type != ExpressionType.IntegerConstant)
                    return EvaluationError(functionCall.Parameters.First(), "expression does not evaluate to an integer");

                var requirement = achievement.LastRequirement;
                requirement.HitCount = (ushort)((IntegerConstantExpression)times).Value;
                return true;
            }

            if (functionCall.FunctionName == "never")
            {
                var temp = new AchievementBuilder();
                if (!ExecuteAchievementExpression(temp, functionCall.Parameters.First(), scope))
                    return false;

                var temp2 = temp.ToAchievement();
                if (temp2.AlternateRequirements.Any())
                {
                    if (temp2.CoreRequirements.Any())
                        return EvaluationError(functionCall.Parameters.First(), "never does not support &&'d conditions");
                    foreach (var alt in temp2.AlternateRequirements)
                    {
                        if (alt.Count() > 1)
                            return EvaluationError(functionCall.Parameters.First(), "never does not support &&'d conditions");
                        
                        var requirement = alt.First();
                        requirement.Type = RequirementType.ResetIf;
                        achievement.Current.Add(requirement);
                    }
                }
                else
                {
                    if (temp2.CoreRequirements.Count() > 1)
                        return EvaluationError(functionCall.Parameters.First(), "never does not support &&'d conditions");

                    var requirement = temp2.CoreRequirements.First();
                    requirement.Type = RequirementType.ResetIf;
                    achievement.Current.Add(requirement);
                }

                return true;
            }

            if (functionCall.FunctionName == "unless")
            {
                var temp = new AchievementBuilder();
                if (!ExecuteAchievementExpression(temp, functionCall.Parameters.First(), scope))
                    return false;

                var temp2 = temp.ToAchievement();
                if (temp2.AlternateRequirements.Any())
                {
                    if (temp2.CoreRequirements.Any())
                        return EvaluationError(functionCall.Parameters.First(), "unless does not support &&'d conditions");
                    foreach (var alt in temp2.AlternateRequirements)
                    {
                        if (alt.Count() > 1)
                            return EvaluationError(functionCall.Parameters.First(), "unless does not support &&'d conditions");

                        var requirement = alt.First();
                        requirement.Type = RequirementType.PauseIf;
                        achievement.Current.Add(requirement);
                    }
                }
                else
                {
                    if (temp2.CoreRequirements.Count() > 1)
                        return EvaluationError(functionCall.Parameters.First(), "unless does not support &&'d conditions");

                    var requirement = temp2.CoreRequirements.First();
                    requirement.Type = RequirementType.PauseIf;
                    achievement.Current.Add(requirement);
                }

                return true;
            }

            if (functionCall.FunctionName == "prev")
            {
                if (!ExecuteAchievementExpression(achievement, functionCall.Parameters.First(), scope))
                    return false;

                var requirement = achievement.LastRequirement;
                requirement.Left = new Field { Size = requirement.Left.Size, Type = FieldType.PreviousValue, Value = requirement.Left.Value };
                return true;
            }

            return EvaluationError(functionCall, "unsupported function within achievement: " + functionCall.FunctionName);
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

        private bool ExecuteRichPresenceDisplay(FunctionCallExpression expression, InterpreterScope scope)
        {
            var displayString = expression.Parameters.ElementAt(0) as StringConstantExpression;
            if (displayString == null)
                return EvaluationError(expression.Parameters.ElementAt(0), "First parameter to rich_presence_display must be a string");

            var valueFields = new List<string>();
            var lookupFields = new TinyDictionary<string, DictionaryExpression>();

            var builder = new StringBuilder();
            var tokenizer = Tokenizer.CreateTokenizer(displayString.Value);
            while (tokenizer.NextChar != '\0')
            {
                var token = tokenizer.ReadTo('{');
                builder.Append(token.ToString());

                if (tokenizer.NextChar == '\0')
                    break;

                tokenizer.Advance();
                var index = tokenizer.ReadNumber();
                if (tokenizer.NextChar != '}')
                    return EvaluationError(displayString, "malformed index");
                tokenizer.Advance();

                var parameterIndex = Int32.Parse(index.ToString()) + 1;
                if (parameterIndex >= expression.Parameters.Count)
                    return EvaluationError(expression.Parameters.ElementAt(0), "invalid parameter index: " + (parameterIndex - 1));
                var parameter = expression.Parameters.ElementAt(parameterIndex) as FunctionCallExpression;
                if (parameter == null)
                    return EvaluationError(expression.Parameters.ElementAt(parameterIndex), "parameter must be a rich_presence_ function");
                if (parameter.Parameters.Count() < 2)
                    return EvaluationError(parameter, "parameter must be a rich_presence_ function");

                var variableName = ((StringConstantExpression)parameter.Parameters.ElementAt(0)).Value;

                ExpressionBase addressExpression;
                if (!parameter.Parameters.ElementAt(1).ReplaceVariables(scope, out addressExpression))
                    return EvaluationError(parameter.Parameters.ElementAt(1), addressExpression);

                string address;
                if (!EvaluateAddress(addressExpression, scope, out address))
                    return false;
 
                if (parameter.FunctionName == "rich_presence_lookup")
                {
                    ExpressionBase value;
                    if (!parameter.Parameters.ElementAt(2).ReplaceVariables(scope, out value))
                        return EvaluationError(parameter.Parameters.ElementAt(2), value);

                    var dict = value as DictionaryExpression;
                    if (dict == null)
                        return EvaluationError(parameter.Parameters.ElementAt(2), "parameter does not evaluate to a dictionary");

                    lookupFields[variableName] = dict;
                }
                else if (parameter.FunctionName == "rich_presence_value")
                {
                    valueFields.Add(variableName);
                }
                else
                {
                    return EvaluationError(expression.Parameters.ElementAt(parameterIndex), "parameter must be a rich_presence_ function");
                }

                builder.Append('@');
                builder.Append(variableName);
                builder.Append('(');
                builder.Append(address);
                builder.Append(')');
            }

            var display = builder.ToString();

            builder.Length = 0;
            foreach (var lookup in lookupFields)
            {
                builder.Append("Lookup:");
                builder.AppendLine(lookup.Key);

                var list = new List<DictionaryExpression.DictionaryEntry>(lookup.Value.Entries);
                list.Sort((l,r) => ((IntegerConstantExpression)l.Key).Value - ((IntegerConstantExpression)r.Key).Value);

                foreach (var entry in list)
                {
                    builder.Append(((IntegerConstantExpression)entry.Key).Value);
                    builder.Append('=');
                    builder.AppendLine(((StringConstantExpression)entry.Value).Value);
                }

                builder.AppendLine();
            }

            foreach (var value in valueFields)
            {
                builder.Append("Format:");
                builder.AppendLine(value);
                builder.AppendLine("FormatType=VALUE");
                builder.AppendLine();
            }

            builder.AppendLine("Display:");
            builder.AppendLine(display);

            RichPresence = builder.ToString();

            return true;
        }

        private bool EvaluateAddress(ExpressionBase expression, InterpreterScope scope, out string address)
        {
            var builder = new StringBuilder();

            Field addressField;
            if (EvaluateAddress(expression, scope, out addressField))
            {
                addressField.Serialize(builder);
                address = builder.ToString();
                return true;
            }

            var mathematic = expression as MathematicExpression;
            if (mathematic != null)
            {
                string left, right;
                if (!EvaluateAddress(mathematic.Left, scope, out left))
                {
                    address = String.Empty;
                    return false; // nested call will have already set error message
                }

                builder.Append(left);

                var integer = mathematic.Right as IntegerConstantExpression;
                switch (mathematic.Operation)
                {
                    case MathematicOperation.Add:
                        builder.Append('_');

                        if (integer != null)
                        {
                            builder.Append('v');
                            builder.Append(integer.Value);
                            address = builder.ToString();
                            return true;
                        }

                        if (!EvaluateAddress(mathematic.Right, scope, out right))
                        {
                            address = String.Empty;
                            return false; // nested call will have already set error message
                        }

                        builder.Append(right);
                        address = builder.ToString();
                        return true;

                    case MathematicOperation.Subtract:
                        if (integer != null)
                        {
                            builder.Append("_v-");
                            builder.Append(integer.Value);
                            address = builder.ToString();
                            return true;
                        }

                        address = String.Empty;
                        return EvaluationError(mathematic.Right, "Only constants can be subtracted");

                    case MathematicOperation.Multiply:
                        if (integer != null)
                        {
                            builder.Append('*');
                            builder.Append(integer.Value);
                            address = builder.ToString();
                            return true;
                        }
                        break;
                }
            }

            var functionCall = expression as FunctionCallExpression;
            if (functionCall != null)
            {
                var function = scope.GetFunction(functionCall.FunctionName);
                if (function == null)
                {
                    address = String.Empty;
                    return EvaluationError(expression, "Unknown function: " + functionCall.FunctionName);
                }

                if (function.Expressions.Count != 1)
                {
                    address = String.Empty;
                    return EvaluationError(expression, "parameter does not evaluate to a memory address");
                }

                var innerScope = GetParameters(function, functionCall, scope);
                if (innerScope != null)
                {
                    var returnExpression = function.Expressions.First() as ReturnExpression;
                    if (returnExpression != null)
                        return EvaluateAddress(returnExpression.Value, innerScope, out address);

                    return EvaluateAddress(function.Expressions.First(), innerScope, out address);
                }
            }
            
            address = String.Empty;
            return EvaluationError(expression, "parameter does not evaluate to a memory address");
        }

        private bool EvaluateAddress(ExpressionBase expression, InterpreterScope scope, out Field addressField)
        {
            addressField = new Field();

            switch (expression.Type)
            {
                case ExpressionType.IntegerConstant:
                    addressField = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = (uint)((IntegerConstantExpression)expression).Value };
                    return true;

                case ExpressionType.Return:
                    return EvaluateAddress(((ReturnExpression)expression).Value, scope, out addressField);

                case ExpressionType.FunctionCall:
                    var functionCall = (FunctionCallExpression)expression;
                    var function = scope.GetFunction(functionCall.FunctionName);
                    if (function != null)
                    {
                        if (function.Expressions.Count != 1)
                            return EvaluationError(expression, "parameter does not evaluate to a memory address");

                        var innerScope = GetParameters(function, functionCall, scope);
                        if (innerScope == null)
                            return false;

                        return EvaluateAddress(function.Expressions.First(), innerScope, out addressField);
                    }

                    var fieldSize = GetMemoryLookupFunctionSize(functionCall.FunctionName);
                    if (fieldSize == FieldSize.None)
                        return EvaluationError(expression, "parameter does not evaluate to a memory address");

                    ExpressionBase addressExpression;
                    if (!functionCall.Parameters.First().ReplaceVariables(scope, out addressExpression))
                        return EvaluationError(functionCall.Parameters.First(), addressExpression);

                    if (!EvaluateAddress(addressExpression, scope, out addressField))
                        return false;

                    addressField = new Field { Size = fieldSize, Type = addressField.Type, Value = addressField.Value };
                    return true;
            }

            return false;
        }

        private static FunctionDefinitionExpression _leaderboardFunction;

        private bool ExecuteLeaderboard(FunctionCallExpression expression, InterpreterScope scope)
        {
            if (_leaderboardFunction == null)
            {
                _leaderboardFunction = new FunctionDefinitionExpression("leaderboard");
                _leaderboardFunction.Parameters.Add("title");
                _leaderboardFunction.Parameters.Add("description");
                _leaderboardFunction.Parameters.Add("start");
                _leaderboardFunction.Parameters.Add("cancel");
                _leaderboardFunction.Parameters.Add("submit");
                _leaderboardFunction.Parameters.Add("value");
            }

            scope = GetParameters(_leaderboardFunction, expression, scope);
            if (scope == null)
                return false;

            var leaderboard = new Leaderboard();

            var str = scope.GetVariable("title") as StringConstantExpression;
            if (str != null)
                leaderboard.Title = str.Value;

            str = scope.GetVariable("description") as StringConstantExpression;
            if (str != null)
                leaderboard.Description = str.Value;

            var achievement = new AchievementBuilder();
            if (!ExecuteAchievementExpression(achievement, scope.GetVariable("start"), scope))
                return false;
            achievement.Optimize();
            leaderboard.Start = achievement.SerializeRequirements();

            achievement = new AchievementBuilder();
            if (!ExecuteAchievementExpression(achievement, scope.GetVariable("cancel"), scope))
                return false;
            achievement.Optimize();
            leaderboard.Cancel = achievement.SerializeRequirements();

            achievement = new AchievementBuilder();
            if (!ExecuteAchievementExpression(achievement, scope.GetVariable("submit"), scope))
                return false;
            achievement.Optimize();
            leaderboard.Submit = achievement.SerializeRequirements();

            string value;
            if (!EvaluateAddress(scope.GetVariable("value"), scope, out value))
                return false;
            leaderboard.Value = value;

            _leaderboards.Add(leaderboard);
            return true;
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
                    if (!function.Parameters.Contains(assignedParameter.Variable.Name))
                    {
                        EvaluationError(parameter, String.Format("{0} does not have a {1} parameter", function.Name, assignedParameter.Variable));
                        return null;
                    }

                    ExpressionBase value;
                    if (!assignedParameter.Value.ReplaceVariables(scope, out value))
                    {
                        EvaluationError(assignedParameter.Value, value);
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
                        EvaluationError(parameter, (ParseErrorExpression)value);
                        return null;
                    }

                    innerScope.AssignVariable(new VariableExpression(function.Parameters.ElementAt(index)), value);
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

        private bool EvaluationError(ExpressionBase expression, ExpressionBase error)
        {
            var parseError = error as ParseErrorExpression;
            if (parseError != null)
            {
                if (error.Line != 0)
                    ErrorMessage = String.Format("{0}:{1} {2}", error.Line, error.Column, parseError.Message);
                else
                    ErrorMessage = String.Format("{0}:{1} {2}", expression.Line, expression.Column, parseError.Message);
            }
            else
            {
                ErrorMessage = String.Format("{0}:{1} Unknown error", expression.Line, expression.Column);
            }

            return false;
        }
    }
}
