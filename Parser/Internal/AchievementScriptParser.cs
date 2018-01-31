using Jamiras.Components;

namespace RATools.Parser.Internal
{
    internal class AchievementScriptParser
    {
        public ExpressionGroup Parse(Tokenizer tokenizer)
        {
            var expressionGroup = new ExpressionGroup();
            var expressionTokenizer = new ExpressionTokenizer(tokenizer, expressionGroup);

            ExpressionBase.SkipWhitespace(expressionTokenizer);

            while (expressionTokenizer.NextChar != '\0')
            {
                var expression = ExpressionBase.Parse(expressionTokenizer);
                if (expression != null)
                {
                    if (expression is VariableExpression)
                        expressionGroup.Errors.Add(new ParseErrorExpression("standalone variable has no meaning", expression));

                    expressionGroup.Expressions.Add(expression);
                }
            }

            return expressionGroup;
        }
    }
}
