using Jamiras.Components;
using System.Diagnostics;

namespace RATools.Parser.Internal
{
    internal class ExpressionTokenizer : PositionalTokenizer
    {
        public ExpressionTokenizer(Tokenizer tokenizer, ExpressionGroup expressionGroup)
            : base(tokenizer)
        {
            _expressionGroup = expressionGroup;
        }

        private ExpressionGroup _expressionGroup;

        public void ChangeExpressionGroup(ExpressionGroup expressionGroup)
        {
            _expressionGroup = expressionGroup;
        }

        public void AddComment(CommentExpression comment)
        {
            _expressionGroup.AddExpression(comment);
        }

        public void AddError(ErrorExpression error)
        {
            _expressionGroup.AddParseError(error);
        }

        public void AdvanceToLine(int line)
        {
            while (Line < line && NextChar != '\0')
                Advance();
        }

        private ExpressionBase _queuedExpression;

        public void QueueExpression(ExpressionBase expression)
        {
            Debug.Assert(_queuedExpression == null);
            _queuedExpression = expression;
        }

        public ExpressionBase DequeueExpression()
        {
            if (_queuedExpression != null)
            {
                var queuedExpression = _queuedExpression;
                _queuedExpression = null;
                return queuedExpression;
            }

            return null;
        }
    }
}
