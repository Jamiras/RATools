using RATools.Data;
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

        private List<ParseErrorExpression> _errors;
        private List<ExpressionBase> _expressions;
        private ExpressionBase _expression;
        private HashSet<string> _dependencies;
        private HashSet<string> _modifies;

        public ICollection<Achievement> GeneratedAchievements { get; set; }
        public ICollection<Leaderboard> GeneratedLeaderboards { get; set; }
        public RichPresenceBuilder GeneratedRichPresence { get; set; }

        public bool NeedsEvaluated { get; set; }

        public IEnumerable<ParseErrorExpression> Errors
        {
            get
            {
                if (_errors != null)
                    return _errors;

                return Enumerable.Empty<ParseErrorExpression>();
            }
        }

        public void ResetErrors()
        {
            _errors = null;
        }

        public void AddError(ParseErrorExpression error)
        {
            if (_errors == null)
                _errors = new List<ParseErrorExpression>();

            _errors.Add(error);
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
            get { return _expression == null && _expressions == null; }
        }

        public int FirstLine { get; internal set; }
        public int LastLine { get; internal set; }

        public void UpdateMetadata()
        {
            if (!IsEmpty)
            {
                var enumerator = Expressions.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    FirstLine = enumerator.Current.Line;
                    LastLine = enumerator.Current.EndLine;

                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current.Line < FirstLine)
                            FirstLine = enumerator.Current.Line;
                        if (enumerator.Current.EndLine > LastLine)
                            LastLine = enumerator.Current.EndLine;
                    }
                }

                var dependencies = new HashSet<string>();
                var modifies = new HashSet<string>();
                foreach (var expression in Expressions)
                {
                    var nested = expression as INestedExpressions;
                    if (nested != null)
                    {
                        nested.GetDependencies(dependencies);
                        nested.GetModifications(modifies);
                    }
                }

                _dependencies = (dependencies.Count > 0) ? dependencies : null;
                _modifies = (modifies.Count > 0) ? modifies : null;
            }
        }

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

        public void Merge(ExpressionGroup from)
        {
            foreach (var expression in from.Expressions)
                AddExpression(expression);

            foreach (var error in from.Errors)
                AddError(error);
        }

        public void AdjustLines(int amount)
        {
            FirstLine += amount;
            LastLine += amount;

            foreach (var expression in Expressions)
                AdjustLines(expression, amount);

            foreach (var expression in Errors)
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

            UpdateMetadata();

            var builder = new StringBuilder();
            builder.Append(FirstLine);
            if (FirstLine != LastLine)
            {
                builder.Append('-');
                builder.Append(LastLine);
            }
            builder.Append(": ");

            foreach (var expression in Expressions)
            {
                builder.AppendLine();
                expression.AppendString(builder);
            }

            return builder.ToString();
        }
    }

    internal interface INestedExpressions
    {
        IEnumerable<ExpressionBase> NestedExpressions { get; }

        void GetDependencies(HashSet<string> dependencies);

        void GetModifications(HashSet<string> modifies);
    }
}
