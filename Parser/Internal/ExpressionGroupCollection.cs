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
            ParseGroups(expressionTokenizer, Groups);

            foreach (var group in Groups)
            {
                group.UpdateMetadata();
                group.NeedsEvaluated = group.HasExpressionsToEvaluate;
            }
        }

        private static void ParseGroups(ExpressionTokenizer tokenizer, List<ExpressionGroup> groups)
        {
            while (tokenizer.NextChar != '\0')
            {
                // create a separate group for comments
                var newGroup = new ExpressionGroup();
                groups.Add(newGroup);
                tokenizer.ChangeExpressionGroup(newGroup);
                ExpressionBase.SkipWhitespace(tokenizer);

                // if comments were found, start a new group
                if (!newGroup.IsEmpty)
                {
                    newGroup = new ExpressionGroup();
                    groups.Add(newGroup);
                    tokenizer.ChangeExpressionGroup(newGroup);
                }

                var expression = ExpressionBase.Parse(tokenizer);

                // sometimes parsing an expression will process trailing whitespace looking for a continuation character. 
                // move the trailing comments into a separate group. do not move the internal comments
                var commentGroup = newGroup.ExtractTrailingComments(expression);
                if (commentGroup != null)
                    groups.Add(commentGroup);

                AddTopLevelExpression(newGroup, expression);
            }

            var lastGroup = groups.LastOrDefault();
            if (lastGroup != null && lastGroup.IsEmpty)
                groups.RemoveAt(groups.Count - 1);
        }

        private static void AddTopLevelExpression(ExpressionGroup expressionGroup, ExpressionBase expression)
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

        public void Update(Tokenizer tokenizer, IEnumerable<int> affectedLines)
        {
            var expressionTokenizer = new ExpressionTokenizer(tokenizer, null);
            var nextUpdatedLine = affectedLines.Min();

            // we can ignore everything before the first modified line
            int groupStart = 0;
            while (groupStart < Groups.Count && nextUpdatedLine > Groups[groupStart].LastLine)
                ++groupStart;
            if (groupStart < Groups.Count)
                nextUpdatedLine = Math.Min(Groups[groupStart].FirstLine, nextUpdatedLine);
            expressionTokenizer.AdvanceToLine(nextUpdatedLine);

            // parse whatever is remaining
            var newGroups = new List<ExpressionGroup>();
            ParseGroups(expressionTokenizer, newGroups);

            // attempt to match the end of the script
            int groupStop = Groups.Count;
            int newGroupStop = newGroups.Count;

            while (groupStop > groupStart && newGroupStop > 0)
            {
                var existingGroup = Groups[--groupStop];
                var newGroup = newGroups[--newGroupStop];

                if (!existingGroup.ExpressionsMatch(newGroup))
                {
                    ++groupStop;
                    ++newGroupStop;
                    break;
                }

                existingGroup.ReplaceExpressions(newGroup);
            }

            // whatever is remaining will be swapped out. capture any affected variables
            var affectedVariables = new HashSet<string>();
            for (int i = groupStart; i < groupStop; ++i)
            {
                foreach (var variable in Groups[i].Modifies)
                {
                    affectedVariables.Add(variable);
                    Scope.UndefineVariable(variable);
                }
            }
            for (int i = 0; i < newGroupStop; ++i)
            {
                var newGroup = newGroups[i];
                newGroup.UpdateMetadata();
                newGroup.NeedsEvaluated = newGroup.HasExpressionsToEvaluate;

                foreach (var variable in newGroup.Modifies)
                    affectedVariables.Add(variable);
            }

            // perform the swap
            Groups.RemoveRange(groupStart, groupStop - groupStart);
            Groups.InsertRange(groupStart, newGroups.Take(newGroupStop));

            // re-evaluate any groups that are dependent on (or modify) the affected variables
            if (affectedVariables.Count > 0)
                FlagDependencies(affectedVariables);
        }

        private void FlagDependencies(HashSet<string> affectedVariables)
        {
            int count;
            do
            {
                count = affectedVariables.Count;

                foreach (var group in Groups)
                {
                    // this group is already flagged for evaluation - ignore
                    if (group.NeedsEvaluated)
                        continue;

                    if (group.IsDependentOn(affectedVariables))
                    {
                        // this group depends on one of the affected variables, re-evaluate
                        group.NeedsEvaluated = true;

                        // also flag the output of this group for possible chaining
                        foreach (var variable in group.Modifies)
                            affectedVariables.Add(variable);
                    }
                    else if (group.Modifies.Any(v => affectedVariables.Contains(v)))
                    {
                        // this group could modify one of the affected variables.
                        // any time a variable is potentially modified, we have to fully re-evaluate it
                        group.NeedsEvaluated = true;
                    }
                }

                // if any new items were added to the affected variables list, go through again and check
                // for new stuff to flag
            } while (affectedVariables.Count > count);
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
