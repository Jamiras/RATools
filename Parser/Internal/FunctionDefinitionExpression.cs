using Jamiras.Components;
using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class FunctionDefinitionExpression : ExpressionBase, INestedExpressions
    {
        public FunctionDefinitionExpression(string name)
            : this()
        {
            Name = new VariableExpression(name);
        }

        private FunctionDefinitionExpression()
            : base(ExpressionType.FunctionDefinition)
        {
            Parameters = new List<VariableExpression>();
            Expressions = new List<ExpressionBase>();
            DefaultParameters = new TinyDictionary<string, ExpressionBase>();
        }

        /// <summary>
        /// Gets the name of the function.
        /// </summary>
        public VariableExpression Name { get; private set; }

        private KeywordExpression _keyword;

        /// <summary>
        /// Gets the names of the parameters.
        /// </summary>
        public ICollection<VariableExpression> Parameters { get; private set; }

        /// <summary>
        /// Gets default values for the parameters.
        /// </summary>
        public IDictionary<string, ExpressionBase> DefaultParameters { get; private set; }

        /// <summary>
        /// Gets the expressions for the contents of the function.
        /// </summary>
        public ICollection<ExpressionBase> Expressions { get; private set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            AppendString(builder);
            return builder.ToString();
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("function ");
            Name.AppendString(builder);
            builder.Append('(');

            if (Parameters.Count > 0)
            {
                foreach (var parameter in Parameters)
                {
                    parameter.AppendString(builder);
                    builder.Append(", ");
                }
                builder.Length -= 2;
            }

            builder.Append(')');
        }

        /// <summary>
        /// Parses a function definition.
        /// </summary>
        /// <remarks>
        /// Assumes the 'function' keyword has already been consumed.
        /// </remarks>
        internal static ExpressionBase Parse(PositionalTokenizer tokenizer, int line = 0, int column = 0)
        {
            var function = new FunctionDefinitionExpression();
            function._keyword = new KeywordExpression("function", line, column);

            ExpressionBase.SkipWhitespace(tokenizer);

            line = tokenizer.Line;
            column = tokenizer.Column;

            var functionName = tokenizer.ReadIdentifier();
            if (functionName.IsEmpty)
            {
                ExpressionBase.ParseError(tokenizer, "Invalid function name");
                return function;
            }
            function.Name = new VariableExpression(functionName.ToString(), line, column);

            ExpressionBase.SkipWhitespace(tokenizer);
            if (tokenizer.NextChar != '(')
            {
                ExpressionBase.ParseError(tokenizer, "Expected '(' after function name", function.Name);
                return function;
            }
            tokenizer.Advance();

            ExpressionBase.SkipWhitespace(tokenizer);
            if (tokenizer.NextChar != ')')
            {
                do
                {
                    line = tokenizer.Line;
                    column = tokenizer.Column;

                    var parameter = tokenizer.ReadIdentifier();
                    if (parameter.IsEmpty)
                    {
                        ExpressionBase.ParseError(tokenizer, "Invalid parameter name", line, column);
                        return function;
                    }

                    function.Parameters.Add(new VariableExpression(parameter.ToString(), line, column));

                    ExpressionBase.SkipWhitespace(tokenizer);
                    if (tokenizer.NextChar == ')')
                        break;

                    if (tokenizer.NextChar != ',')
                    {
                        ExpressionBase.ParseError(tokenizer, "Expected ',' or ')' after parameter name, found: " + tokenizer.NextChar);
                        return function;
                    }

                    tokenizer.Advance();
                    ExpressionBase.SkipWhitespace(tokenizer);
                } while (true);
            }

            tokenizer.Advance(); // closing parenthesis
            ExpressionBase.SkipWhitespace(tokenizer);

            ExpressionBase expression;

            if (tokenizer.Match("=>"))
            {
                ExpressionBase.SkipWhitespace(tokenizer);

                expression = ExpressionBase.Parse(tokenizer);
                if (expression.Type == ExpressionType.ParseError)
                    return expression;

                if (expression.Type == ExpressionType.Return)
                    return new ParseErrorExpression("Return statement is implied by =>", ((ReturnExpression)expression).Keyword);

                var returnExpression = new ReturnExpression(expression);
                function.Expressions.Add(returnExpression);
                return function;
            }

            if (tokenizer.NextChar != '{')
            {
                ExpressionBase.ParseError(tokenizer, "Expected '{' after function declaration", function.Name);
                return function;
            }

            line = tokenizer.Line;
            column = tokenizer.Column;
            tokenizer.Advance();
            ExpressionBase.SkipWhitespace(tokenizer);

            bool seenReturn = false;
            while (tokenizer.NextChar != '}')
            {
                expression = ExpressionBase.Parse(tokenizer);
                if (expression.Type == ExpressionType.ParseError)
                    return function;

                if (expression.Type == ExpressionType.Return)
                    seenReturn = true;
                else if (seenReturn)
                    ExpressionBase.ParseError(tokenizer, "Expression after return statement", expression);

                function.Expressions.Add(expression);

                ExpressionBase.SkipWhitespace(tokenizer);
            }

            tokenizer.Advance();
            return function;
        }

        /// <summary>
        /// Determines whether the specified <see cref="FunctionDefinitionExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="FunctionDefinitionExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="FunctionDefinitionExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = (FunctionDefinitionExpression)obj;
            return Name == that.Name && Parameters == that.Parameters && Expressions == that.Expressions;
        }

        bool INestedExpressions.GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            if (_keyword != null && _keyword.Line == line)
                expressions.Add(_keyword);
            if (Name.Line == line)
                expressions.Add(Name);

            foreach (var parameter in Parameters)
            {
                if (parameter.Line == line)
                    expressions.Add(parameter);
            }

            ExpressionGroup.GetExpressionsForLine(expressions, Expressions, line);

            return true;
        }
    }
}
