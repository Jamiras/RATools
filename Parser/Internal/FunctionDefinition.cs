using System;
using System.Collections.Generic;
using System.Diagnostics;
using Jamiras.Components;

namespace RATools.Parser.Internal
{
    [DebuggerDisplay("function {Name,nq}")]
    internal class FunctionDefinition
    {
        public FunctionDefinition(string name)
            : this()
        {
            Name = name;
        }

        public FunctionDefinition()
        {
            Parameters = new List<string>();
            Expressions = new List<ExpressionDefinition>();
        }

        public string Name { get; private set; }

        public List<string> Parameters { get; private set; }
        public List<ExpressionDefinition> Expressions { get; private set; }

        public string Parse(PositionalTokenizer tokenizer)
        {
            AchievementScriptParser.SkipWhitespace(tokenizer);

            var functionName = tokenizer.ReadIdentifier();
            if (functionName.IsEmpty)
                return AchievementScriptParser.ParseError(tokenizer, "Invalid function name");
            Name = functionName.ToString();

            AchievementScriptParser.SkipWhitespace(tokenizer);
            if (tokenizer.NextChar != '(')
                return AchievementScriptParser.ParseError(tokenizer, "Expected '(' after function name");
            tokenizer.Advance();

            AchievementScriptParser.SkipWhitespace(tokenizer);
            if (tokenizer.NextChar != ')')
            {
                do
                {
                    var parameter = tokenizer.ReadIdentifier();
                    if (parameter.IsEmpty)
                        return AchievementScriptParser.ParseError(tokenizer, "Invalid parameter name");

                    Parameters.Add(parameter.ToString());

                    AchievementScriptParser.SkipWhitespace(tokenizer);
                    if (tokenizer.NextChar == ')')
                        break;

                    if (tokenizer.NextChar != ',')
                        return AchievementScriptParser.ParseError(tokenizer, "Invalid parameter name");

                    tokenizer.Advance();
                    AchievementScriptParser.SkipWhitespace(tokenizer);
                } while (true);
            }

            tokenizer.Advance(); // closing parenthesis
            AchievementScriptParser.SkipWhitespace(tokenizer);

            ExpressionDefinition expression;
            string message;

            if (tokenizer.Match("=>"))
            {
                AchievementScriptParser.SkipWhitespace(tokenizer);

                expression = new ExpressionDefinition();
                message = expression.Parse(tokenizer);
                if (!String.IsNullOrEmpty(message))
                    return message;

                var returnExpression = new ExpressionDefinition { Operator = ExpressionOperator.Return, Operand = expression };
                Expressions.Add(returnExpression);
                return null;
            }

            if (tokenizer.NextChar != '{')
                return AchievementScriptParser.ParseError(tokenizer, "Opening brace expected after function declaration");

            var functionStartErrorTokenizer = tokenizer.Clone();
            tokenizer.Advance();
            AchievementScriptParser.SkipWhitespace(tokenizer);

            bool seenReturn = false;
            while (tokenizer.NextChar != '}')
            {
                var lineTokenizer = tokenizer.Clone();

                expression = new ExpressionDefinition();
                message = expression.Parse(tokenizer);
                if (!String.IsNullOrEmpty(message))
                {
                    if (tokenizer.NextChar == '\0')
                        return AchievementScriptParser.ParseError(functionStartErrorTokenizer, "No matching closing brace found");

                    return message;
                }

                switch (expression.Operator)
                {
                    case ExpressionOperator.Return:
                        seenReturn = true;
                        break;

                    case ExpressionOperator.Assign:
                    case ExpressionOperator.FunctionCall:
                        if (seenReturn)
                            return AchievementScriptParser.ParseError(lineTokenizer, "Expression after return statement");
                        break;

                    default:
                        return AchievementScriptParser.ParseError(lineTokenizer, "Only assignment, return, and function calls allowed within function body");
                }

                Expressions.Add(expression);

                AchievementScriptParser.SkipWhitespace(tokenizer);
            }

            tokenizer.Advance();
            return null;
        }
    }
}
