using Jamiras.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        /// Gets the line where this expression ended.
        /// </summary>
        public int EndLine { get; protected set; }

        /// <summary>
        /// Gets the column where this expression ended.
        /// </summary>
        public int EndColumn { get; protected set; }

        /// <summary>
        /// Copies the location of this expression into another expression.
        /// </summary>
        internal void CopyLocation(ExpressionBase source)
        {
            if (Line != 0)
            {
                source.Line = Line;
                source.Column = Column;
                source.EndLine = EndLine;
                source.EndColumn = EndColumn;
            }
        }

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
            tokenizer.SkipWhitespace();
            while (tokenizer.Match("//"))
            {
                var expressionTokenizer = tokenizer as ExpressionTokenizer;
                if (expressionTokenizer != null)
                {
                    int line = tokenizer.Line;
                    int column = tokenizer.Column - 2;

                    var comment = tokenizer.ReadTo('\n');
                    if (comment.Length > 0 && comment[comment.Length - 1] == '\r')
                        comment = comment.SubToken(0, comment.Length - 1);

                    expressionTokenizer.AddComment(new CommentExpression("//" + comment.ToString()) { Line = line, Column = column, EndLine = line, EndColumn = column + comment.Length + 1 });
                }
                else
                {
                    tokenizer.ReadTo('\n');
                }

                tokenizer.SkipWhitespace();
            }
        }

        internal static ParseErrorExpression ParseError(PositionalTokenizer tokenizer, string message, int line, int column)
        {
            var error = new ParseErrorExpression(message, line, column, tokenizer.Line, tokenizer.Column);

            var expressionTokenizer = tokenizer as ExpressionTokenizer;
            if (expressionTokenizer != null)
                expressionTokenizer.AddError(error);

            return error;
        }

        internal static ParseErrorExpression ParseError(PositionalTokenizer tokenizer, string message)
        {
            return ParseError(tokenizer, message, tokenizer.Line, tokenizer.Column);
        }

        internal static ParseErrorExpression ParseError(PositionalTokenizer tokenizer, string message, ExpressionBase expression)
        {
            var error = ParseError(tokenizer, message);
            error.Line = expression.Line;
            error.Column = expression.Column;
            error.EndLine = expression.EndLine;
            error.EndColumn = expression.EndColumn;
            return error;
        }

        /// <summary>
        /// Gets the next expression from the input.
        /// </summary>
        /// <returns>The next expression, <c>null</c> if at end of file.</returns>
        public static ExpressionBase Parse(PositionalTokenizer tokenizer)
        {
            var expressionTokenizer = tokenizer as ExpressionTokenizer;
            if (expressionTokenizer != null)
            {
                var queuedExpression = expressionTokenizer.DequeueExpression();
                if (queuedExpression != null)
                    return queuedExpression;
            }

            SkipWhitespace(tokenizer);

            if (tokenizer.NextChar == '\0')
                return new ParseErrorExpression("Unexpected end of script", tokenizer.Line, tokenizer.Column, tokenizer.Line, tokenizer.Column);

            var clause = ExpressionBase.ParseClause(tokenizer);
            if (clause.Type == ExpressionType.ParseError || clause.Type == ExpressionType.Comment)
                return clause;

            var clauseEndLine = tokenizer.Line;
            var clauseEndColumn = tokenizer.Column;

            SkipWhitespace(tokenizer);

            var joinerLine = tokenizer.Line;
            var joinerColumn = tokenizer.Column;

            switch (tokenizer.NextChar)
            {
                case '+':
                    tokenizer.Advance();
                    clause = ParseMathematic(tokenizer, clause, MathematicOperation.Add, joinerLine, joinerColumn);
                    break;

                case '-':
                    tokenizer.Advance();
                    clause = ParseMathematic(tokenizer, clause, MathematicOperation.Subtract, joinerLine, joinerColumn);
                    break;

                case '*':
                    tokenizer.Advance();
                    clause = ParseMathematic(tokenizer, clause, MathematicOperation.Multiply, joinerLine, joinerColumn);
                    break;

                case '/':
                    tokenizer.Advance();
                    clause = ParseMathematic(tokenizer, clause, MathematicOperation.Divide, joinerLine, joinerColumn);
                    break;

                case '%':
                    tokenizer.Advance();
                    clause = ParseMathematic(tokenizer, clause, MathematicOperation.Modulus, joinerLine, joinerColumn);
                    break;

                case '=':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        clause = ParseComparison(tokenizer, clause, ComparisonOperation.Equal, joinerLine, joinerColumn);
                    }
                    else
                    {
                        clause = ParseAssignment(tokenizer, clause, joinerLine, joinerColumn);
                    }
                    break;

                case '!':
                    tokenizer.Advance();
                    if (tokenizer.NextChar != '=')
                    {
                        ParseError(tokenizer, "= expected following !", joinerLine, joinerColumn);
                    }
                    else
                    {
                        tokenizer.Advance();
                        clause = ParseComparison(tokenizer, clause, ComparisonOperation.NotEqual, joinerLine, joinerColumn);
                    }
                    break;

                case '<':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        clause = ParseComparison(tokenizer, clause, ComparisonOperation.LessThanOrEqual, joinerLine, joinerColumn);
                    }
                    else
                    {
                        clause = ParseComparison(tokenizer, clause, ComparisonOperation.LessThan, joinerLine, joinerColumn);
                    }
                    break;

                case '>':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        clause = ParseComparison(tokenizer, clause, ComparisonOperation.GreaterThanOrEqual, joinerLine, joinerColumn);
                    }
                    else
                    {
                        clause = ParseComparison(tokenizer, clause, ComparisonOperation.GreaterThan, joinerLine, joinerColumn);
                    }
                    break;

                case '&':
                    tokenizer.Advance();
                    if (tokenizer.NextChar != '&')
                    {
                        ParseError(tokenizer, "& expected following &", joinerLine, joinerColumn);
                    }
                    else
                    {
                        tokenizer.Advance();
                        clause = ParseConditional(tokenizer, clause, ConditionalOperation.And, joinerLine, joinerColumn);
                        if (clause.Type == ExpressionType.ParseError)
                            return clause;
                    }
                    break;

                case '|':
                    tokenizer.Advance();
                    if (tokenizer.NextChar != '|')
                    {
                        ParseError(tokenizer, "| expected following |", joinerLine, joinerColumn);
                    }
                    else
                    {
                        tokenizer.Advance();
                        clause = ParseConditional(tokenizer, clause, ConditionalOperation.Or, joinerLine, joinerColumn);
                        if (clause.Type == ExpressionType.ParseError)
                            return clause;
                    }
                    break;

                default:
                    if (clause.EndColumn == 0)
                    {
                        clause.EndLine = clauseEndLine;
                        clause.EndColumn = clauseEndColumn;
                    }
                    return clause;
            }

            clause = clause.Rebalance();

            Debug.Assert(clause.Line != 0);
            Debug.Assert(clause.Column != 0);
            Debug.Assert(clause.EndLine != 0);
            Debug.Assert(clause.EndColumn != 0);

            return clause;
        }

        internal void AdjustLines(int amount)
        {
            Line += amount;
            EndLine += amount;
        }

        protected static ExpressionBase ParseClause(PositionalTokenizer tokenizer)
        {
            var line = tokenizer.Line;
            var column = tokenizer.Column;

            var clause = ParseClauseCore(tokenizer);

            if (clause.Column == 0)
            {
                clause.Line = line;
                clause.Column = column;
            }

            if (clause.EndColumn == 0)
            {
                clause.EndLine = tokenizer.Line;
                clause.EndColumn = (tokenizer.Column > 1) ? tokenizer.Column - 1 : 1;
            }

            return clause;
        }

        private static ExpressionBase ParseClauseCore(PositionalTokenizer tokenizer)
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
                            return ParseError(tokenizer, "No closing parenthesis found");

                        return ParseError(tokenizer, "Expected closing parenthesis, found: " + tokenizer.NextChar);
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
                        return ParseError(tokenizer, ex.Message);
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
                            return new IntegerConstantExpression(-hexValue);
                        }

                        var number = tokenizer.ReadNumber();
                        int value;
                        Int32.TryParse(number.ToString(), out value);
                        return new IntegerConstantExpression(-value);
                    }
                    return ParseError(tokenizer, "Minus without value");

                case '{':
                    tokenizer.Advance();
                    return DictionaryExpression.Parse(tokenizer);

                case '[':
                    tokenizer.Advance();
                    return ParseArray(tokenizer);

                default:
                    var line = tokenizer.Line;
                    var column = tokenizer.Column;
                    var identifier = tokenizer.ReadIdentifier();
                    if (identifier.IsEmpty)
                    {
                        var error = ParseError(tokenizer, "Unexpected character: "+ tokenizer.NextChar);
                        tokenizer.Advance();
                        return error;
                    }

                    SkipWhitespace(tokenizer);

                    if (identifier == "return")
                    {
                        clause = ExpressionBase.Parse(tokenizer);
                        if (clause.Type == ExpressionType.ParseError)
                            return clause;

                        return new ReturnExpression(new KeywordExpression(identifier.ToString(), line, column), clause);
                    }

                    if (identifier == "function")
                        return FunctionDefinitionExpression.Parse(tokenizer, line, column);
                    if (identifier == "for")
                        return ForExpression.Parse(tokenizer, line, column);
                    if (identifier == "if")
                        return IfExpression.Parse(tokenizer, line, column);

                    if (tokenizer.NextChar == '(')
                    {
                        tokenizer.Advance();

                        var parameters = new List<ExpressionBase>();
                        ParseParameters(tokenizer, parameters);

                        var functionCall = new FunctionCallExpression(new FunctionNameExpression(identifier.ToString(), line, column), parameters);
                        functionCall.EndLine = tokenizer.Line;
                        functionCall.EndColumn = tokenizer.Column - 1;
                        return functionCall;
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
                                return ParseError(tokenizer, "Expecting closing bracket after index");
                            tokenizer.Advance();
                            SkipWhitespace(tokenizer);

                            if (parent != null)
                                parent = new IndexedVariableExpression(parent, index);
                            else
                                parent = new IndexedVariableExpression(new VariableExpression(identifier.ToString(), line, column), index);

                        } while (tokenizer.NextChar == '[');

                        return parent;
                    }

                    return new VariableExpression(identifier.ToString(), line, column);
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
                        return ParseError(tokenizer, "Invalid expression", parameter);

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
                return ParseError(tokenizer, "No closing parenthesis found", line, column);

            return ParseError(tokenizer, "Expected closing parenthesis, found: " + tokenizer.NextChar);
        }

        private static ExpressionBase ParseMathematic(PositionalTokenizer tokenizer, ExpressionBase left, MathematicOperation operation, int joinerLine, int joinerColumn)
        {
            var right = ExpressionBase.Parse(tokenizer);
            switch (right.Type)
            {
                case ExpressionType.ParseError:
                    return right;

                case ExpressionType.Comparison:
                case ExpressionType.Conditional:
                case ExpressionType.Dictionary:
                case ExpressionType.FunctionCall:
                case ExpressionType.IntegerConstant:
                case ExpressionType.Mathematic:
                case ExpressionType.StringConstant:
                case ExpressionType.Variable:
                    break;

                default:
                    var expressionTokenizer = tokenizer as ExpressionTokenizer;
                    if (expressionTokenizer != null)
                        expressionTokenizer.QueueExpression(right);

                    right = new KeywordExpression(MathematicExpression.GetOperatorCharacter(operation).ToString(), joinerLine, joinerColumn);
                    ParseError(tokenizer, "incompatible mathematical operation", right);
                    break;
            }

            return new MathematicExpression(left, operation, right);
        }

        private static ExpressionBase ParseComparison(PositionalTokenizer tokenizer, ExpressionBase left, ComparisonOperation operation, int joinerLine, int joinerColumn)
        {
            var right = ExpressionBase.Parse(tokenizer);
            switch (right.Type)
            {
                case ExpressionType.ParseError:
                    return right;

                case ExpressionType.Conditional: // will be rebalanced
                case ExpressionType.FunctionCall:
                case ExpressionType.IntegerConstant:
                case ExpressionType.Mathematic:
                case ExpressionType.StringConstant:
                case ExpressionType.Variable:
                    break;

                default:
                    var expressionTokenizer = tokenizer as ExpressionTokenizer;
                    if (expressionTokenizer != null)
                        expressionTokenizer.QueueExpression(right);

                    right = new KeywordExpression(ComparisonExpression.GetOperatorString(operation), joinerLine, joinerColumn);
                    ParseError(tokenizer, "incompatible comparison", right);
                    break;
            }

            return new ComparisonExpression(left, operation, right);
        }

        private static ExpressionBase ParseConditional(PositionalTokenizer tokenizer, ExpressionBase left, ConditionalOperation operation, int joinerLine, int joinerColumn)
        {
            var right = ExpressionBase.Parse(tokenizer);

            switch (right.Type)
            {
                case ExpressionType.ParseError:
                    return right;

                case ExpressionType.Comparison:
                case ExpressionType.Conditional:
                case ExpressionType.FunctionCall:
                case ExpressionType.Variable:
                    break;

                default:
                    var expressionTokenizer = tokenizer as ExpressionTokenizer;
                    if (expressionTokenizer != null)
                        expressionTokenizer.QueueExpression(right);

                    right = new KeywordExpression(ConditionalExpression.GetOperatorString(operation), joinerLine, joinerColumn);
                    ParseError(tokenizer, "incompatible logical condition", right);
                    break;
            }

            return new ConditionalExpression(left, operation, right);
        }

        private static ExpressionBase ParseAssignment(PositionalTokenizer tokenizer, ExpressionBase variable, int joinerLine, int joinerColumn)
        {
            if (variable.Type != ExpressionType.Variable)
                return ParseError(tokenizer, "Cannot assign value to non-variable", variable);

            var value = ExpressionBase.Parse(tokenizer);
            switch (value.Type)
            {
                case ExpressionType.ParseError:
                    return value;

                case ExpressionType.Array:
                case ExpressionType.Comparison:
                case ExpressionType.Conditional:
                case ExpressionType.Dictionary:
                case ExpressionType.FunctionCall:
                case ExpressionType.IntegerConstant:
                case ExpressionType.Mathematic:
                case ExpressionType.StringConstant:
                case ExpressionType.Variable:
                    break;

                default:
                    var expressionTokenizer = tokenizer as ExpressionTokenizer;
                    if (expressionTokenizer != null)
                        expressionTokenizer.QueueExpression(value);

                    value = new KeywordExpression("=", joinerLine, joinerColumn);
                    ParseError(tokenizer, "incompatible assignment", value);
                    break;
            }

            return new AssignmentExpression((VariableExpression)variable, value);
        }

        private static ExpressionBase ParseArray(PositionalTokenizer tokenizer)
        {
            SkipWhitespace(tokenizer);

            var array = new ArrayExpression();
            while (tokenizer.NextChar != ']')
            {
                var value = Parse(tokenizer);
                if (value.Type == ExpressionType.ParseError)
                    return value;

                array.Entries.Add(value);

                SkipWhitespace(tokenizer);
                if (tokenizer.NextChar == ']')
                    break;

                if (tokenizer.NextChar != ',')
                    return ParseError(tokenizer, "Expecting comma between entries");
                tokenizer.Advance();
                SkipWhitespace(tokenizer);
            }

            tokenizer.Advance();
            return array;
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
                        return ParseError(tokenizer, "No matching closing brace found", line, column);

                    var statement = ExpressionBase.Parse(tokenizer);
                    if (statement.Type == ExpressionType.ParseError)
                        return statement;

                    if (statement.Type == ExpressionType.Variable)
                        return new ParseErrorExpression("standalone variable has no meaning", statement);

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

        protected static bool ExpressionsEqual(IEnumerable<ExpressionBase> left, IEnumerable<ExpressionBase> right)
        {
            var leftEnumerator = left.GetEnumerator();
            var rightEnumerator = right.GetEnumerator();

            while (leftEnumerator.MoveNext())
            {
                if (!rightEnumerator.MoveNext())
                    return false;

                if (leftEnumerator.Current != rightEnumerator.Current)
                    return false;
            }

            return (!rightEnumerator.MoveNext());
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
        /// An array.
        /// </summary>
        Array,

        /// <summary>
        /// A for loop.
        /// </summary>
        For,

        /// <summary>
        /// An if statement.
        /// </summary>
        If,

        /// <summary>
        /// A comment.
        /// </summary>
        Comment,

        /// <summary>
        /// A keyword.
        /// </summary>
        Keyword,

        /// <summary>
        /// A parse error.
        /// </summary>
        ParseError,
    }
}
