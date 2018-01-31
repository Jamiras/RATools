using System.Collections.Generic;

namespace RATools.Parser.Internal
{
    internal class ExpressionGroup
    {
        public ExpressionGroup()
        {
            Errors = new List<ParseErrorExpression>();
            Comments = new List<CommentExpression>();
            Expressions = new List<ExpressionBase>();
        }

        public List<ParseErrorExpression> Errors { get; private set; }
        public List<CommentExpression> Comments { get; private set; }
        public List<ExpressionBase> Expressions { get; private set; }

        public bool GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            bool result = GetExpressionsForLine(expressions, Expressions, line);

            foreach (var comment in Comments)
            {
                if (comment.Line == line)
                {
                    expressions.Add(comment);
                    break;
                }
            }

            foreach (var error in Errors)
            {
                if (error.Line > line)
                    break;

                if (error.Line == line)
                    expressions.Add(error);
            }

            return result;
        }

        public static bool GetExpressionsForLine(List<ExpressionBase> matchingExpressions, IEnumerable<ExpressionBase> expressions, int line)
        { 
            foreach (var expression in expressions)
            {
                if (expression.EndLine < line)
                    continue;

                if (expression.Line > line)
                    break;

                var nested = expression as INestedExpressions;
                if (nested != null)
                {
                    if (!nested.GetExpressionsForLine(matchingExpressions, line))
                        return false;
                }
                else
                {
                    matchingExpressions.Add(expression);
                }
            }

            return true;
        }
    }

    internal interface INestedExpressions
    {
        bool GetExpressionsForLine(List<ExpressionBase> expressions, int line);
    }
}
