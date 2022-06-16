using RATools.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class ExpressionGroup
    {
        public ExpressionGroup()
        {
        }

        private List<ErrorExpression> _parseErrors;
        private List<ExpressionBase> _expressions;
        private ExpressionBase _expression;
        private HashSet<string> _dependencies;
        private HashSet<string> _modifies;

        public ICollection<Achievement> GeneratedAchievements { get; set; }
        public ICollection<Leaderboard> GeneratedLeaderboards { get; set; }
        public RichPresenceBuilder GeneratedRichPresence { get; set; }

        public bool NeedsEvaluated { get; private set; }

        public void MarkForEvaluation()
        {
            if (_parseErrors != null)
            {
                // don't evaluate if parse errors are present
                NeedsEvaluated = false;
            }
            else
            {
                // if there are no expressions, or only comments, we don't need to evaluate
                NeedsEvaluated = Expressions.Any(e => e.Type != ExpressionType.Comment);
            }
        }

        public void MarkEvaluated()
        {
            NeedsEvaluated = false;
        }

        public IEnumerable<ErrorExpression> ParseErrors
        {
            get
            {
                if (_parseErrors != null)
                    return _parseErrors;

                return Enumerable.Empty<ErrorExpression>();
            }
        }

        public void AddParseError(ErrorExpression error)
        {
            if (_parseErrors == null)
                _parseErrors = new List<ErrorExpression>();

            _parseErrors.Add(error);
        }

        public IEnumerable<ExpressionBase> Expressions
        {
            get
            {
                if (_expressions != null)
                    return _expressions;

                if (_expression != null)
                    return new[] { _expression };

                return Enumerable.Empty<ExpressionBase>();
            }
        }

        public void AddExpression(ExpressionBase expression)
        {
            if (_expressions == null)
            {
                if (_expression == null)
                {
                    _expression = expression;
                    return;
                }

                _expressions = new List<ExpressionBase>();
                _expressions.Add(_expression);
                _expression = null;
            }

            int insertAt = _expressions.Count();
            while (insertAt > 0)
            {
                var previous = _expressions[insertAt - 1];
                if (previous.Location.Start.Line < expression.Location.Start.Line)
                    break;

                if (previous.Location.Start.Line == expression.Location.Start.Line &&
                    previous.Location.Start.Column < expression.Location.Start.Column)
                    break;

                --insertAt;
            }

            _expressions.Insert(insertAt, expression);
        }

        internal ExpressionGroup ExtractTrailingComments(ExpressionBase expression)
        {
            if (_expression != null)
            {
                if (_expression.Location.Start.Line < expression.Location.End.Line)
                    return null;
                // ASSERT: comment must be the last thing on a line, so we don't have to check columns

                var newGroup = new ExpressionGroup();
                newGroup._expression = _expression;
                _expression = null;
                return newGroup;
            }

            if (_expressions == null)
                return null;

            int index = _expressions.Count;
            while (index > 0 && _expressions[index - 1].Location.Start.Line >= expression.Location.End.Line)
                --index;

            if (index != _expressions.Count)
            {
                var newGroup = new ExpressionGroup();
                if (index == 1)
                {
                    _expression = _expressions[0];
                    _expressions.RemoveAt(0);
                    --index;
                }

                if (index == 0)
                {
                    newGroup._expressions = _expressions;
                    _expressions = null;
                    return newGroup;
                }

                newGroup._expressions = new List<ExpressionBase>();
                for (int i = index; i < _expressions.Count; ++i)
                    newGroup._expressions.Add(_expressions[i]);

                _expressions.RemoveRange(index, _expressions.Count - index);
                return newGroup;
            }

            return null;
        }

        public bool IsDependentOn(string name)
        {
            if (_dependencies == null)
                return false;

            return _dependencies.Contains(name);
        }

        public bool IsDependentOn(HashSet<string> names)
        {
            if (_dependencies != null)
            {
                foreach (var name in names)
                {
                    if (_dependencies.Contains(name))
                        return true;
                }
            }

            return false;
        }

        public IEnumerable<string> Modifies
        {
            get
            {
                if (_modifies != null)
                    return _modifies;

                return Enumerable.Empty<string>();
            }
        }

        public bool IsEmpty
        {
            get { return _expression == null && _expressions == null && _parseErrors == null; }
        }

        public int FirstLine { get; internal set; }
        public int LastLine { get; internal set; }

        public void UpdateMetadata()
        {
            if (!IsEmpty)
            {
                UpdateRange();

                var dependencies = new HashSet<string>();
                var modifies = new HashSet<string>();
                foreach (var expression in Expressions)
                {
                    var nested = expression as INestedExpressions;
                    if (nested != null)
                    {
                        nested.GetDependencies(dependencies);

                        // if any parse errors are present, this group will not be evaluated, so no modifications will be made
                        if (_parseErrors == null)
                            nested.GetModifications(modifies);
                    }
                }

                _dependencies = (dependencies.Count > 0) ? dependencies : null;
                _modifies = (modifies.Count > 0) ? modifies : null;
            }
        }

        private void UpdateRange()
        {
            FirstLine = Int32.MaxValue;
            LastLine = 0;

            var enumerator = Expressions.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Location.Start.Line < FirstLine)
                    FirstLine = enumerator.Current.Location.Start.Line;
                if (enumerator.Current.Location.End.Line > LastLine)
                    LastLine = enumerator.Current.Location.End.Line;
            }

            enumerator = ParseErrors.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Location.Start.Line < FirstLine)
                    FirstLine = enumerator.Current.Location.Start.Line;
                if (enumerator.Current.Location.End.Line > LastLine)
                    LastLine = enumerator.Current.Location.End.Line;
            }
        }

        public bool GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            GetExpressionsForLine(expressions, Expressions, line);

            foreach (var error in ParseErrors)
            {
                var innerError = error.InnermostError ?? error;
                if (innerError.Location.Start.Line <= line && innerError.Location.End.Line >= line)
                   expressions.Add(innerError);
            }

            return (expressions.Count > 0);
        }

        private static void GetExpressionsForLine(List<ExpressionBase> matchingExpressions, IEnumerable<ExpressionBase> expressions, int line)
        { 
            foreach (var expression in expressions)
            {
                if (expression.Location.End.Line < line)
                    continue;

                if (expression.Location.Start.Line > line)
                    continue;

                var nested = expression as INestedExpressions;
                if (nested != null && nested.NestedExpressions.Any())
                    GetExpressionsForLine(matchingExpressions, nested.NestedExpressions, line);
                else
                    matchingExpressions.Add(expression);
            }
        }

        public bool ExpressionsMatch(ExpressionGroup that)
        {
            if (_expression != null)
                return (_expression == that._expression);
            if (that._expression != null)
                return false;

            if (_expressions == null)
                return (that._expressions == null);
            if (that._expressions == null)
                return false;

            if (_expressions.Count != that._expressions.Count)
                return false;

            for (int i = 0; i < _expressions.Count; ++i)
            {
                if (_expressions[i] != that._expressions[i])
                    return false;
            }

            return true;
        }

        public void ReplaceExpressions(ExpressionGroup that, bool updateDependencies)
        {
            _expression = that._expression;
            _expressions = that._expressions;
            _parseErrors = that._parseErrors;

            if (updateDependencies)
            {
                // update everything
                UpdateMetadata();
            }
            else if (that.FirstLine != 0)
            {
                // copy everything
                FirstLine = that.FirstLine;
                LastLine = that.LastLine;

                _dependencies = that._dependencies;
                _modifies = that._modifies;
            }
            else
            {
                // only update the range, assume the Dependencies and Modifies collections are correct
                UpdateRange();
            }
        }

        public override string ToString()
        {
            if (IsEmpty)
                return "empty";

            var builder = new StringBuilder();
            builder.Append(FirstLine);
            if (FirstLine != LastLine)
            {
                builder.Append('-');
                builder.Append(LastLine);
            }
            builder.Append(": ");

            if (_expression != null || _expressions != null)
            {
                foreach (var expression in Expressions)
                {
                    builder.AppendLine();
                    expression.AppendString(builder);
                }
            }
            else
            {
                foreach (var expression in ParseErrors)
                {
                    builder.AppendLine();
                    expression.AppendString(builder);
                }
            }

            return builder.ToString();
        }
    }

    internal interface INestedExpressions
    {
        /// <summary>
        /// Gets the child expressions.
        /// </summary>
        IEnumerable<ExpressionBase> NestedExpressions { get; }

        /// <summary>
        /// Gets the name of any variables or functions this expression depends on.
        /// </summary>
        void GetDependencies(HashSet<string> dependencies);

        /// <summary>
        /// Gets any variables modified by this expression.
        /// </summary>
        void GetModifications(HashSet<string> modifies);
    }
}
