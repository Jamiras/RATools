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
            while (expressionTokenizer.NextChar != '\0')
            {
                // create a separate group for comments
                var newGroup = new ExpressionGroup();
                Groups.Add(newGroup);
                expressionTokenizer.ChangeExpressionGroup(newGroup);
                ExpressionBase.SkipWhitespace(expressionTokenizer);

                // if comments were found, start a new group
                if (!newGroup.IsEmpty)
                {
                    newGroup = new ExpressionGroup();
                    Groups.Add(newGroup);
                    expressionTokenizer.ChangeExpressionGroup(newGroup);
                }

                var expression = ExpressionBase.Parse(expressionTokenizer);

                // sometimes parsing an expression will process trailing whitespace looking for a continuation character. 
                // move the trailing comments into a separate group. do not move the internal comments
                var commentGroup = newGroup.ExtractTrailingComments(expression);
                if (commentGroup != null)
                    Groups.Add(commentGroup);

                switch (expression.Type)
                {
                    case ExpressionType.For:
                    case ExpressionType.Assignment:
                    case ExpressionType.FunctionCall:
                    case ExpressionType.FunctionDefinition:
                        /* valid at top-level */
                        newGroup.AddExpression(expression);
                        newGroup.NeedsEvaluated = true;
                        break;

                    default:
                        newGroup.AddError(new ParseErrorExpression(String.Format("standalone {0} has no meaning", expression.Type), expression));
                        break;
                }
            }

            var lastGroup = Groups.LastOrDefault();
            if (lastGroup != null && lastGroup.IsEmpty)
                Groups.RemoveAt(Groups.Count - 1);

            foreach (var group in Groups)
                group.UpdateMetadata();
        }

        public void Update(Tokenizer tokenizer, IEnumerable<int> affectedLines)
        {
            var expressionTokenizer = new ExpressionTokenizer(tokenizer, null);

            var updatedLines = new List<int>(affectedLines);
            updatedLines.Sort();
            var updatedLineIndex = 0;
            var nextUpdatedLine = (updatedLines.Count > 0) ? updatedLines[0] : 0;

            var affectedVariables = new HashSet<string>();
            int adjust = 0;
            for (int i = 0; i < Groups.Count; ++i)
            {
                var group = Groups[i];

                // if the next updated line is not in the group, skip the group
                var lastLine = group.LastLine + adjust;
                if (nextUpdatedLine == 0 || nextUpdatedLine > lastLine)
                {
                    if (adjust != 0)
                        group.AdjustLines(adjust);
                    continue;
                }

                // if the next updated line is between groups, check for a potential new group
                var firstLine = group.FirstLine + adjust;
                if (nextUpdatedLine < firstLine && expressionTokenizer.Line < firstLine)
                { 
                    group = new ExpressionGroup();
                    group.FirstLine = Math.Max(nextUpdatedLine, expressionTokenizer.Line);
                    group.LastLine = lastLine;
                    Groups.Insert(i, group);
                }
                else
                {
                    // undefine any variables modified by this group - we'll completely re-evaluate them later
                    foreach (var variable in group.Modifies)
                    {
                        affectedVariables.Add(variable);
                        Scope.UndefineVariable(variable);
                    }
                }

                // update nextUpdatedLine for the next trip through the loop
                do
                {
                    if (++updatedLineIndex == updatedLines.Count)
                    {
                        nextUpdatedLine = 0;
                        break;
                    }

                    nextUpdatedLine = updatedLines[updatedLineIndex];
                } while (nextUpdatedLine <= group.LastLine);

                // skip ahead to the data to parse
                expressionTokenizer.AdvanceToLine(group.FirstLine);

                // do the parse
                int newLastLine = 0;
                var newGroup = new ExpressionGroup();
                expressionTokenizer.ChangeExpressionGroup(newGroup);
                ExpressionBase.SkipWhitespace(expressionTokenizer);

                if (!newGroup.IsEmpty)
                {
                    newGroup.UpdateMetadata();

                    // found comments
                    if (group.IsEmpty || // replacing a placeholder
                        group.Expressions.First().Type == ExpressionType.Comment) // group was only comments before
                    {
                        Groups[i] = newGroup;
                        newLastLine = newGroup.LastLine;
                    }
                    else
                    {
                        // pre-existing non-comment group before. insert the new group for the comments
                        Groups.Insert(i++, newGroup);
                    }
                }

                if (newLastLine == 0)
                {
                    if (!newGroup.IsEmpty)
                    {
                        newGroup = new ExpressionGroup();
                        expressionTokenizer.ChangeExpressionGroup(newGroup);
                    }

                    var expression = ExpressionBase.Parse(expressionTokenizer);
                    newGroup.ExtractTrailingComments(expression);
                    AddTopLevelExpression(newGroup, expression);
                    newGroup.UpdateMetadata();

                    newGroup.NeedsEvaluated = true;
                    Groups[i] = newGroup;
                    newLastLine = newGroup.LastLine;
                }

                // make sure to re-evaluate anything that might have already been looking for modified names
                foreach (var variable in newGroup.Modifies)
                    affectedVariables.Add(variable);

                adjust = (newLastLine - lastLine);
            }

            // re-evaluate any groups that are dependent on (or modify) the affected variables
            foreach (var group in Groups)
            {
                if (!group.NeedsEvaluated &&
                    (group.IsDependentOn(affectedVariables) || group.Modifies.Any(v => affectedVariables.Contains(v))))
                {
                    group.NeedsEvaluated = true;
                }
            }
        }

        private void AddTopLevelExpression(ExpressionGroup expressionGroup, ExpressionBase expression)
        {
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

            foreach (var group in GetGroupsForLine(line))
                result |= group.GetExpressionsForLine(expressions, line);

            return result;
        }

        public IEnumerable<ExpressionGroup> GetGroupsForLine(int line)
        {
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
                    left = mid + 1;
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

                        yield return group;
                    }
                    break;
                }
            }
        }
    }
}
