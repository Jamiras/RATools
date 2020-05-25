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

        public ParseErrorExpression(string message, int line, int column, int endLine, int endColumn)
            : this(message)
        {
            Line = line;
            Column = column;
            EndLine = endLine;
            EndColumn = endColumn;
        }

        public ParseErrorExpression(string message, ExpressionBase expression)
            : this(message, expression.Line, expression.Column, expression.EndLine, expression.EndColumn)
        {
        }

        public ParseErrorExpression(ExpressionBase error, ExpressionBase expression)
            : base(ExpressionType.ParseError)
        {
            Line = expression.Line;
            Column = expression.Column;
            EndLine = expression.EndLine;
            EndColumn = expression.EndColumn;

            var parseError = error as ParseErrorExpression;
            if (parseError != null)
            {
                Message = parseError.Message;
                InnerError = parseError.InnerError;

                if (parseError.Line != 0)
                {
                    Line = parseError.Line;
                    Column = parseError.Column;
                    EndLine = parseError.EndLine;
                    EndColumn = parseError.EndColumn;
                }
            }
            else
            {
                Message = "Unknown error: " + error.Type;
            }
        }

        /// <summary>
        /// Gets the message.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Gets a secondary error that caused this error.
        /// </summary>
        public ParseErrorExpression InnerError { get; internal set; }

        /// <summary>
        /// Gets the root error that caused this error.
        /// </summary>
        public ParseErrorExpression InnermostError
        {
            get
            {
                var error = InnerError;
                if (error != null)
                {
                    while (error.InnerError != null)
                        error = error.InnerError;
                }

                return error;
            }
        }

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
