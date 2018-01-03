using Jamiras.Components;
using System;
using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Internal
{
    /// <summary>
    /// Base class for a part of an expression.
    /// </summary>
    internal abstract class ExpressionBase
    {
        protected ExpressionBase(ExpressionType type)
        {
            Type = type;
        }

        /// <summary>
        /// Gets the type of this part of the expression.
        /// </summary>
        public ExpressionType Type { get; private set; }

        /// <summary>
        /// Gets the line where this expression started.
        /// </summary>
        public int Line { get; protected set; }

        /// <summary>
        /// Gets the column where this expression started.
        /// </summary>
        public int Column { get; protected set; }

        /// <summary>
        /// Gets or sets whether this is a logical unit.
        /// </summary>
        /// <remarks>
        /// If <c>true</c>, this element was wrapped in parenthesis and cannot be broken by rebalancing.
        /// </remarks>
        public bool IsLogicalUnit { get; set; }

        /// <summary>
        /// Rebalances this expression based on the precendence of operators.
        /// </summary>
        /// <returns>Rebalanced expression</returns>
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

        /// <summary>
        /// Gets the next expression from the input.
        /// </summary>
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

                case '%':
                    tokenizer.Advance();
                    clause = ParseMathematic(tokenizer, clause, MathematicOperation.Modulus);
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
                        if (clause.Type == ExpressionType.ParseError)
                            return new ParseErrorExpression("Invalid expression following &&", clause.Line, clause.Column);
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
                        if (clause.Type == ExpressionType.ParseError)
                            return new ParseErrorExpression("Invalid expression following ||", clause.Line, clause.Column);
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

                    clause.IsLogicalUnit = true;
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
                    {
                        var number = tokenizer.ReadNumber();
                        uint value;
                        UInt32.TryParse(number.ToString(), out value);
                        return new IntegerConstantExpression((int)value);
                    }

                case '-':
                    tokenizer.Advance();
                    if (tokenizer.NextChar >= '0' && tokenizer.NextChar <= '9')
                    {
                        var number = tokenizer.ReadNumber();
                        int value;
                        Int32.TryParse(number.ToString(), out value);
                        return new IntegerConstantExpression(-value);
                    }
                    return new ParseErrorExpression("No identifier found");

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
                        IndexedVariableExpression parent = null;

                        do
                        {
                            tokenizer.Advance();

                            var index = ExpressionBase.Parse(tokenizer);
                            if (index.Type == ExpressionType.ParseError)
                                return index;

                            SkipWhitespace(tokenizer);
                            if (tokenizer.NextChar != ']')
                                return new ParseErrorExpression("Expecting closing bracket after index");
                            tokenizer.Advance();
                            SkipWhitespace(tokenizer);

                            if (parent != null)
                                parent = new IndexedVariableExpression(parent, index);
                            else
                                parent = new IndexedVariableExpression(identifier.ToString(), index);

                        } while (tokenizer.NextChar == '[');

                        return parent;
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

            if (tokenizer.NextChar == '\0')
                return new ParseErrorExpression("No closing parenthesis found", line, column);

            return new ParseErrorExpression("Expected closing parenthesis, found " + tokenizer.NextChar, tokenizer.Line, tokenizer.Column);
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

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append(Type);
            builder.Append(": ");
            AppendString(builder);
            return builder.ToString();
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder"/>.
        /// </summary>
        internal abstract void AppendString(StringBuilder builder);

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override sealed bool Equals(object obj)
        {
            var that = obj as ExpressionBase;
            if (that == null)
                return false;

            if (Type != that.Type)
                return false;

            return Equals(that);
        }

        /// <summary>
        /// Determines whether the specified <see cref="ExpressionBase" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="ExpressionBase" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="ExpressionBase" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected abstract bool Equals(ExpressionBase obj);

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Determines if two <see cref="ExpressionBase"/>s are equivalent.
        /// </summary>
        public static bool operator ==(ExpressionBase left, ExpressionBase right)
        {
            if (ReferenceEquals(left, null))
                return ReferenceEquals(right, null);
            if (ReferenceEquals(right, null))
                return false;
            if (left.Type != right.Type)
                return false;

            return left.Equals(right);
        }

        /// <summary>
        /// Determines if two <see cref="ExpressionBase"/>s are not equivalent.
        /// </summary>
        public static bool operator !=(ExpressionBase left, ExpressionBase right)
        {
            if (ReferenceEquals(left, null))
                return !ReferenceEquals(right, null);
            if (ReferenceEquals(right, null))
                return true;
            if (left.Type != right.Type)
                return true;

            return !left.Equals(right);
        }

        /// <summary>
        /// Replaces the variables in the expression with values from <paramref name="scope"/>.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] The new expression containing the replaced variables.</param>
        /// <returns><c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result"/> will likely be a <see cref="ParseErrorExpression"/>.</returns>
        public virtual bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            result = this;
            return true;
        }

        /// <summary>
        /// Determines whether the expression evaluates to true for the provided <paramref name="scope"/>
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="error">[out] The error that prevented evaluation (or null if successful).</param>
        /// <returns>The result of evaluating the expression</returns>
        public virtual bool IsTrue(InterpreterScope scope, out ParseErrorExpression error)
        {
            error = null;
            return false;
        }
    }

    /// <summary>
    /// The supported expression types.
    /// </summary>
    public enum ExpressionType
    {
        /// <summary>
        /// Unspecified
        /// </summary>
        None = 0,

        /// <summary>
        /// A variable reference.
        /// </summary>
        Variable,

        /// <summary>
        /// An integer constant.
        /// </summary>
        IntegerConstant,

        /// <summary>
        /// A string constant.
        /// </summary>
        StringConstant,

        /// <summary>
        /// A function call.
        /// </summary>
        FunctionCall,

        /// <summary>
        /// A mathematic equation.
        /// </summary>
        Mathematic,

        /// <summary>
        /// A comparison.
        /// </summary>
        Comparison,
        /// <summary>
        /// The conditional
        /// </summary>
        Conditional,

        /// <summary>
        /// An assignment.
        /// </summary>
        Assignment,


        /// <summary>
        /// A function definition.
        /// </summary>
        FunctionDefinition,

        /// <summary>
        /// A return statement.
        /// </summary>
        Return,

        /// <summary>
        /// A dictionary.
        /// </summary>
        Dictionary,

        /// <summary>
        /// A for loop.
        /// </summary>
        For,

        /// <summary>
        /// An if statement.
        /// </summary>
        If,


        /// <summary>
        /// A parse error.
        /// </summary>
        ParseError,
    }
}
