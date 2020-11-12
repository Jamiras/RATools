﻿using Jamiras.Components;

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
            _expressionGroup.Expressions.Add(comment);
        }

        public void AddError(ParseErrorExpression error)
        {
            _expressionGroup.Errors.Add(error);
        }
    }
}
