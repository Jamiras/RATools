using System.Text;

namespace RATools.Parser.Expressions
{
    public class KeywordExpression : ExpressionBase
    {
        public KeywordExpression(string keyword)
            : base(ExpressionType.Keyword)
        {
            Keyword = keyword;
        }

        internal KeywordExpression(string keyword, int line, int column)
            : this(keyword)
        {
            Location = new Jamiras.Components.TextRange(line, column, line, column + keyword.Length - 1);
        }

        /// <summary>
        /// Gets the keyword.
        /// </summary>
        public string Keyword { get; private set; }

        /// <summary>
        /// Gets whether this is non-changing.
        /// </summary>
        public override bool IsConstant
        {
            get { return true; }
        }

        // NOTE: Not returning true for IsLiteralConstant. While it is unchanging at compile-time,
        //       it won't make it to the output.

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(Keyword);
        }

        /// <summary>
        /// Determines whether the specified <see cref="KeywordExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="KeywordExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="KeywordExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as KeywordExpression;
            return that != null && Keyword == that.Keyword;
        }
    }
}
