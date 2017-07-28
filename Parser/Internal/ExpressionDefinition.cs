using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jamiras.Components;

namespace RATools.Parser.Internal
{
    internal class ExpressionDefinition
    {
        public ExpressionDefinition()
        {
            Parameters = new List<ExpressionDefinition>();
        }

        public string Identifier { get; set; }
        public ExpressionOperator Operator { get; set; }
        public ExpressionDefinition Operand { get; set; }
        public List<ExpressionDefinition> Parameters { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }

        public string Parse(PositionalTokenizer tokenizer)
        {
            ExpressionDefinition expression = null;
            string message;

            Line = tokenizer.Line;
            Column = tokenizer.Column;

            AchievementScriptParser.SkipWhitespace(tokenizer);
            if (tokenizer.NextChar == '!')
            {
                tokenizer.Advance();

                message = ParseClause(tokenizer, out expression);
                if (!String.IsNullOrEmpty(message))
                    return message;

                expression = new ExpressionDefinition { Operator = ExpressionOperator.Not, Operand = expression };
            }
            else
            {
                message = ParseClause(tokenizer, out expression);
                if (!String.IsNullOrEmpty(message))
                    return message;
            }

            AchievementScriptParser.SkipWhitespace(tokenizer);

            switch (tokenizer.NextChar)
            {
                case '+':
                    Operator = ExpressionOperator.Add;
                    tokenizer.Advance();
                    break;

                case '-':
                    Operator = ExpressionOperator.Subtract;
                    tokenizer.Advance();
                    break;

                case '*':
                    Operator = ExpressionOperator.Multiply;
                    tokenizer.Advance();
                    break;

                case '<':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        Operator = ExpressionOperator.LessThanOrEqual;
                        tokenizer.Advance();
                    }
                    else
                    {
                        Operator = ExpressionOperator.LessThan;
                    }
                    break;

                case '>':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        Operator = ExpressionOperator.GreaterThanOrEqual;
                        tokenizer.Advance();
                    }
                    else
                    {
                        Operator = ExpressionOperator.GreaterThan;
                    }
                    break;

                case '=':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        Operator = ExpressionOperator.Equal;
                    }
                    else
                    {
                        Operator = ExpressionOperator.Assign;
                    }
                    break;

                case '!':
                    tokenizer.Advance();
                    if (tokenizer.NextChar != '=')
                        return AchievementScriptParser.ParseError(tokenizer, "! is not a valid operator");

                    tokenizer.Advance();
                    Operator = ExpressionOperator.NotEqual;
                    break;

                case '&':
                    tokenizer.Advance();
                    if (tokenizer.NextChar != '&')
                        return AchievementScriptParser.ParseError(tokenizer, "logical and requires two ampersands");

                    tokenizer.Advance();
                    Operator = ExpressionOperator.And;
                    break;

                case '|':
                    tokenizer.Advance();
                    if (tokenizer.NextChar != '|')
                        return AchievementScriptParser.ParseError(tokenizer, "logical or requires two pipes");

                    tokenizer.Advance();
                    Operator = ExpressionOperator.Or;
                    break;

                default:
                    if (expression == null)
                        return null;

                    Identifier = expression.Identifier;
                    Operator = expression.Operator;
                    Operand = expression.Operand;
                    Parameters = expression.Parameters;
                    return null;
            }

            if (expression.Operator == ExpressionOperator.None)
            {
                Identifier = expression.Identifier;
            }
            else
            {
                Parameters = new List<ExpressionDefinition>();
                Parameters.Add(expression);
            }

            AchievementScriptParser.SkipWhitespace(tokenizer);
            Operand = new ExpressionDefinition();
            message = Operand.Parse(tokenizer);
            if (!String.IsNullOrEmpty(message))
                return message;

            if (IsOperatorLowerPriority(Operand.Operator, Operator))
            {
                expression = new ExpressionDefinition { Column = Column, Line = Line, Identifier = Identifier, Operator = Operator, Parameters = Parameters, Operand = Operand };
                Identifier = null;
                Parameters = new List<ExpressionDefinition>();
                Parameters.Add(expression);
                Operator = Operand.Operator;
                Operand = Operand.Operand;
                expression.Operand.Operand = null;
                expression.Operand.Operator = ExpressionOperator.None;
            }

            return null;
        }

        private bool IsOperatorLowerPriority(ExpressionOperator left, ExpressionOperator right)
        {
            if (right == ExpressionOperator.None)
                return false;

            var buckets = new ExpressionOperator[][] {
                new [] { ExpressionOperator.Assign },
                new [] { ExpressionOperator.And, ExpressionOperator.Or },
                new [] { ExpressionOperator.Equal, ExpressionOperator.NotEqual, ExpressionOperator.LessThan, ExpressionOperator.LessThanOrEqual, ExpressionOperator.GreaterThan, ExpressionOperator.GreaterThanOrEqual },
                new [] { ExpressionOperator.Add, ExpressionOperator.Subtract },
                new [] { ExpressionOperator.Multiply },
            };

            int leftPriority = buckets.Length;
            int rightPriority = buckets.Length;

            for (int i = 0; i < buckets.Length; i++)
            {
                if (buckets[i].Contains(left))
                    leftPriority = i;
                if (buckets[i].Contains(right))
                    rightPriority = i;
            }

            return (leftPriority < rightPriority);
        }

        private string ParseClause(PositionalTokenizer tokenizer, out ExpressionDefinition expression)
        {
            if (tokenizer.NextChar == '(')
            {
                var clauseStart = tokenizer.Clone();

                tokenizer.Advance();
                expression = new ExpressionDefinition();
                var message = expression.Parse(tokenizer);
                if (!String.IsNullOrEmpty(message))
                    return message;

                AchievementScriptParser.SkipWhitespace(tokenizer);
                if (tokenizer.NextChar != ')')
                {
                    if (tokenizer.NextChar == '\0')
                        return AchievementScriptParser.ParseError(clauseStart, "No closing parenthesis found");

                    return AchievementScriptParser.ParseError(tokenizer, "Expected closing parenthesis, found " + tokenizer.NextChar);
                }

                tokenizer.Advance();
            }
            else if (tokenizer.NextChar == '"')
            {
                string value = tokenizer.ReadQuotedString().ToString();
                expression = new ExpressionDefinition { Identifier = '"' + value };
            }
            else if (Char.IsDigit(tokenizer.NextChar))
            {
                string identifier = "";
                string additionalChars = String.Empty;
                if (tokenizer.Match("0x"))
                {
                    identifier = "0x";
                    additionalChars = "abcdefABCDEF";
                }

                while (Char.IsDigit(tokenizer.NextChar) || additionalChars.IndexOf(tokenizer.NextChar) != -1)
                {
                    identifier += tokenizer.NextChar;
                    tokenizer.Advance();
                }

                expression = new ExpressionDefinition { Identifier = identifier };
            }
            else
            {
                expression = null;

                var identifier = tokenizer.ReadIdentifier();
                if (identifier.IsEmpty)
                    return AchievementScriptParser.ParseError(tokenizer, "No identifier found");

                if (identifier == "return")
                {
                    Operator = ExpressionOperator.Return;
                    Operand = new ExpressionDefinition();
                    return Operand.Parse(tokenizer);
                }

                AchievementScriptParser.SkipWhitespace(tokenizer);
                if (tokenizer.NextChar == '(')
                {
                    expression = new ExpressionDefinition { Operator = ExpressionOperator.FunctionCall, Identifier = identifier.ToString() };
                    var message = expression.ParseParameters(tokenizer);
                    if (!String.IsNullOrEmpty(message))
                        return message;
                }
                else
                {
                    expression = new ExpressionDefinition { Identifier = identifier.ToString() };
                }
            }

            return null;
        }

        private string ParseParameters(PositionalTokenizer tokenizer)
        {
            var parametersStart = tokenizer.Clone();
            tokenizer.Advance(); // '('
            AchievementScriptParser.SkipWhitespace(tokenizer);

            if (tokenizer.NextChar != ')')
            {
                do
                {
                    var parameter = new ExpressionDefinition();
                    var message = parameter.Parse(tokenizer);
                    if (!String.IsNullOrEmpty(message))
                        return message;

                    if (Parameters == null)
                        Parameters = new List<ExpressionDefinition>();
                    Parameters.Add(parameter);

                    AchievementScriptParser.SkipWhitespace(tokenizer);
                    if (tokenizer.NextChar != ',')
                        break;

                    tokenizer.Advance();
                    AchievementScriptParser.SkipWhitespace(tokenizer);
                } while (true);
            }

            if (tokenizer.NextChar == ')')
            {
                tokenizer.Advance();
                return null;
            }

            if (tokenizer.NextChar == '\0')
                return AchievementScriptParser.ParseError(parametersStart, "No closing parenthesis found");

            return AchievementScriptParser.ParseError(parametersStart, "Expected closing parenthesis, found " + tokenizer.NextChar);
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            BuildToString(builder);
            return builder.ToString();
        }

        private void BuildToString(StringBuilder builder)
        {
            string oper = null;

            switch (Operator)
            {
                case ExpressionOperator.None:
                    builder.Append(Identifier);
                    if (Identifier[0] == '"')
                        builder.Append('"');                        
                    return;

                case ExpressionOperator.FunctionCall:
                    builder.Append(Identifier);
                    builder.Append('(');
                    if (Parameters != null && Parameters.Count > 0)
                    {
                        foreach (var parameter in Parameters)
                        {
                            parameter.BuildToString(builder);
                            builder.Append(", ");
                        }
                        builder.Length -= 2;
                    }
                    builder.Append(')');
                    return;

                case ExpressionOperator.Return:
                    builder.Append("return ");
                    break;

                case ExpressionOperator.Assign:
                    builder.Append(Identifier);
                    builder.Append(" = ");
                    break;

                case ExpressionOperator.Add:
                    oper = "+";
                    break;

                case ExpressionOperator.Subtract:
                    oper = "-";
                    break;

                case ExpressionOperator.Multiply:
                    oper = "*";
                    break;

                case ExpressionOperator.Not:
                    builder.Append('!');
                    break;

                case ExpressionOperator.Equal:
                    oper = "==";
                    break;

                case ExpressionOperator.NotEqual:
                    oper = "!=";
                    break;

                case ExpressionOperator.GreaterThan:
                    oper = ">";
                    break;

                case ExpressionOperator.GreaterThanOrEqual:
                    oper = ">=";
                    break;

                case ExpressionOperator.LessThan:
                    oper = "<";
                    break;

                case ExpressionOperator.LessThanOrEqual:
                    oper = "<=";
                    break;

                case ExpressionOperator.And:
                    oper = "&&";
                    break;

                case ExpressionOperator.Or:
                    oper = "||";
                    break;
            }

            if (!String.IsNullOrEmpty(oper))
            {
                if (!String.IsNullOrEmpty(Identifier))
                {
                    builder.Append(Identifier);
                    if (Identifier[0] == '"')
                        builder.Append('"');
                }
                else if (Parameters != null && Parameters.Count > 0)
                {
                    if (Parameters[0].Operator == ExpressionOperator.Not)
                    {
                        Parameters[0].BuildToString(builder);
                    }
                    else
                    {
                        builder.Append('(');
                        Parameters[0].BuildToString(builder);
                        builder.Append(')');
                    }
                }

                builder.Append(' ');
                builder.Append(oper);
                builder.Append(' ');
            }

            if (Operand != null)
                Operand.BuildToString(builder);
            else
                builder.Append("null");
        }
    }

    public enum ExpressionOperator
    {
        None = 0,
        FunctionCall,
        Return,
        Assign,
        Add,
        Subtract,
        Multiply,
        Not,
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        And,
        Or,
    }

}
