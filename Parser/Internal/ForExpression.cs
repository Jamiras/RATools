using Jamiras.Components;
using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class ForExpression : ExpressionBase
    {
        public ForExpression(string iteratorName, ExpressionBase range)
            : base(ExpressionType.For)
        {
            IteratorName = iteratorName;
            Range = range;
            Expressions = new List<ExpressionBase>();
        }

        /// <summary>
        /// Gets the name of the iterator variable.
        /// </summary>
        public string IteratorName { get; private set; }

        /// <summary>
        /// Gets the expression that defines the values for each iteration.
        /// </summary>
        public ExpressionBase Range { get; private set; }

        /// <summary>
        /// Gets the expressions to evaluate for each iteration.
        /// </summary>
        public ICollection<ExpressionBase> Expressions { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("for ");
            builder.Append(IteratorName);
            builder.Append(" in ");
            Range.AppendString(builder);
        }

        /// <summary>
        /// Parses a for loop.
        /// </summary>
        /// <remarks>
        /// Assumes the 'for' keyword has already been consumed.
        /// </remarks>
        internal new static ExpressionBase Parse(PositionalTokenizer tokenizer)
        {
            ExpressionBase.SkipWhitespace(tokenizer);

            var iteratorName = tokenizer.ReadIdentifier();
            if (iteratorName.IsEmpty)
                return new ParseErrorExpression("Invalid function name");

            ExpressionBase.SkipWhitespace(tokenizer);
            if (!tokenizer.Match("in"))
                return new ParseErrorExpression("Expected 'in' after loop variable", tokenizer.Line, tokenizer.Column);

            var range = ExpressionBase.Parse(tokenizer);
            if (range.Type == ExpressionType.ParseError)
                return range;

            var loop = new ForExpression(iteratorName.ToString(), range);

            var error = ExpressionBase.ParseStatementBlock(tokenizer, loop.Expressions);
            if (error != null)
                return error;

            return loop;
        }

        /// <summary>
        /// Determines whether the specified <see cref="ForExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="ForExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="ForExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = (ForExpression)obj;
            return IteratorName == that.IteratorName && Range == that.Range && Expressions == that.Expressions;
        }
    }
}
