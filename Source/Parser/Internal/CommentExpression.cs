using System.Text;

namespace RATools.Parser.Internal
{
    internal class CommentExpression : ExpressionBase
    {
        public CommentExpression(string value)
            : base(ExpressionType.Comment)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(Value);
        }

        /// <summary>
        /// Determines whether the specified <see cref="CommentExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="CommentExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="CommentExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as CommentExpression;
            return (that != null && Value == that.Value);
        }
    }
}
