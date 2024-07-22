//#define DEBUG_UPDATE

using Jamiras.Components;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class ExpressionGroupCollection
    {
        public ExpressionGroupCollection()
        {
            _groups = new List<ExpressionGroup>();
            _evaluationErrors = new List<ErrorExpression>();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly List<ExpressionGroup> _groups;

        public IEnumerable<ExpressionGroup> Groups { get { return _groups; } }

        public InterpreterScope Scope { get; set; }

        private readonly List<ErrorExpression> _evaluationErrors;

        public void Parse(Tokenizer tokenizer)
        {
            _groups.Clear();

            var expressionTokenizer = new ExpressionTokenizer(tokenizer, null);
            ParseGroups(expressionTokenizer, _groups, CreateGroup);

            foreach (var group in _groups)
            {
                group.UpdateMetadata();
                group.MarkForEvaluation();
            }
        }

        protected virtual ExpressionGroup CreateGroup()
        {
            return new ExpressionGroup();
        }

        private class PlaceholderExpression : ExpressionBase
        {
            public PlaceholderExpression(TextLocation startLocation, TextLocation endLocation)
                : base(ExpressionType.None)
            {
                Location = new TextRange(startLocation, endLocation);
            }

            protected override bool Equals(ExpressionBase obj)
            {
                return obj is PlaceholderExpression && Equals(Location, obj.Location);
            }

            internal override void AppendString(StringBuilder builder)
            {
            }
        }

        private static void ParseGroups(ExpressionTokenizer tokenizer, List<ExpressionGroup> groups, Func<ExpressionGroup> createGroup)
        {
            while (tokenizer.NextChar != '\0')
            {
                // create a separate group for comments
                var newGroup = createGroup();
                groups.Add(newGroup);
                tokenizer.ChangeExpressionGroup(newGroup);
                ExpressionBase.SkipWhitespace(tokenizer);

                // if comments were found, start a new group
                if (!newGroup.IsEmpty)
                {
                    newGroup = createGroup();
                    groups.Add(newGroup);
                    tokenizer.ChangeExpressionGroup(newGroup);
                }

                if (tokenizer.NextChar == '\0')
                    break;

                var startLocation = tokenizer.Location;
                var expression = ExpressionBase.Parse(tokenizer);

                // sometimes parsing an expression will process trailing whitespace looking for a continuation character. 
                // move the trailing comments into a separate group. do not move the internal comments
                var commentGroup = newGroup.ExtractTrailingComments(expression);
                if (commentGroup != null)
                    groups.Add(commentGroup);

                if (expression.Type != ExpressionType.Error)
                    newGroup.AddExpression(expression);
                else
                    newGroup.AddExpression(new PlaceholderExpression(startLocation, tokenizer.Location));
            }

            var lastGroup = groups.LastOrDefault();
            if (lastGroup != null && lastGroup.IsEmpty)
                groups.RemoveAt(groups.Count - 1);
        }

        private static bool IsAtValidGroupStart(ExpressionTokenizer tokenizer)
        {
            if (Char.IsLetter(tokenizer.NextChar))
                return true;

            if (tokenizer.NextChar == '_')
                return true;

            if (tokenizer.MatchSubstring("//") == 2)
                return true;

            return false;
        }

        public bool Update(Tokenizer tokenizer, IEnumerable<int> affectedLines)
        {
            var expressionTokenizer = new ExpressionTokenizer(tokenizer, null);
            int groupStart = 0;

            // we can ignore everything before the first modified line
            if (affectedLines.Any())
            {
                var nextUpdatedLine = affectedLines.Min();
                LOG_UPDATE("Updating lines {0}-{1} (searching {2} groups)", nextUpdatedLine, affectedLines.Max(), _groups.Count);

                while (groupStart < _groups.Count && nextUpdatedLine > _groups[groupStart].LastLine)
                    ++groupStart;

                if (groupStart < _groups.Count)
                {
                    LOG_UPDATE("Found line {0} in group {1} (first line of group is {2})", nextUpdatedLine, groupStart, _groups[groupStart].FirstLine);
                    nextUpdatedLine = Math.Min(_groups[groupStart].FirstLine, nextUpdatedLine);
                }

                expressionTokenizer.PushState();
                expressionTokenizer.AdvanceToLine(nextUpdatedLine);

                if (groupStart > 0)
                {
                    // if the first character to be parsed is not a valid identifier character or comment token,
                    // the new content might need to be merged with the previous group.
                    bool needPreviousGroup;
                    if (Char.IsWhiteSpace(expressionTokenizer.NextChar))
                    {
                        expressionTokenizer.PushState();
                        expressionTokenizer.SkipWhitespace();
                        needPreviousGroup = !IsAtValidGroupStart(expressionTokenizer);
                        expressionTokenizer.PopState();
                    }
                    else
                    {
                        needPreviousGroup = !IsAtValidGroupStart(expressionTokenizer);
                    }

                    if (needPreviousGroup)
                    {
                        --groupStart;
                        nextUpdatedLine = _groups[groupStart].FirstLine;

                        LOG_UPDATE("Also processing group {0} (first line of group is {1})", groupStart, nextUpdatedLine);
                        expressionTokenizer.PopState();
                        expressionTokenizer.AdvanceToLine(nextUpdatedLine);
                    }
                }
            }
            else
            {
                LOG_UPDATE("Updating all lines ({0} groups)", _groups.Count);
            }

            LOG_GROUPS(groupStart - 2, groupStart + 2);

            // parse whatever is remaining
            var newGroups = new List<ExpressionGroup>();
            ParseGroups(expressionTokenizer, newGroups, CreateGroup);

            // attempt to match the end of the script
            int groupStop = _groups.Count;
            int newGroupStop = newGroups.Count;

            if (newGroupStop > 0)
            {
                while (groupStop > groupStart)
                {
                    var existingGroup = _groups[--groupStop];
                    var newGroup = newGroups[--newGroupStop];

                    if (!existingGroup.ExpressionsMatch(newGroup))
                    {
                        ++groupStop;
                        ++newGroupStop;
                        break;
                    }

                    var firstLine = existingGroup.FirstLine;
                    var lastLine = existingGroup.LastLine;

                    existingGroup.ReplaceExpressions(newGroup, false);
                    Scope.UpdateVariables(existingGroup.Modifies, newGroup);

                    var adjustment = existingGroup.FirstLine - firstLine;
                    if (adjustment != 0)
                    {
                        existingGroup.AdjustSourceLines(adjustment);

                        foreach (var error in _evaluationErrors)
                        {
                            var innerError = error;
                            while (innerError != null)
                            {
                                if (innerError.Location.Start.Line >= firstLine && innerError.Location.End.Line <= lastLine)
                                    innerError.AdjustLines(adjustment);

                                innerError = innerError.InnerError;
                            }
                        }
                    }

                    if (newGroupStop == 0)
                    {
                        if (groupStop == groupStart)
                        {
                            // no change detected
                            return false;
                        }

                        // groups were removed
                        break;
                    }
                }
            }

            // whatever is remaining will be swapped out.
            // capture any affected variables and remove associated evaluation errors
            var affectedVariables = new HashSet<string>();
            for (int i = groupStart; i < groupStop; ++i)
            {
                var group = _groups[i];
                foreach (var variable in group.Modifies)
                {
                    affectedVariables.Add(variable);
                    Scope.UndefineVariable(variable);
                    Scope.UndefineFunction(variable);
                }

                for (int j = _evaluationErrors.Count - 1; j >= 0; j--)
                {
                    var error = _evaluationErrors[j];
                    if (error.Location.End.Line >= group.FirstLine && error.Location.Start.Line <= group.LastLine)
                    {
                        _evaluationErrors.RemoveAt(j);
                    }
                    else if (error.InnerError != null)
                    {
                        error = error.InnermostError;
                        if (error.Location.End.Line >= group.FirstLine && error.Location.Start.Line <= group.LastLine)
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
            if (newGroupStop == 0)
            {
                if (groupStart < _groups.Count)
                {
                    LOG_UPDATE("Removing groups {0}-{1} (lines {2}-{3})",
                        groupStart, groupStop - 1, _groups[groupStart].FirstLine, _groups[groupStop - 1].LastLine);

                    _groups.RemoveRange(groupStart, groupStop - groupStart);
                }
            }
            else if (groupStop == groupStart)
            {
                LOG_UPDATE("Adding {0} groups (lines {1}-{2})",
                    newGroupStop, newGroups[0].FirstLine, newGroups[newGroupStop - 1].LastLine);

                _groups.InsertRange(groupStart, newGroups.Take(newGroupStop));
            }
            else
            {
                LOG_UPDATE("Replacing groups {0}-{1} (lines {2}-{3}) with {4} groups (lines {5}-{6})",
                    groupStart, groupStop - 1, _groups[groupStart].FirstLine, _groups[groupStop - 1].LastLine,
                    newGroupStop, newGroups[0].FirstLine, newGroups[newGroupStop - 1].LastLine);

                _groups.RemoveRange(groupStart, groupStop - groupStart);
                _groups.InsertRange(groupStart, newGroups.Take(newGroupStop));
            }

            LOG_GROUPS(groupStart - 2, groupStart + newGroupStop + 2);

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

        [Conditional("DEBUG_UPDATE")]
        private void LOG_UPDATE(string fmt, params object[] parameters)
        {
            Debug.WriteLine(fmt, parameters);
        }

        [Conditional("DEBUG_UPDATE")]
        private void LOG_GROUPS(int start, int end)
        {
            for (int i = start; i < end; ++i)
            {
                if (i >= 0 && i < _groups.Count)
                {
                    var group = _groups[i].ToString();
                    int index = group.IndexOf('\n');
                    if (index >= 0)
                    {
                        var index2 = group.IndexOf('\n', index + 1);
                        if (index2 > index)
                            group = group.Substring(index, index2 - index).Trim();
                        else
                            group = group.Substring(index).Trim();
                    }
                    LOG_UPDATE("{0}-{1}: {2}", _groups[i].FirstLine, _groups[i].LastLine, group);
                }
            }
        }

        private void FlagDependencies(HashSet<string> affectedVariables)
        {
            int count;
            do
            {
                count = affectedVariables.Count;

                foreach (var group in _groups)
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

        internal void AddEvaluationError(ErrorExpression error)
        {
            lock (_evaluationErrors)
            {
                _evaluationErrors.Add(error);
            }
        }

        internal void ResetErrors()
        {
            lock (_evaluationErrors)
            {
                _evaluationErrors.Clear();
            }
        }

        public IEnumerable<ErrorExpression> Errors
        {
            get
            {
                foreach (var group in _groups)
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

            lock (_evaluationErrors)
            {
                foreach (var error in _evaluationErrors)
                {
                    var unknownVariableError = error.InnermostError as UnknownVariableParseErrorExpression;
                    if (unknownVariableError != null && unknownVariableError.Location.Start.Line <= line &&
                        unknownVariableError.Location.End.Line >= line)
                    {
                        if (!expressions.Contains(unknownVariableError))
                            expressions.Add(unknownVariableError);

                        result = true;
                    }

                    ErrorExpression mostSignificantError = null;
                    var scan = error;
                    do
                    {
                        if (scan.Location.Start.Line <= line && scan.Location.End.Line >= line)
                        {
                            // scan is more significant than current error, use it
                            mostSignificantError = scan;
                        }
                        else if (mostSignificantError != null &&
                            scan.Location.Start.Line >= mostSignificantError.Location.Start.Line &&
                            scan.Location.End.Line < mostSignificantError.Location.End.Line)
                        {
                            // scan is more significant than current error, but not part of line, ignore it and the current error
                            mostSignificantError = null;
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
            }

            return result;
        }

        public IEnumerable<ExpressionGroup> GetGroupsForLine(int line)
        {
            int left = 0;
            int right = _groups.Count;

            while (left != right)
            {
                int mid = (left + right) / 2;
                var group = _groups[mid];
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
                    while (index >= left && index > 0 && _groups[index - 1].LastLine >= line)
                        index--;

                    while (index < right)
                    {
                        group = _groups[index++];
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
