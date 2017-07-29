using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Jamiras.Components;

namespace RATools.Parser.Internal
{
    internal class FunctionDefinitionExpression : ExpressionBase
    {
        public FunctionDefinitionExpression(string name)
            : this()
        {
            Name = name;
        }

        public FunctionDefinitionExpression()
            : base(ExpressionType.FunctionDefinition)
        {
            Parameters = new List<string>();
            Expressions = new List<ExpressionBase>();
        }

        public string Name { get; private set; }

        public ICollection<string> Parameters { get; private set; }
        public ICollection<ExpressionBase> Expressions { get; private set; }

        public override string ToString()
        {
            var builder = new StringBuilder();
            AppendString(builder);
            return builder.ToString();
        }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("function ");
            builder.Append(Name);
            builder.Append('(');

            if (Parameters.Count > 0)
            {
                foreach (var parameter in Parameters)
                {
                    builder.Append(parameter);
                    builder.Append(", ");
                }
                builder.Length -= 2;
            }

            builder.Append(')');
        }

        internal new static ExpressionBase Parse(PositionalTokenizer tokenizer)
        {
            var function = new FunctionDefinitionExpression();

            ExpressionBase.SkipWhitespace(tokenizer);

            var functionName = tokenizer.ReadIdentifier();
            if (functionName.IsEmpty)
                return new ParseErrorExpression("Invalid function name");
            function.Name = functionName.ToString();

            ExpressionBase.SkipWhitespace(tokenizer);
            if (tokenizer.NextChar != '(')
                return new ParseErrorExpression("Expected '(' after function name", tokenizer.Line, tokenizer.Column);
            tokenizer.Advance();

            int line, column;

            ExpressionBase.SkipWhitespace(tokenizer);
            if (tokenizer.NextChar != ')')
            {
                do
                {
                    line = tokenizer.Line;
                    column = tokenizer.Column;

                    var parameter = tokenizer.ReadIdentifier();
                    if (parameter.IsEmpty)
                        return new ParseErrorExpression("Invalid parameter name", line, column);

                    function.Parameters.Add(parameter.ToString());

                    ExpressionBase.SkipWhitespace(tokenizer);
                    if (tokenizer.NextChar == ')')
                        break;

                    if (tokenizer.NextChar != ',')
                        return new ParseErrorExpression("Invalid parameter name", line, column);

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

                var returnExpression = new ReturnExpression(expression);
                function.Expressions.Add(returnExpression);
                return function;
            }

            if (tokenizer.NextChar != '{')
                return new ParseErrorExpression("Opening brace expected after function declaration", tokenizer.Line, tokenizer.Column);

            line = tokenizer.Line;
            column = tokenizer.Column;
            tokenizer.Advance();
            ExpressionBase.SkipWhitespace(tokenizer);

            bool seenReturn = false;
            while (tokenizer.NextChar != '}')
            {
                expression = ExpressionBase.Parse(tokenizer);
                if (expression.Type == ExpressionType.ParseError)
                    return expression;

                if (tokenizer.NextChar == '\0')
                    return new ParseErrorExpression("No matching closing brace found", line, column);

                if (expression.Type == ExpressionType.Return)
                    seenReturn = true;
                else if (seenReturn)
                    return new ParseErrorExpression("Expression after return statement", expression.Line, expression.Column);

                function.Expressions.Add(expression);

                ExpressionBase.SkipWhitespace(tokenizer);
            }

            tokenizer.Advance();
            return function;
        }
    }
}
