using Jamiras.Components;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Internal
{
    internal class ExpressionGroupCollection
    {
        public ExpressionGroupCollection()
        {
            Groups = new List<ExpressionGroup>();

        }

        public List<ExpressionGroup> Groups { get; private set; }
        public InterpreterScope Scope { get; set; }

        public void Parse(Tokenizer tokenizer)
        {
            Groups.Clear();

            var expressionTokenizer = new ExpressionTokenizer(tokenizer, null);
            ExpressionGroup expressionGroup;

            while (expressionTokenizer.NextChar != '\0')
            {
                // create a separate group for comments
                var commentGroup = new ExpressionGroup();
                Groups.Add(commentGroup);
                expressionTokenizer.ChangeExpressionGroup(commentGroup);
                ExpressionBase.SkipWhitespace(expressionTokenizer);

                // if comments were found, start a new group
                if (!commentGroup.IsEmpty)
                {
                    expressionGroup = new ExpressionGroup();
                    Groups.Add(expressionGroup);
                    expressionTokenizer.ChangeExpressionGroup(expressionGroup);
                }
                else
                {
                    expressionGroup = commentGroup;
                }

                var expression = ExpressionBase.Parse(expressionTokenizer);
                switch (expression.Type)
                {
                    case ExpressionType.For:
                    case ExpressionType.Assignment:
                    case ExpressionType.FunctionCall:
                    case ExpressionType.FunctionDefinition:
                        /* valid at top-level */
                        expressionGroup.AddExpression(expression);
                        break;

                    default:
                        expressionGroup.AddError(new ParseErrorExpression(String.Format("standalone {0} has no meaning", expression.Type), expression));
                        break;
                }
            }

            var lastGroup = Groups.LastOrDefault();
            if (lastGroup != null && lastGroup.IsEmpty)
                Groups.RemoveAt(Groups.Count - 1);

            foreach (var group in Groups)
            {
                group.UpdateMetadata();
                group.NeedsEvaluated = group.Modifies.Any();
            }
        }

        public IEnumerable<ParseErrorExpression> Errors
        {
            get
            {
                foreach (var group in Groups)
                {
                    foreach (var error in group.Errors)
                        yield return error;
                }
            }
        }


        public bool GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            bool result = false;
            int left = 0;
            int right = Groups.Count;

            while (left != right)
            {
                int mid = (left + right) / 2;
                var group = Groups[mid];
                if (line < group.FirstLine)
                {
                    right = mid;
                }
                else if (line > group.LastLine)
                {
                    left = mid;
                }
                else
                {
                    var index = mid;
                    while (index >= left && index > 0 && Groups[index - 1].LastLine >= line)
                        index--;

                    while (index < right)
                    {
                        group = Groups[index++];
                        if (group.FirstLine > line)
                            break;

                        result |= group.GetExpressionsForLine(expressions, line);
                    }
                    break;
                }
            }

            return result;
        }
    }
}
