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
            Location = new Jamiras.Components.TextRange(line, column, endLine, endColumn);
        }

        public ParseErrorExpression(string message, ExpressionBase expression)
            : this(message)
        {
            Location = expression.Location;
        }

        public ParseErrorExpression(ExpressionBase error, ExpressionBase expression)
            : base(ExpressionType.ParseError)
        {
            Location = expression.Location;

            var parseError = error as ParseErrorExpression;
            if (parseError != null)
            {
                Message = parseError.Message;
                InnerError = parseError.InnerError;

                if (parseError.Location.Start.Line != 0)
                {
                    Location = parseError.Location;
                }
            }
            else
            {
                Message = "Unknown error: " + error.Type;
            }
        }

        public static ParseErrorExpression WrapError(ParseErrorExpression error, string message, ExpressionBase expression)
        {
            if (error.Location.End == expression.Location.End && error.Location.Start == expression.Location.Start)
            {
                return error;
            }

            return new ParseErrorExpression(message, expression) { InnerError = error };
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
            var that = obj as ParseErrorExpression;
            return that != null && Message == that.Message;
        }
    }

    internal class UnknownVariableParseErrorExpression : ParseErrorExpression
    {
        public UnknownVariableParseErrorExpression(string message, ExpressionBase expression)
            : base(message, expression)
        {
        }
    }
}
