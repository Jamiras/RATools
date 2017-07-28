using System;
using System.Collections.Generic;
using System.Linq;
using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Internal;

namespace RATools.Parser
{
    public class AchievementScriptParser
    {
        public AchievementScriptParser()
        {
            _functions = new List<FunctionDefinition>();
            _globalVariables = new TinyDictionary<string, ExpressionDefinition>();
            _achievements = new List<Achievement>();
        }

        private List<FunctionDefinition> _functions;
        private TinyDictionary<string, ExpressionDefinition> _globalVariables;
        private List<Achievement> _achievements;

        public bool Parse(Tokenizer input)
        {
            var tokenizer = new PositionalTokenizer(input);
            do
            {
                tokenizer.SkipWhitespace();

                if (tokenizer.Match("//"))
                {
                    var line = tokenizer.ReadTo('\n').Trim();
                    if (line.StartsWith("#ID"))
                        ExtractGameId(line);

                    continue;
                }

                if (tokenizer.Match("function "))
                {
                    if (!ParseFunction(tokenizer))
                        return false;

                    continue;
                }

                var expressionStart = tokenizer.Clone();

                var expression = new ExpressionDefinition();
                ErrorMessage = expression.Parse(tokenizer);
                if (!String.IsNullOrEmpty(ErrorMessage))
                    return false;

                switch (expression.Operator)
                {
                    case ExpressionOperator.Assign:
                        _globalVariables[expression.Identifier] = expression.Operand;
                        break;

                    case ExpressionOperator.FunctionCall:
                        if (!CallFunction(expression, _globalVariables))
                            return false;
                        break;

                    default:
                        ErrorMessage = ParseError(expressionStart, "Only assignment statements, function calls and function definitions allowed at outer scope");
                        return false;
                }

                tokenizer.SkipWhitespace();
            } while (tokenizer.NextChar != '\0');

            return false;
        }

        internal static void SkipWhitespace(PositionalTokenizer tokenizer)
        {
            do
            {
                tokenizer.SkipWhitespace();
                if (!tokenizer.Match("//"))
                    break;

                tokenizer.ReadTo('\n');
                tokenizer.Advance();
            } while (true);
        }

        private bool CallFunction(ExpressionDefinition expression, TinyDictionary<string, ExpressionDefinition> variables)
        {
            var func = _functions.FirstOrDefault(f => f.Name == expression.Identifier);
            if (func != null)
                return EvaluateFunction(func, expression, variables);

            if (expression.Identifier == "achievement")
                return EvaluateAchievement(expression, variables);

            return true;
        }

        private static FunctionDefinition _achievementFunction;

        private bool EvaluateAchievement(ExpressionDefinition expression, TinyDictionary<string, ExpressionDefinition> variables)
        {
            if (_achievementFunction == null)
            {
                _achievementFunction = new FunctionDefinition("achievement");
                _achievementFunction.Parameters.Add("title");
                _achievementFunction.Parameters.Add("description");
                _achievementFunction.Parameters.Add("points");
                _achievementFunction.Parameters.Add("trigger");
            }

            var parameters = new TinyDictionary<string,ExpressionDefinition>();
            if (!GetParameters(parameters, _achievementFunction, expression, variables))
                return false;

            var achievement = new AchievementBuilder();
            achievement.Title = GetString(parameters["title"]);
            achievement.Description = GetString(parameters["description"]);

            int points;
            string pointsString = parameters["points"].Identifier;
            if (pointsString != null && Int32.TryParse(pointsString, out points))
                achievement.Points = points;

            if (!EvaluateAchievementExpression(achievement, parameters["trigger"], variables))
                return false;

            achievement.Optimize();
            _achievements.Add(achievement.ToAchievement());
            return true;
        }

        private bool EvaluateAchievementExpression(AchievementBuilder achievement, ExpressionDefinition expression, TinyDictionary<string, ExpressionDefinition> variables)
        {
            ExpressionDefinition operand;

            switch (expression.Operator)
            {
                case ExpressionOperator.FunctionCall:
                    return EvaluateAchievementFunction(achievement, expression, variables);

                case ExpressionOperator.Assign:
                    if (!EvaluateExpression(expression.Operand, variables, out operand))
                        return false;
                    variables[expression.Identifier] = operand;
                    return true;

                case ExpressionOperator.Not:
                    achievement.IsInNot = !achievement.IsInNot;
                    if (!EvaluateAchievementExpression(achievement, expression.Operand, variables))
                        return false;
                    achievement.IsInNot = !achievement.IsInNot;
                    return true;

                case ExpressionOperator.And:
                    if (achievement.IsInNot)
                        achievement.BeginAlt();
                    operand = GetLeftExpression(expression);
                    if (!EvaluateAchievementExpression(achievement, operand, variables))
                        return false;
                    if (achievement.IsInNot)
                        achievement.BeginAlt();
                    if (!EvaluateAchievementExpression(achievement, expression.Operand, variables))
                        return false;
                    return true;

                case ExpressionOperator.Or:
                    if (!achievement.IsInNot)
                        achievement.BeginAlt();
                    operand = GetLeftExpression(expression);
                    if (!EvaluateAchievementExpression(achievement, operand, variables))
                        return false;
                    if (!achievement.IsInNot)
                        achievement.BeginAlt();
                    if (!EvaluateAchievementExpression(achievement, expression.Operand, variables))
                        return false;
                    return true;

                case ExpressionOperator.Equal:
                case ExpressionOperator.NotEqual:
                case ExpressionOperator.LessThan:
                case ExpressionOperator.LessThanOrEqual:
                case ExpressionOperator.GreaterThan:
                case ExpressionOperator.GreaterThanOrEqual:
                    achievement.EqualityModifier = 0;

                    operand = GetLeftExpression(expression);
                    if (!EvaluateAchievementExpression(achievement, operand, variables))
                        return false;
                    if (!EvaluateVariables(expression.Operand, variables, out operand))
                        return false;

                    var op = GetRequirementOperator(expression.Operator);
                    if (achievement.IsInNot)
                        op = GetOppositeRequirementOperator(op);

                    if (operand.Operator == ExpressionOperator.None)
                    {
                        long value = ParseNumber(operand.Identifier) + achievement.EqualityModifier;
                        var requirement = achievement.LastRequirement;
                        requirement.Operator = op;
                        requirement.Right = new Field { Size = requirement.Left.Size, Type = FieldType.Value, Value = (uint)value };
                    }
                    else
                    {
                        achievement.EqualityModifier = -achievement.EqualityModifier;
                        if (!EvaluateAchievementExpression(achievement, expression.Operand, variables))
                            return false;

                        if (achievement.EqualityModifier != 0)
                            return EvaluationError(expression, "expansion of function calls results in non-zero modifier when comparing multiple memory addresses");

                        var extraRequirement = achievement.LastRequirement;
                        achievement.Current.RemoveAt(achievement.Current.Count - 1);

                        var requirement = achievement.LastRequirement;
                        requirement.Operator = op;
                        requirement.Right = extraRequirement.Left;
                    }

                    return true;

                case ExpressionOperator.Return:
                    return EvaluateAchievementExpression(achievement, expression.Operand, variables);

                case ExpressionOperator.Add:
                    if (!EvaluateAchievementExpression(achievement, expression.Parameters[0], variables))
                        return false;

                    if (!EvaluateVariables(expression.Operand, variables, out operand))
                        return false;
                    achievement.EqualityModifier -= (int)ParseNumber(operand.Identifier);
                    return true;

                case ExpressionOperator.Subtract:
                    if (!EvaluateAchievementExpression(achievement, expression.Parameters[0], variables))
                        return false;

                    if (!EvaluateVariables(expression.Operand, variables, out operand))
                        return false;
                    achievement.EqualityModifier += (int)ParseNumber(operand.Identifier);
                    return true;

                case ExpressionOperator.None:
                    if (!EvaluateVariables(expression, variables, out operand))
                        return false;

                    return EvaluateAchievementExpression(achievement, operand, variables);
            }

            return false;
        }

        private RequirementOperator GetRequirementOperator(ExpressionOperator expressionOperator)
        {
            switch (expressionOperator)
            {
                case ExpressionOperator.Equal: return RequirementOperator.Equal;
                case ExpressionOperator.NotEqual: return RequirementOperator.NotEqual;
                case ExpressionOperator.LessThan: return RequirementOperator.LessThan;
                case ExpressionOperator.LessThanOrEqual: return RequirementOperator.LessThanOrEqual;
                case ExpressionOperator.GreaterThan: return RequirementOperator.GreaterThan;
                case ExpressionOperator.GreaterThanOrEqual: return RequirementOperator.GreaterThanOrEqual;
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

        private bool EvaluateAchievementFunction(AchievementBuilder achievement, ExpressionDefinition expression, TinyDictionary<string, ExpressionDefinition> variables)
        {
            var func = _functions.FirstOrDefault(f => f.Name == expression.Identifier);
            if (func != null)
            {
                var parameters = new TinyDictionary<string, ExpressionDefinition>();
                if (!GetParameters(parameters, func, expression, variables))
                    return false;

                foreach (var cmd in func.Expressions)
                {
                    if (!EvaluateAchievementExpression(achievement, cmd, parameters))
                        return false;
                }

                return true;
            }

            FieldSize fieldSize = GetMemoryLookupFunctionSize(expression.Identifier);
            if (fieldSize != FieldSize.None)
            {
                ExpressionDefinition address;
                if (!EvaluateExpression(expression.Parameters[0], variables, out address))
                    return false;
                if (!EvaluateVariables(address, variables, out address))
                    return false;
                if (address.Operator != ExpressionOperator.None)
                    return EvaluationError(expression, "address did not resolve to a constant");

                var requirement = new Requirement { Operator = achievement.IsInNot ? RequirementOperator.Equal : RequirementOperator.NotEqual };
                requirement.Left = new Field { Size = fieldSize, Type = FieldType.MemoryAddress, Value = ParseNumber(address.Identifier) };
                requirement.Right = new Field { Size = fieldSize, Type = FieldType.Value, Value = 0 };
                achievement.Current.Add(requirement);
                return true;
            }

            if (expression.Identifier == "once")
            {
                if (!EvaluateAchievementExpression(achievement, expression.Parameters[0], variables))
                    return false;

                var requirement = achievement.LastRequirement;
                requirement.HitCount = 1;
                return true;
            }

            if (expression.Identifier == "never")
            {
                if (!EvaluateAchievementExpression(achievement, expression.Parameters[0], variables))
                    return false;

                var requirement = achievement.LastRequirement;
                requirement.Type = RequirementType.ResetIf;
                return true;
            }

            return EvaluationError(expression, "unsupported function within achievement");
        }

        private static uint ParseNumber(string number)
        {
            uint value = 0;

            if (number.StartsWith("0x"))
                UInt32.TryParse(number.Substring(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.CurrentCulture, out value);
            else
                UInt32.TryParse(number, out value);

            return value;
        }

        private bool EvaluateFunction(FunctionDefinition func, ExpressionDefinition expression, TinyDictionary<string, ExpressionDefinition> variables)
        {
            var parameters = new TinyDictionary<string, ExpressionDefinition>();
            if (!GetParameters(parameters, func, expression, variables))
                return false;

            foreach (var cmd in func.Expressions)
            {
                switch (cmd.Operator)
                {
                    case ExpressionOperator.FunctionCall:
                        CallFunction(cmd, parameters);
                        break;

                    case ExpressionOperator.Assign:
                        ExpressionDefinition operand;
                        if (!EvaluateExpression(expression.Operand, parameters, out operand))
                            return false;

                        parameters[expression.Identifier] = operand;
                        break;

                    default:
                        return EvaluationError(cmd, "only assignments and function calls allowed within function");
                }
            }

            return true;
        }

        private bool EvaluateExpression(ExpressionDefinition expression, TinyDictionary<string, ExpressionDefinition> variables, out ExpressionDefinition operand)
        {
            operand = null;

            switch (expression.Operator)
            {
                case ExpressionOperator.FunctionCall:
                    var func = _functions.FirstOrDefault(f => f.Name == expression.Identifier);
                    if (func != null)
                    {
                        var parameters = new TinyDictionary<string, ExpressionDefinition>();
                        if (!GetParameters(parameters, func, expression, variables))
                            return false;

                        foreach (var cmd in func.Expressions)
                        {
                            if (!EvaluateExpression(cmd, parameters, out operand))
                                return false;
                        }

                        return true;
                    }

                    var fieldSize = GetMemoryLookupFunctionSize(expression.Identifier);
                    if (fieldSize != FieldSize.None)
                    {
                        ExpressionDefinition address;
                        if (!EvaluateExpression(expression.Parameters[0], variables, out address))
                            return false;

                        operand = new ExpressionDefinition { Operator = ExpressionOperator.FunctionCall, Identifier = expression.Identifier };
                        operand.Parameters.Add(address);
                        return true;
                    }

                    return EvaluationError(expression, "unknown function");

                case ExpressionOperator.Return:
                    return EvaluateExpression(expression.Operand, variables, out operand);

                case ExpressionOperator.None:
                    operand = expression;
                    return true;

                case ExpressionOperator.Equal:
                case ExpressionOperator.NotEqual:
                case ExpressionOperator.LessThan:
                case ExpressionOperator.LessThanOrEqual:
                case ExpressionOperator.GreaterThan:
                case ExpressionOperator.GreaterThanOrEqual:
                    // cannot evaluate logic here - it will be evaluated in EvaluateAchievementExpression, just evaluate variables
                    ExpressionDefinition right;
                    if (!EvaluateExpression(expression.Operand, variables, out right))
                        return false;

                    ExpressionDefinition left = GetLeftExpression(expression);
                    if (!EvaluateVariables(left, variables, out left))
                        return false;

                    operand = new ExpressionDefinition { Operator = expression.Operator, Operand = right };
                    if (left.Operator == ExpressionOperator.None)
                        operand.Identifier = left.Identifier;
                    else
                        operand.Parameters.Add(left);

                    return true;
            }

            return EvaluateVariables(expression, variables, out operand);
        }

        private ExpressionDefinition GetLeftExpression(ExpressionDefinition expression)
        {
            if (String.IsNullOrEmpty(expression.Identifier))
                return expression.Parameters[0];

            return new ExpressionDefinition { Identifier = expression.Identifier };
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

        private static string GetString(ExpressionDefinition expression)
        {
            if (expression.Operator == ExpressionOperator.None && !String.IsNullOrEmpty(expression.Identifier))
            {
                if (expression.Identifier[0] == '\"')
                    return expression.Identifier.Substring(1);

                return expression.Identifier;
            }

            return expression.ToString();
        }

        private bool GetParameters(TinyDictionary<string, ExpressionDefinition> parameters, FunctionDefinition func, ExpressionDefinition expression, TinyDictionary<string, ExpressionDefinition> variables)
        {
            if (expression.Parameters == null)
                return true;

            int index = 0;
            bool namedParameters = false;
            foreach (var parameter in expression.Parameters)
            {
                if (parameter.Operator == ExpressionOperator.Assign)
                {
                    if (!func.Parameters.Contains(parameter.Identifier))
                        return EvaluationError(parameter, String.Format("{0} does not have a {1} parameter", func.Name, parameter.Identifier));

                    ExpressionDefinition value;
                    if (!EvaluateVariables(parameter.Operand, variables, out value))
                        return false;

                    parameters[parameter.Identifier] = value;
                    namedParameters = true;
                }
                else
                {
                    if (namedParameters)
                        return EvaluationError(parameter, "non-named parameter following named parameter");

                    if (index == func.Parameters.Count)
                        return EvaluationError(expression, "too many parameters passed to function");

                    ExpressionDefinition value;
                    if (!EvaluateVariables(parameter, variables, out value))
                        return false;

                    parameters[func.Parameters[index]] = value;
                }

                ++index;
            }

            return true;
        }

        private bool EvaluateVariables(ExpressionDefinition expression, TinyDictionary<string, ExpressionDefinition> variables, out ExpressionDefinition result)
        {
            result = null;

            var identifier = expression.Identifier;
            if (!String.IsNullOrEmpty(identifier) && (Char.IsLetter(identifier[0]) || identifier[0] == '_'))
            {
                ExpressionDefinition variableDefinition;
                if (variables.TryGetValue(identifier, out variableDefinition))
                {
                    if (variableDefinition.Operator != ExpressionOperator.None)
                    {
                        if (!EvaluateVariables(variableDefinition, variables, out variableDefinition))
                            return false;
                    }

                    if (expression.Operand == null)
                    {
                        result = variableDefinition;
                        return true;
                    }
                    
                    if (variableDefinition.Operator != ExpressionOperator.None)
                    {
                        result = new ExpressionDefinition { Operator = expression.Operator, Operand = expression.Operand };
                        result.Parameters.Add(variableDefinition);
                        return true;
                    }

                    identifier = variableDefinition.Identifier;
                    expression = new ExpressionDefinition { Identifier = identifier, Operator = expression.Operator, Operand = expression.Operand };
                }
            }

            ExpressionDefinition right;
            uint iLeft, iRight;

            switch (expression.Operator)
            {
                case ExpressionOperator.Add:
                    if (!EvaluateVariables(expression.Operand, variables, out right))
                        return false;

                    if (identifier.Length > 0 && identifier[0] == '"')
                    {
                        if (right.Identifier.Length > 0 && right.Identifier[0] == '"')
                            identifier += right.Identifier.Substring(1);
                        else
                            identifier += right.Identifier;

                        result = new ExpressionDefinition { Identifier = identifier };
                        return true;
                    }

                    if (right.Identifier.Length > 0 && right.Identifier[0] == '"')
                    {
                        identifier = '\"' + identifier + right.Identifier.Substring(1);
                        result = new ExpressionDefinition { Identifier = identifier };
                        return true;
                    }

                    iLeft = ParseNumber(identifier);
                    iRight = ParseNumber(right.Identifier);
                    result = new ExpressionDefinition { Identifier = (iLeft + iRight).ToString() };
                    return true;

                case ExpressionOperator.Multiply:
                    if (!EvaluateVariables(expression.Operand, variables, out right))
                        return false;

                    iLeft = ParseNumber(identifier);
                    iRight = ParseNumber(right.Identifier);
                    result = new ExpressionDefinition { Identifier = (iLeft * iRight).ToString() };
                    return true;

                case ExpressionOperator.FunctionCall:
                    if (expression.Parameters.Count > 0)
                    {
                        var newParameters = new List<ExpressionDefinition>();
                        bool parametersModified = false;
                        foreach (var parameter in expression.Parameters)
                        {
                            ExpressionDefinition newParameter;
                            if (!EvaluateVariables(parameter, variables, out newParameter))
                                return false;

                            newParameters.Add(newParameter);
                            if (!ReferenceEquals(newParameter, parameter))
                                parametersModified = true;
                        }

                        if (parametersModified)
                        {
                            result = new ExpressionDefinition { Identifier = expression.Identifier, Operator = ExpressionOperator.FunctionCall };
                            result.Parameters.AddRange(newParameters);
                            return true;
                        }
                    }

                    result = expression;
                    return true;

                case ExpressionOperator.And:
                case ExpressionOperator.Or:
                    result = expression;
                    return true;
            }

            result = expression;
            return true;
        }

        private bool EvaluationError(ExpressionDefinition expression, string message)
        {
            ErrorMessage = String.Format("{0}:{1} {2}", expression.Line, expression.Column, message);
            return false;
        }

        private bool ParseFunction(PositionalTokenizer tokenizer)
        {
            var func = new FunctionDefinition();
            ErrorMessage = func.Parse(tokenizer);
            if (!String.IsNullOrEmpty(ErrorMessage))
                return false;

            _functions.Add(func);
            return true;
        }

        internal static string ParseError(PositionalTokenizer tokenizer, string message)
        {
            return String.Format("{0}:{1} {2}", tokenizer.Line, tokenizer.Column, message);
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

        public string ErrorMessage { get; private set; }
        
        public int GameId { get; private set; }
    }
}
