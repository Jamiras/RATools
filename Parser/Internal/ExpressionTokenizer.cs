using Jamiras.Components;

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

        public void AddError(ParseErrorExpression error)
        {
            _expressionGroup.AddError(error);
        }

        public void AdvanceToLine(int line)
        {
            while (Line < line && NextChar != '\0')
                Advance();
        }
    }
}
