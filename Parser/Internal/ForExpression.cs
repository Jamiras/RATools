using System.Collections.Generic;
using System.Text;
using Jamiras.Components;

namespace RATools.Parser.Internal
{
    internal class ForExpression : ExpressionBase
    {
        public ForExpression()
            : base(ExpressionType.For)
        {
            Expressions = new List<ExpressionBase>();
        }

        public string IteratorName { get; private set; }
        public ExpressionBase Range { get; private set; }

        public ICollection<ExpressionBase> Expressions { get; private set; }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(IteratorName);
            builder.Append(" in ");
            Range.AppendString(builder);
        }

        internal new static ExpressionBase Parse(PositionalTokenizer tokenizer)
        {
            var loop = new ForExpression();

            ExpressionBase.SkipWhitespace(tokenizer);

            var iteratorName = tokenizer.ReadIdentifier();
            if (iteratorName.IsEmpty)
                return new ParseErrorExpression("Invalid function name");
            loop.IteratorName = iteratorName.ToString();

            ExpressionBase.SkipWhitespace(tokenizer);
            if (!tokenizer.Match("in"))
                return new ParseErrorExpression("Expected 'in' after loop variable", tokenizer.Line, tokenizer.Column);

            loop.Range = ExpressionBase.Parse(tokenizer);
            if (loop.Range.Type == ExpressionType.ParseError)
                return loop.Range;

            if (tokenizer.NextChar != '{')
                return new ParseErrorExpression("Opening brace expected after loop declaration", tokenizer.Line, tokenizer.Column);

            var line = tokenizer.Line;
            var column = tokenizer.Column;
            tokenizer.Advance();
            ExpressionBase.SkipWhitespace(tokenizer);

            while (tokenizer.NextChar != '}')
            {
                var expression = ExpressionBase.Parse(tokenizer);
                if (expression.Type == ExpressionType.ParseError)
                    return expression;

                if (tokenizer.NextChar == '\0')
                    return new ParseErrorExpression("No matching closing brace found", line, column);

                loop.Expressions.Add(expression);

                ExpressionBase.SkipWhitespace(tokenizer);
            }

            tokenizer.Advance();
            return loop;
        }
    }
}
