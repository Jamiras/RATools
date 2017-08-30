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

            var error = ExpressionBase.ParseStatementBlock(tokenizer, loop.Expressions);
            if (error != null)
                return error;

            return loop;
        }
    }
}
