using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jamiras.Components;

namespace RATools.Parser.Internal
{
    internal abstract class ExpressionBase
    {
        protected ExpressionBase(ExpressionType type)
        {
            Type = type;
        }

        public ExpressionType Type { get; private set; }
        public int Line { get; protected set; }
        public int Column { get; protected set; }

        internal virtual ExpressionBase Rebalance()
        {
            return this;
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

        public static ExpressionBase Parse(PositionalTokenizer tokenizer)
        {
            SkipWhitespace(tokenizer);

            var line = tokenizer.Line;
            var column = tokenizer.Column;

            var clause = ExpressionBase.ParseClause(tokenizer);
            if (clause.Line == 0)
            {
                clause.Line = line;
                clause.Column = column;
            }

            if (clause.Type == ExpressionType.ParseError)
                return clause;

            SkipWhitespace(tokenizer);

            switch (tokenizer.NextChar)
            {
                case '+':
                    tokenizer.Advance();
                    clause = ParseMathematic(tokenizer, clause, MathematicOperation.Add);
                    break;

                case '-':
                    tokenizer.Advance();
                    clause = ParseMathematic(tokenizer, clause, MathematicOperation.Subtract);
                    break;

                case '*':
                    tokenizer.Advance();
                    clause = ParseMathematic(tokenizer, clause, MathematicOperation.Multiply);
                    break;

                case '/':
                    tokenizer.Advance();
                    clause = ParseMathematic(tokenizer, clause, MathematicOperation.Divide);
                    break;

                case '=':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        clause = ParseComparison(tokenizer, clause, ComparisonOperation.Equal);
                    }
                    else
                    {
                        clause = ParseAssignment(tokenizer, clause);
                    }
                    break;

                case '!':
                    tokenizer.Advance();
                    if (tokenizer.NextChar != '=')
                    {
                        clause = new ParseErrorExpression("= expected following !");
                    }
                    else
                    {
                        tokenizer.Advance();
                        clause = ParseComparison(tokenizer, clause, ComparisonOperation.NotEqual);
                    }
                    break;

                case '<':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        clause = ParseComparison(tokenizer, clause, ComparisonOperation.LessThanOrEqual);
                    }
                    else
                    {
                        clause = ParseComparison(tokenizer, clause, ComparisonOperation.LessThan);
                    }
                    break;

                case '>':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        clause = ParseComparison(tokenizer, clause, ComparisonOperation.GreaterThanOrEqual);
                    }
                    else
                    {
                        clause = ParseComparison(tokenizer, clause, ComparisonOperation.GreaterThan);
                    }
                    break;

                case '&':
                    tokenizer.Advance();
                    if (tokenizer.NextChar != '&')
                    {
                        clause = new ParseErrorExpression("& expected following &");
                    }
                    else
                    {
                        tokenizer.Advance();
                        clause = ParseConditional(tokenizer, clause, ConditionalOperation.And);
                    }
                    break;

                case '|':
                    tokenizer.Advance();
                    if (tokenizer.NextChar != '|')
                    {
                        clause = new ParseErrorExpression("| expected following |");
                    }
                    else
                    {
                        tokenizer.Advance();
                        clause = ParseConditional(tokenizer, clause, ConditionalOperation.Or);
                    }
                    break;

                default:
                    break;
            }

            clause = clause.Rebalance();

            if (clause.Line == 0)
            {
                clause.Line = line;
                clause.Column = column;
            }

            return clause;
        }

        private static ExpressionBase ParseClause(PositionalTokenizer tokenizer)
        {
            ExpressionBase clause;

            switch (tokenizer.NextChar)
            {
                case '!':
                    tokenizer.Advance();
                    clause = ParseClause(tokenizer);
                    if (clause.Type == ExpressionType.ParseError)
                        return clause;

                    return new ConditionalExpression(null, ConditionalOperation.Not, clause);

                case '(':
                    tokenizer.Advance();
                    clause = ExpressionBase.Parse(tokenizer);
                    if (clause.Type == ExpressionType.ParseError)
                        return clause;

                    if (tokenizer.NextChar != ')')
                    {
                        if (tokenizer.NextChar == '\0')
                            return new ParseErrorExpression("No closing parenthesis found");

                        return new ParseErrorExpression("Expected closing parenthesis, found " + tokenizer.NextChar);
                    }

                    tokenizer.Advance();
                    return clause;

                case '"':
                    try
                    {
                        var stringValue = tokenizer.ReadQuotedString().ToString();
                        return new StringConstantExpression(stringValue);
                    }
                    catch (InvalidOperationException ex)
                    {
                        return new ParseErrorExpression(ex.Message);
                    }

                case '0':
                    if (tokenizer.Match("0x"))
                    {
                        string hexNumber = "";
                        while (Char.IsDigit(tokenizer.NextChar) || (tokenizer.NextChar >= 'A' && tokenizer.NextChar <= 'F') || (tokenizer.NextChar >= 'a' && tokenizer.NextChar <= 'f'))
                        {
                            hexNumber += tokenizer.NextChar;
                            tokenizer.Advance();
                        }

                        int hexValue = 0;
                        Int32.TryParse(hexNumber, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.CurrentCulture, out hexValue);
                        return new IntegerConstantExpression(hexValue);
                    }
                    goto case '1';

                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    var number = tokenizer.ReadNumber();
                    int value;
                    Int32.TryParse(number.ToString(), out value);
                    return new IntegerConstantExpression(value);

                case '{':
                    tokenizer.Advance();
                    return ParseDictionary(tokenizer);

                default:
                    var identifier = tokenizer.ReadIdentifier();
                    if (identifier.IsEmpty)
                        return new ParseErrorExpression("No identifier found");

                    SkipWhitespace(tokenizer);

                    if (identifier == "return")
                    {
                        clause = ExpressionBase.Parse(tokenizer);
                        if (clause.Type == ExpressionType.ParseError)
                            return clause;

                        return new ReturnExpression(clause);
                    }

                    if (identifier == "function")
                        return FunctionDefinitionExpression.Parse(tokenizer);
                    if (identifier == "for")
                        return ForExpression.Parse(tokenizer);
                    if (identifier == "if")
                        return IfExpression.Parse(tokenizer);

                    if (tokenizer.NextChar == '(')
                    {
                        tokenizer.Advance();

                        var parameters = new List<ExpressionBase>();
                        var parseError = ParseParameters(tokenizer, parameters);
                        if (parseError != null)
                            return parseError;

                        return new FunctionCallExpression(identifier.ToString(), parameters);
                    }

                    if (tokenizer.NextChar == '[')
                    {
                        tokenizer.Advance();

                        var index = ExpressionBase.Parse(tokenizer);
                        if (index.Type == ExpressionType.ParseError)
                            return index;

                        SkipWhitespace(tokenizer);
                        if (tokenizer.NextChar != ']')
                            return new ParseErrorExpression("Expecting closing bracket after index");
                        tokenizer.Advance();

                        return new IndexedVariableExpression(identifier.ToString(), index);
                    }

                    return new VariableExpression(identifier.ToString());
            }
        }

        private static ExpressionBase ParseParameters(PositionalTokenizer tokenizer, ICollection<ExpressionBase> parameters)
        {
            int line = tokenizer.Line;
            int column = tokenizer.Column;

            SkipWhitespace(tokenizer);

            if (tokenizer.NextChar != ')')
            {
                do
                {
                    var parameter = ExpressionBase.Parse(tokenizer);
                    if (parameter.Type == ExpressionType.ParseError)
                        return parameter;

                    parameters.Add(parameter);

                    SkipWhitespace(tokenizer);
                    if (tokenizer.NextChar != ',')
                        break;

                    tokenizer.Advance();
                    SkipWhitespace(tokenizer);
                } while (true);
            }

            if (tokenizer.NextChar == ')')
            {
                tokenizer.Advance();
                return null;
            }

            var error = (tokenizer.NextChar == '\0') ? new ParseErrorExpression("No closing parenthesis found") : new ParseErrorExpression("Expected closing parenthesis, found " + tokenizer.NextChar);
            error.Line = line;
            error.Column = column;
            return error;
        }

        private static ExpressionBase ParseMathematic(PositionalTokenizer tokenizer, ExpressionBase left, MathematicOperation operation)
        {
            var right = ExpressionBase.Parse(tokenizer);
            if (right.Type == ExpressionType.ParseError)
                return right;

            return new MathematicExpression(left, operation, right);
        }

        private static ExpressionBase ParseComparison(PositionalTokenizer tokenizer, ExpressionBase left, ComparisonOperation operation)
        {
            var right = ExpressionBase.Parse(tokenizer);
            if (right.Type == ExpressionType.ParseError)
                return right;

            return new ComparisonExpression(left, operation, right);
        }

        private static ExpressionBase ParseConditional(PositionalTokenizer tokenizer, ExpressionBase left, ConditionalOperation operation)
        {
            var right = ExpressionBase.Parse(tokenizer);
            if (right.Type == ExpressionType.ParseError)
                return right;

            return new ConditionalExpression(left, operation, right);
        }

        private static ExpressionBase ParseAssignment(PositionalTokenizer tokenizer, ExpressionBase variable)
        {
            if (variable.Type != ExpressionType.Variable)
                return new ParseErrorExpression("Cannot assign value to non-variable");

            var value = ExpressionBase.Parse(tokenizer);
            if (value.Type == ExpressionType.ParseError)
                return value;

            return new AssignmentExpression((VariableExpression)variable, value);
        }

        private static ExpressionBase ParseDictionary(PositionalTokenizer tokenizer)
        {
            SkipWhitespace(tokenizer);

            var dict = new DictionaryExpression();
            while (tokenizer.NextChar != '}')
            {
                var key = ParseClause(tokenizer);
                if (key.Type == ExpressionType.ParseError)
                    return key;

                SkipWhitespace(tokenizer);
                if (tokenizer.NextChar != ':')
                    return new ParseErrorExpression("Expecting colon following key expression");
                tokenizer.Advance();
                SkipWhitespace(tokenizer);

                var value = ParseClause(tokenizer);
                if (value.Type == ExpressionType.ParseError)
                    return value;

                dict.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = key, Value = value });

                SkipWhitespace(tokenizer);
                if (tokenizer.NextChar == '}')
                    break;

                if (tokenizer.NextChar != ',')
                    return new ParseErrorExpression("Expecting comma between entries");
                tokenizer.Advance();
                SkipWhitespace(tokenizer);
            }

            tokenizer.Advance();
            return dict;
        }

        internal static ExpressionBase ParseStatementBlock(PositionalTokenizer tokenizer, ICollection<ExpressionBase> expressions)
        {
            ExpressionBase.SkipWhitespace(tokenizer);

            if (tokenizer.NextChar != '{')
            {
                var statement = ExpressionBase.Parse(tokenizer);
                if (statement.Type == ExpressionType.ParseError)
                    return statement;

                expressions.Add(statement);
            }
            else
            {
                var line = tokenizer.Line;
                var column = tokenizer.Column;

                tokenizer.Advance();
                do
                {
                    ExpressionBase.SkipWhitespace(tokenizer);
                    if (tokenizer.NextChar == '}')
                        break;

                    if (tokenizer.NextChar == '\0')
                        return new ParseErrorExpression("No matching closing brace found", line, column);

                    var statement = ExpressionBase.Parse(tokenizer);
                    if (statement.Type == ExpressionType.ParseError)
                        return statement;

                    expressions.Add(statement);
                } while (true);

                tokenizer.Advance();
            }

            return null;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append(Type);
            builder.Append(": ");
            AppendString(builder);
            return builder.ToString();
        }

        internal virtual void AppendString(StringBuilder builder)
        {
        }

        public virtual bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            result = this;
            return true;
        }

        public virtual bool IsTrue(InterpreterScope scope, out ParseErrorExpression error)
        {
            error = null;
            return false;
        }
    }

    public enum ExpressionType
    {
        None = 0,

        Variable,
        IntegerConstant,
        StringConstant,
        FunctionCall,
        Mathematic,
        Comparison,
        Conditional,
        Assignment,

        FunctionDefinition,
        Return,
        Dictionary,
        For,
        If,

        ParseError,
    }
}
