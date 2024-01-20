using Jamiras.Components;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class ErrorExpression : ExpressionBase
    {
        public ErrorExpression(string message)
            : base(ExpressionType.Error)
        {
            Message = message;
        }

        public ErrorExpression(string message, int line, int column, int endLine, int endColumn)
            : this(message, new TextRange(line, column, endLine, endColumn))
        {
        }

        public ErrorExpression(string message, ExpressionBase expression)
            : this(message, expression.Location)
        {
        }

        public ErrorExpression(string message, TextRange location)
            : this(message)
        {
            Location = location;
        }

        public ErrorExpression(ExpressionBase error, ExpressionBase expression)
            : base(ExpressionType.Error)
        {
            Location = expression.Location;

            var parseError = error as ErrorExpression;
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

        public static ErrorExpression WrapError(ErrorExpression error, string message, ExpressionBase expression)
        {
            if (error.Location.End == expression.Location.End && error.Location.Start == expression.Location.Start)
            {
                return error;
            }

            return new ErrorExpression(message, expression) { InnerError = error };
        }

        /// <summary>
        /// Gets the message.
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Gets a secondary error that caused this error.
        /// </summary>
        public ErrorExpression InnerError { get; internal set; }

        /// <summary>
        /// Gets the root error that caused this error.
        /// </summary>
        public ErrorExpression InnermostError
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
        /// Determines whether the specified <see cref="ErrorExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="ErrorExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="ErrorExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as ErrorExpression;
            return that != null && Message == that.Message;
        }
    }

    internal class UnknownVariableParseErrorExpression : ErrorExpression
    {
        public UnknownVariableParseErrorExpression(string message, ExpressionBase expression)
            : base(message, expression)
        {
        }
    }
}
