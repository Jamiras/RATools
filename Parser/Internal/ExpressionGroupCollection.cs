﻿using Jamiras.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RATools.Parser.Internal
{
    internal class ExpressionGroupCollection
    {
        public ExpressionGroupCollection()
        {
            Groups = new List<ExpressionGroup>();
            _evaluationErrors = new List<ParseErrorExpression>();
        }

        public List<ExpressionGroup> Groups { get; private set; }
        public InterpreterScope Scope { get; set; }

        private readonly List<ParseErrorExpression> _evaluationErrors;

        public void Parse(Tokenizer tokenizer)
        {
            Groups.Clear();

            var expressionTokenizer = new ExpressionTokenizer(tokenizer, null);
            ParseGroups(expressionTokenizer, Groups);

            foreach (var group in Groups)
            {
                group.UpdateMetadata();
                group.MarkForEvaluation();
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

                newGroup.AddExpression(expression);
            }

            var lastGroup = groups.LastOrDefault();
            if (lastGroup != null && lastGroup.IsEmpty)
                groups.RemoveAt(groups.Count - 1);
        }

        public bool Update(Tokenizer tokenizer, IEnumerable<int> affectedLines)
        {
            var expressionTokenizer = new ExpressionTokenizer(tokenizer, null);
            int groupStart = 0;

            // we can ignore everything before the first modified line
            if (affectedLines.Any())
            {
                var nextUpdatedLine = affectedLines.Min();
                Debug.WriteLine("Updating {0}-{1} ({2} groups)", nextUpdatedLine, affectedLines.Max(), Groups.Count);

                while (groupStart < Groups.Count && nextUpdatedLine > Groups[groupStart].LastLine)
                    ++groupStart;
                if (groupStart < Groups.Count)
                    nextUpdatedLine = Math.Min(Groups[groupStart].FirstLine, nextUpdatedLine);

                expressionTokenizer.AdvanceToLine(nextUpdatedLine);
            }
            else
            {
                Debug.WriteLine("Updating all lines ({0} groups)", Groups.Count);
            }

            DumpGroups(groupStart - 2, groupStart + 2);

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

                existingGroup.ReplaceExpressions(newGroup, false);
            }

            // whatever is remaining will be swapped out. 
            // capture any affected variables and remove associated evaluation errors
            var affectedVariables = new HashSet<string>();
            for (int i = groupStart; i < groupStop; ++i)
            {
                var group = Groups[i];
                foreach (var variable in group.Modifies)
                {
                    affectedVariables.Add(variable);
                    Scope.UndefineVariable(variable);
                }

                for (int j = _evaluationErrors.Count - 1; j >= 0; j--)
                {
                    var error = _evaluationErrors[j];
                    if (error.EndLine >= group.FirstLine && error.Line <= group.LastLine)
                    {
                        _evaluationErrors.RemoveAt(j);
                    }
                    else
                    {
                        error = error.InnermostError;
                        if (error.EndLine >= group.FirstLine && error.Line <= group.LastLine)
                            _evaluationErrors.RemoveAt(j);
                    }
                }
            }

            // also capture any affected variables for groups being swapped in, and determine 
            // if they need to be evaluated.
            for (int i = 0; i < newGroupStop; ++i)
            {
                var newGroup = newGroups[i];
                newGroup.UpdateMetadata();
                newGroup.MarkForEvaluation();

                foreach (var variable in newGroup.Modifies)
                    affectedVariables.Add(variable);
            }

            // perform the swap
            Debug.WriteLine("Replacing {0}-{1} with {2} lines", groupStart, groupStop - 1, newGroupStop);
            Groups.RemoveRange(groupStart, groupStop - groupStart);
            Groups.InsertRange(groupStart, newGroups.Take(newGroupStop));

            DumpGroups(groupStart - 2, groupStart + newGroupStop + 2);

            bool needsEvaluated = false;

            // re-evaluate any groups that are dependent on (or modify) the affected variables
            if (affectedVariables.Count > 0)
            {
                FlagDependencies(affectedVariables);
                needsEvaluated = true;
            }
            else
            {
                for (int i = 0; i < newGroupStop; ++i)
                {
                    if (newGroups[i].NeedsEvaluated)
                    {
                        needsEvaluated = true;
                        break;
                    }
                }
            }

            return needsEvaluated;
        }

        private void DumpGroups(int start, int end)
        {
            for (int i = start; i < end; ++i)
            {
                if (i >= 0 && i < Groups.Count)
                {
                    var group = Groups[i].ToString();
                    int index = group.IndexOf('\n');
                    if (index >= 0)
                    {
                        var index2 = group.IndexOf('\n', index + 1);
                        if (index2 > index)
                            group = group.Substring(index, index2 - index).Trim();
                        else
                            group = group.Substring(index).Trim();
                    }
                    Debug.WriteLine("{0}-{1}: {2}", Groups[i].FirstLine, Groups[i].LastLine, group);
                }
            }
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
                        group.MarkForEvaluation();

                        // also flag the output of this group for possible chaining
                        foreach (var variable in group.Modifies)
                            affectedVariables.Add(variable);
                    }
                    else if (group.Modifies.Any(v => affectedVariables.Contains(v)))
                    {
                        // this group could modify one of the affected variables.
                        // any time a variable is potentially modified, we have to fully re-evaluate it
                        group.MarkForEvaluation();
                    }
                }

                // if any new items were added to the affected variables list, go through again and check
                // for new stuff to flag
            } while (affectedVariables.Count > count);
        }

        public bool HasEvaluationErrors
        {
            get {  return _evaluationErrors.Count > 0; }
        }

        public void AddEvaluationError(ParseErrorExpression error)
        {
            _evaluationErrors.Add(error);
        }

        public void ResetErrors()
        {
            _evaluationErrors.Clear();
        }

        public IEnumerable<ParseErrorExpression> Errors
        {
            get
            {
                foreach (var group in Groups)
                {
                    foreach (var error in group.ParseErrors)
                        yield return error;
                }

                foreach (var error in _evaluationErrors)
                    yield return error;
            }
        }

        public bool GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            bool result = false;

            foreach (var group in GetGroupsForLine(line))
                result |= group.GetExpressionsForLine(expressions, line);

            foreach (var error in _evaluationErrors)
            {
                ParseErrorExpression mostSignificantError = null;
                var scan = error;
                do
                {
                    if (scan.Line <= line && scan.EndLine >= line)
                    {
                        // scan is more significant than current error, use it
                        mostSignificantError = scan;
                    }
                    else if (mostSignificantError != null && scan.Line >= mostSignificantError.Line && scan.EndLine < mostSignificantError.EndLine)
                    {
                        // scan is more significant than current error, but not part of line, ignore it
                        mostSignificantError = null;
                        break;
                    }

                    scan = scan.InnerError;
                } while (scan != null);

                if (mostSignificantError != null)
                {
                    if (!expressions.Contains(mostSignificantError))
                        expressions.Add(mostSignificantError);

                    result = true;
                }
            }

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