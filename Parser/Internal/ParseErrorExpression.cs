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

        public string Message { get; private set; }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(Message);
        }
    }
}
