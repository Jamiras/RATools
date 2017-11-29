using System.Text;

namespace RATools.Parser.Internal
{
    internal class ParseErrorExpression : ExpressionBase
    {
        public ParseErrorExpression(string message)
            : base(ExpressionType.ParseError)
        {
            Message = message;
        }

        public ParseErrorExpression(string message, int line, int column)
            : this(message)
        {
            Line = line;
            Column = column;
        }

        /// <summary>
        /// Gets the message.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(Message);
        }

        /// <summary>
        /// Determines whether the specified <see cref="ParseErrorExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="ParseErrorExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="ParseErrorExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = (ParseErrorExpression)obj;
            return Message == that.Message;
        }
    }
}
