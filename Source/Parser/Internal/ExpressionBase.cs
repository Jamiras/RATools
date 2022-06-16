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
        /// Gets the location of the expression within the document
        /// </summary>
        public TextRange Location { get; protected set; }

        /// <summary>
        /// Gets whether or not an expression has been marked as immutable.
        /// </summary>
        /// <remarks>
        /// Used to prevent relocating expressions (via <see cref="CopyLocation"/>) when they're returned
        /// by <see cref="ReplaceVariables"/> without being copied.
        /// </remarks>
        internal bool IsReadOnly { get; private set; }

        internal ExpressionBase MakeReadOnly(ExpressionBase expression)
        {
            expression.IsReadOnly = true;

            var nested = expression as INestedExpressions;
            if (nested != null)
            {
                foreach (var nestedExpression in nested.NestedExpressions)
                    MakeReadOnly(nestedExpression);
            }

            return expression;
        }

        /// <summary>
        /// Copies the location of this expression into another expression.
        /// </summary>
        internal void CopyLocation(ExpressionBase target)
        {
            if (!Location.IsEmpty && !target.IsReadOnly)
                target.Location = Location;
        }

        /// <summary>
        /// Gets or sets whether this is a logical unit.
        /// </summary>
        /// <remarks>
        /// If <c>true</c>, this element was wrapped in parenthesis and cannot be broken by rebalancing.
        /// </remarks>
        public bool IsLogicalUnit { get; set; }

        /// <summary>
        /// Gets whether this is non-changing.
        /// </summary>
        public virtual bool IsConstant
        {
            get { return false; }
        }

        /// <summary>
        /// Gets whether this is a compile-time constant.
        /// </summary>
        public virtual bool IsLiteralConstant
        {
            get { return false; }
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder"/>.
        /// </summary>
        /// <remarks>Used for constructing a StringConstantExpression from smaller expressions.</remarks>
        internal virtual void AppendStringLiteral(StringBuilder builder)
        {
            AppendString(builder);
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

                    expressionTokenizer.AddComment(new CommentExpression("//" + comment.ToString()) 
                    {
                        Location = new TextRange(line, column, line, column + comment.Length + 1)
                    });
                }
                else
                {
                    tokenizer.ReadTo('\n');
                }

                tokenizer.SkipWhitespace();
            }
        }

        internal static ErrorExpression ParseError(PositionalTokenizer tokenizer, string message, int line, int column)
        {
            var error = new ErrorExpression(message, line, column, tokenizer.Line, tokenizer.Column);

            var expressionTokenizer = tokenizer as ExpressionTokenizer;
            if (expressionTokenizer != null)
                expressionTokenizer.AddError(error);

            return error;
        }

        internal static ErrorExpression ParseError(PositionalTokenizer tokenizer, string message)
        {
            return ParseError(tokenizer, message, tokenizer.Line, tokenizer.Column);
        }

        internal static ErrorExpression ParseError(PositionalTokenizer tokenizer, string message, ExpressionBase expression)
        {
            var error = ParseError(tokenizer, message);
            error.InnerError = expression as ErrorExpression;
            expression.CopyLocation(error);
            return error;
        }

        // logical evalation happens from highest priority to lowest
        private enum OperationPriority
        {
            None = 0,
            Assign,
            Or,
            And,
            Compare,
            BitwiseAnd,
            AppendString,
            AddSubtract,
            MulDivMod,
            Not,
            Parenthesis,
        }

        /// <summary>
        /// Gets the next expression from the input.
        /// </summary>
        /// <returns>The next expression, <c>null</c> if at end of file.</returns>
        public static ExpressionBase Parse(PositionalTokenizer tokenizer)
        {
            return ParseExpression(tokenizer, OperationPriority.None);
        }

        private static ExpressionBase ParseExpression(PositionalTokenizer tokenizer, OperationPriority priority)
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
                return ParseError(tokenizer, "Unexpected end of script");

            var clause = ParseClause(tokenizer);
            if (clause.Type == ExpressionType.Error || clause.Type == ExpressionType.Comment)
                return clause;

            clause = ParseClauseExtension(clause, tokenizer, priority);
            if (clause.Type == ExpressionType.Error)
                return clause;

            Debug.Assert(clause.Location.Start.Line != 0);
            Debug.Assert(clause.Location.Start.Column != 0);
            Debug.Assert(clause.Location.End.Line != 0);
            Debug.Assert(clause.Location.End.Column != 0);

            return clause;
        }

        private static ExpressionBase ParseClauseExtension(ExpressionBase clause, PositionalTokenizer tokenizer, OperationPriority priority)
        {
            do
            {
                var clauseEndLine = tokenizer.Line;
                var clauseEndColumn = tokenizer.Column;

                SkipWhitespace(tokenizer);

                var joinerLine = tokenizer.Line;
                var joinerColumn = tokenizer.Column;

                switch (tokenizer.NextChar)
                {
                    case '+':
                        if (priority == OperationPriority.AddSubtract && clause.Type == ExpressionType.StringConstant)
                            priority = OperationPriority.AppendString;

                        if (priority >= OperationPriority.AddSubtract)
                            return clause;

                        tokenizer.Advance();
                        clause = ParseMathematic(tokenizer, clause, MathematicOperation.Add, joinerLine, joinerColumn);
                        break;

                    case '-':
                        if (priority >= OperationPriority.AddSubtract)
                            return clause;

                        tokenizer.Advance();
                        clause = ParseMathematic(tokenizer, clause, MathematicOperation.Subtract, joinerLine, joinerColumn);
                        break;

                    case '*':
                        if (priority >= OperationPriority.MulDivMod)
                            return clause;

                        tokenizer.Advance();
                        clause = ParseMathematic(tokenizer, clause, MathematicOperation.Multiply, joinerLine, joinerColumn);
                        break;

                    case '/':
                        if (priority >= OperationPriority.MulDivMod)
                            return clause;

                        tokenizer.Advance();
                        clause = ParseMathematic(tokenizer, clause, MathematicOperation.Divide, joinerLine, joinerColumn);
                        break;

                    case '%':
                        if (priority >= OperationPriority.MulDivMod)
                            return clause;

                        tokenizer.Advance();
                        clause = ParseMathematic(tokenizer, clause, MathematicOperation.Modulus, joinerLine, joinerColumn);
                        break;

                    case '=':
                        if (tokenizer.MatchSubstring("==") == 2)
                        {
                            if (priority >= OperationPriority.Compare)
                                return clause;

                            tokenizer.Advance(2);
                            clause = ParseComparison(tokenizer, clause, ComparisonOperation.Equal, joinerLine, joinerColumn);
                        }
                        else
                        {
                            if (priority > OperationPriority.Assign)
                                return clause;

                            tokenizer.Advance();
                            if (tokenizer.NextChar == '>')
                            {
                                tokenizer.Advance();
                                clause = AnonymousUserFunctionDefinitionExpression.ParseAnonymous(tokenizer, clause);
                            }
                            else
                            {
                                clause = ParseAssignment(tokenizer, clause, joinerLine, joinerColumn);
                            }
                        }
                        break;

                    case '!':
                        if (priority >= OperationPriority.Compare)
                            return clause;

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
                        if (priority >= OperationPriority.Compare)
                            return clause;

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
                        if (priority >= OperationPriority.Compare)
                            return clause;

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
                        if (tokenizer.MatchSubstring("&&") == 2)
                        {
                            if (priority >= OperationPriority.And)
                                return clause;

                            tokenizer.Advance(2);
                            clause = ParseConditional(tokenizer, clause, ConditionalOperation.And, joinerLine, joinerColumn);
                        }
                        else
                        {
                            if (priority >= OperationPriority.BitwiseAnd)
                                return clause;

                            tokenizer.Advance();
                            clause = ParseMathematic(tokenizer, clause, MathematicOperation.BitwiseAnd, joinerLine, joinerColumn);
                        }
                        break;

                    case '|':
                        if (priority >= OperationPriority.Or)
                            return clause;

                        tokenizer.Advance();
                        if (tokenizer.NextChar != '|')
                        {
                            ParseError(tokenizer, "| expected following |", joinerLine, joinerColumn);
                        }
                        else
                        {
                            tokenizer.Advance();
                            clause = ParseConditional(tokenizer, clause, ConditionalOperation.Or, joinerLine, joinerColumn);
                        }
                        break;

                    default:
                        if (clause.Location.End.Column == 0)
                            clause.Location = new TextRange(clause.Location.Start, new TextLocation(clauseEndLine, clauseEndColumn));

                        return clause;
                }

                if (clause.Type == ExpressionType.Error)
                    return clause;

            } while (true);
        }

        internal void AdjustLines(int amount)
        {
            Location = new TextRange(Location.Start.Line + amount, Location.Start.Column, Location.End.Line + amount, Location.End.Column);
        }

        protected static ExpressionBase ParseClause(PositionalTokenizer tokenizer)
        {
            var start = tokenizer.Location;

            var clause = ParseClauseCore(tokenizer);

            var end = clause.Location.End;
            if (end.Column == 0 || clause.Location.Start.Column == 0)
            {
                if (clause.Location.Start.Column != 0)
                    start = clause.Location.Start;

                if (end.Line == 0)
                    end = new TextLocation(tokenizer.Line, (tokenizer.Column > 1) ? tokenizer.Column - 1 : 1);

                clause.Location = new TextRange(start, end);
            }

            return clause;
        }

        private static ExpressionBase ParseNumber(PositionalTokenizer tokenizer, bool isUnsigned)
        {
            int line = tokenizer.Line;
            int column = tokenizer.Column;
            int endLine = line;
            int endColumn = column;
            uint value;
            string number = "";

            if (tokenizer.Match("0x"))
            {
                while (Char.IsDigit(tokenizer.NextChar) || (tokenizer.NextChar >= 'A' && tokenizer.NextChar <= 'F') || (tokenizer.NextChar >= 'a' && tokenizer.NextChar <= 'f'))
                {
                    number += tokenizer.NextChar;

                    endLine = tokenizer.Line;
                    endColumn = tokenizer.Column;
                    tokenizer.Advance();
                }

                if (!UInt32.TryParse(number, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.CurrentCulture, out value))
                    return new ErrorExpression("Number too large");
            }
            else
            {
                while (Char.IsDigit(tokenizer.NextChar))
                {
                    number += tokenizer.NextChar;

                    endLine = tokenizer.Line;
                    endColumn = tokenizer.Column;
                    tokenizer.Advance();
                }

                if (tokenizer.NextChar == '.')
                {
                    number += tokenizer.NextChar;
                    tokenizer.Advance();

                    while (Char.IsDigit(tokenizer.NextChar))
                    {
                        number += tokenizer.NextChar;

                        endLine = tokenizer.Line;
                        endColumn = tokenizer.Column;
                        tokenizer.Advance();
                    }

                    float floatValue;
                    if (!float.TryParse(number, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out floatValue))
                        return new ErrorExpression("Number too large");

                    var floatExpression = new FloatConstantExpression(floatValue);
                    floatExpression.Location = new TextRange(line, column, endLine, endColumn);
                    return floatExpression;
                }

                if (!UInt32.TryParse(number, out value))
                    return new ErrorExpression("Number too large");
            }

            if (value > Int32.MaxValue && !isUnsigned)
                return new ErrorExpression("Number too large");

            var integerExpression = new IntegerConstantExpression((int)value);
            integerExpression.Location = new TextRange(line, column, endLine, endColumn);
            return integerExpression;
        }

        private static ExpressionBase ParseClauseCore(PositionalTokenizer tokenizer)
        { 
            ExpressionBase clause;

            switch (tokenizer.NextChar)
            {
                case '!':
                    tokenizer.Advance();
                    clause = ParseClause(tokenizer);
                    if (clause.Type == ExpressionType.Error)
                        return clause;

                    return new ConditionalExpression(null, ConditionalOperation.Not, clause);

                case '(':
                    if (AnonymousUserFunctionDefinitionExpression.IsAnonymousParameterList(tokenizer))
                        return AnonymousUserFunctionDefinitionExpression.ParseAnonymous(tokenizer);

                    tokenizer.Advance();
                    clause = ExpressionBase.Parse(tokenizer);
                    if (clause.Type == ExpressionType.Error)
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
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    return ParseNumber(tokenizer, true);

                case '-':
                    var tokenStart = tokenizer.Location;
                    tokenizer.Advance();
                    if (tokenizer.NextChar >= '0' && tokenizer.NextChar <= '9')
                    {
                        var result = ParseNumber(tokenizer, false);
                        var tokenEnd = result.Location.End;
                        switch (result.Type)
                        {
                            case ExpressionType.IntegerConstant:
                                result = new IntegerConstantExpression(-((IntegerConstantExpression)result).Value);
                                break;

                            case ExpressionType.FloatConstant:
                                result = new FloatConstantExpression(-((FloatConstantExpression)result).Value);
                                break;

                            default:
                                return result;
                        }

                        result.Location = new TextRange(tokenStart, tokenEnd);
                        return result;
                    }
                    return ParseError(tokenizer, "Minus without value", tokenStart.Line, tokenStart.Column);

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
                        if (clause.Type == ExpressionType.Error)
                            return clause;

                        return new ReturnExpression(new KeywordExpression(identifier.ToString(), line, column), clause);
                    }

                    if (identifier == "function")
                        return UserFunctionDefinitionExpression.Parse(tokenizer, line, column);
                    if (identifier == "for")
                        return ForExpression.Parse(tokenizer, line, column);
                    if (identifier == "if")
                        return IfExpression.Parse(tokenizer, line, column);

                    if (identifier == "true")
                        return new BooleanConstantExpression(true, line, column);
                    if (identifier == "false")
                        return new BooleanConstantExpression(false, line, column);

                    if (tokenizer.NextChar == '(')
                    {
                        tokenizer.Advance();

                        var parameters = new List<ExpressionBase>();
                        ParseParameters(tokenizer, parameters);

                        var functionCall = new FunctionCallExpression(new FunctionNameExpression(identifier.ToString(), line, column), parameters);
                        functionCall.Location = new TextRange(line, column, tokenizer.Line, tokenizer.Column - 1);
                        return functionCall;
                    }

                    if (tokenizer.NextChar == '[')
                    {
                        IndexedVariableExpression parent = null;

                        do
                        {
                            tokenizer.Advance();

                            var index = ExpressionBase.Parse(tokenizer);
                            if (index.Type == ExpressionType.Error)
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
                    if (parameter.Type == ExpressionType.Error)
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
            OperationPriority priority;
            switch (operation)
            {
                case MathematicOperation.Add:
                    if (left is StringConstantExpression)
                    {
                        priority = OperationPriority.AppendString;
                        break;
                    }
                    priority = OperationPriority.AddSubtract;
                    break;

                case MathematicOperation.Subtract:
                    priority = OperationPriority.AddSubtract;
                    break;

                case MathematicOperation.Multiply:
                case MathematicOperation.Divide:
                case MathematicOperation.Modulus:
                    priority = OperationPriority.MulDivMod;
                    break;

                case MathematicOperation.BitwiseAnd:
                    priority = OperationPriority.BitwiseAnd;
                    break;

                default:
                    return new ErrorExpression("Unknown operator: " + operation);
            }

            var right = ParseExpression(tokenizer, priority);
            switch (right.Type)
            {
                case ExpressionType.Error:
                    return right;

                case ExpressionType.Comparison: // will be rebalanced
                case ExpressionType.Conditional: // will be rebalanced
                case ExpressionType.FloatConstant:
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
                    return ParseError(tokenizer, "Incompatible mathematical operation", right);
            }

            if (priority == OperationPriority.AddSubtract)
            {
                var mathematicRight = right as MathematicExpression;

            }

            return new MathematicExpression(left, operation, right);
        }

        private static ExpressionBase ParseComparison(PositionalTokenizer tokenizer, ExpressionBase left, ComparisonOperation operation, int joinerLine, int joinerColumn)
        {
            var right = ParseExpression(tokenizer, OperationPriority.Compare);
            switch (right.Type)
            {
                case ExpressionType.Error:
                    return right;

                case ExpressionType.BooleanConstant:
                case ExpressionType.Conditional: // will be rebalanced
                case ExpressionType.FloatConstant:
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
                    return ParseError(tokenizer, "Incompatible comparison", right);
            }

            return new ComparisonExpression(left, operation, right);
        }

        private static ExpressionBase ParseConditional(PositionalTokenizer tokenizer, ExpressionBase left, ConditionalOperation operation, int joinerLine, int joinerColumn)
        {
            OperationPriority priority;
            switch (operation)
            {
                case ConditionalOperation.And:
                    priority = OperationPriority.And;
                    break;
                case ConditionalOperation.Or:
                    priority = OperationPriority.Or;
                    break;
                case ConditionalOperation.Not:
                    priority = OperationPriority.Not;
                    break;
                default:
                    return new ErrorExpression("Unknown operation: " + operation);
            }

            var right = ParseExpression(tokenizer, priority);

            switch (right.Type)
            {
                case ExpressionType.Error:
                    return right;

                case ExpressionType.BooleanConstant:
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
                    return ParseError(tokenizer, "Incompatible logical condition", right);
            }

            return new ConditionalExpression(left, operation, right);
        }

        private static ExpressionBase ParseAssignment(PositionalTokenizer tokenizer, ExpressionBase variable, int joinerLine, int joinerColumn)
        {
            if (variable.Type != ExpressionType.Variable)
                return ParseError(tokenizer, "Cannot assign value to non-variable", variable);

            var value = ParseExpression(tokenizer, OperationPriority.Assign);
            switch (value.Type)
            {
                case ExpressionType.Error:
                    value = new KeywordExpression("=", joinerLine, joinerColumn);
                    break;

                case ExpressionType.Array:
                case ExpressionType.BooleanConstant:
                case ExpressionType.Comparison:
                case ExpressionType.Conditional:
                case ExpressionType.Dictionary:
                case ExpressionType.FloatConstant:
                case ExpressionType.FunctionCall:
                case ExpressionType.FunctionDefinition:
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
                    return ParseError(tokenizer, "Incompatible assignment", value);
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
                if (value.Type == ExpressionType.Error)
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
                if (statement.Type == ExpressionType.Error)
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
                    if (statement.Type == ExpressionType.Error)
                        return statement;

                    if (statement.Type == ExpressionType.Variable)
                        return new ErrorExpression("standalone variable has no meaning", statement);

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
        /// <returns><c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result"/> will likely be a <see cref="ErrorExpression"/>.</returns>
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
        public virtual bool? IsTrue(InterpreterScope scope, out ErrorExpression error)
        {
            error = null;
            return null;
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
        /// A variable definition.
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
        /// A boolean constant.
        /// </summary>
        BooleanConstant,

        /// <summary>
        /// A floating point constant.
        /// </summary>
        FloatConstant,

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
        /// An error.
        /// </summary>
        Error,

        /// <summary>
        /// A reference to a variable.
        /// </summary>
        VariableReference,
    }
}
