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

        public void Parse(Tokenizer tokenizer)
        {
            Groups.Clear();

            var expressionTokenizer = new ExpressionTokenizer(tokenizer, null);
            ExpressionGroup constantVariablesExpressionGroup = null;
            ExpressionGroup expressionGroup;

            while (expressionTokenizer.NextChar != '\0')
            {
                // create a separate group for comments
                var commentGroup = new ExpressionGroup();
                Groups.Add(commentGroup);
                expressionTokenizer.ChangeExpressionGroup(commentGroup);
                ExpressionBase.SkipWhitespace(expressionTokenizer);

                // if comments were found, start a new group
                if (commentGroup.Comments.Count > 0)
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
                    case ExpressionType.Assignment:
                        /* valid at top-level */
                        expressionGroup.Expressions.Add(expression);

                        // multiple constant assignments can be grouped into a single expression group with no dependencies
                        var assignment = (AssignmentExpression)expression;
                        switch (assignment.Value.Type)
                        {
                            case ExpressionType.IntegerConstant:
                            case ExpressionType.StringConstant:
                                if (constantVariablesExpressionGroup != null)
                                {
                                    constantVariablesExpressionGroup.Comments.AddRange(commentGroup.Comments);
                                    constantVariablesExpressionGroup.Expressions.AddRange(expressionGroup.Expressions);
                                    constantVariablesExpressionGroup.Errors.AddRange(expressionGroup.Errors);
                                }
                                else
                                {
                                    constantVariablesExpressionGroup = expressionGroup;

                                    if (!ReferenceEquals(commentGroup, expressionGroup))
                                    {
                                        expressionGroup.Comments.InsertRange(0, commentGroup.Comments);
                                        Groups.Remove(commentGroup);
                                    }
                                }
                                break;

                            default:
                                constantVariablesExpressionGroup = null;
                                break;
                        }
                        break;

                    case ExpressionType.For:
                    case ExpressionType.FunctionCall:
                    case ExpressionType.FunctionDefinition:
                        /* valid at top-level */
                        expressionGroup.Expressions.Add(expression);
                        constantVariablesExpressionGroup = null;
                        break;

                    default:
                        expressionGroup.Errors.Add(new ParseErrorExpression(String.Format("standalone {0} has no meaning", expression.Type), expression));
                        constantVariablesExpressionGroup = null;
                        break;
                }
            }

            var lastGroup = Groups.LastOrDefault();
            if (lastGroup != null && lastGroup.Expressions.Count == 0 && lastGroup.Comments.Count == 0)
                Groups.RemoveAt(Groups.Count - 1);
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

            foreach (var group in Groups)
                result |= group.GetExpressionsForLine(expressions, line);

            return result;
        }
    }
}
