using System.Collections.Generic;

namespace RATools.Parser.Internal
{
    internal class ExpressionGroup
    {
        public ExpressionGroup()
        {
            Errors = new List<ParseErrorExpression>();
            Expressions = new List<ExpressionBase>();
        }

        public List<ParseErrorExpression> Errors { get; private set; }
        public List<ExpressionBase> Expressions { get; private set; }

        public bool GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            bool result = GetExpressionsForLine(expressions, Expressions, line);

            foreach (var error in Errors)
            {
                var innerError = error.InnermostError ?? error;
                if (innerError.Line <= line && innerError.EndLine >= line)
                   expressions.Add(innerError);
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
