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

        private List<ParseErrorExpression> _parseErrors;
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

        public IEnumerable<ParseErrorExpression> ParseErrors
        {
            get
            {
                if (_parseErrors != null)
                    return _parseErrors;

                return Enumerable.Empty<ParseErrorExpression>();
            }
        }

        public void AddParseError(ParseErrorExpression error)
        {
            if (_parseErrors == null)
                _parseErrors = new List<ParseErrorExpression>();

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
                if (previous.Line < expression.Line)
                    break;
                if (previous.Line == expression.Line && previous.Column < expression.Column)
                    break;

                --insertAt;
            }

            _expressions.Insert(insertAt, expression);
        }

        internal ExpressionGroup ExtractTrailingComments(ExpressionBase expression)
        {
            if (_expression != null)
            {
                if (_expression.Line < expression.EndLine)
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
            while (index > 0 && _expressions[index - 1].Line >= expression.EndLine)
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
                if (enumerator.Current.Line < FirstLine)
                    FirstLine = enumerator.Current.Line;
                if (enumerator.Current.EndLine > LastLine)
                    LastLine = enumerator.Current.EndLine;
            }

            enumerator = ParseErrors.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Line < FirstLine)
                    FirstLine = enumerator.Current.Line;
                if (enumerator.Current.EndLine > LastLine)
                    LastLine = enumerator.Current.EndLine;
            }
        }

        public bool GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            bool result = GetExpressionsForLine(expressions, Expressions, line);

            foreach (var error in ParseErrors)
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
                if (nested != null && nested.NestedExpressions.Any())
                {
                    if (!GetExpressionsForLine(matchingExpressions, nested.NestedExpressions, line))
                        return false;
                }
                else
                {
                    matchingExpressions.Add(expression);
                }
            }

            return true;
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

        public void AdjustLines(int amount)
        {
            FirstLine += amount;
            LastLine += amount;

            foreach (var expression in Expressions)
                AdjustLines(expression, amount);

            foreach (var expression in ParseErrors)
                AdjustLines(expression, amount);
        }

        private static void AdjustLines(ExpressionBase expression, int amount)
        {
            expression.AdjustLines(amount);

            var nested = expression as INestedExpressions;
            if (nested != null)
            {
                foreach (var nestedExpression in nested.NestedExpressions)
                    AdjustLines(nestedExpression, amount);
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
