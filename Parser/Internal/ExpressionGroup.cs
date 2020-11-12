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

        public IEnumerable<ParseErrorExpression> Errors
        {
            get
            {
                if (_errors != null)
                    return _errors;

                return Enumerable.Empty<ParseErrorExpression>();
            }
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
            if (_expressions != null)
            {
                _expressions.Add(expression);
            }
            else if (_expression != null)
            {
                _expressions = new List<ExpressionBase>();
                _expressions.Add(_expression);
                _expression = null;
                _expressions.Add(expression);
            }
            else
            {
                _expression = expression;
            }
        }

        public bool IsDependentOn(string name)
        {
            if (_dependencies == null)
                return false;

            return _dependencies.Contains(name);
        }

        public bool IsEmpty
        {
            get { return _expression == null && _expressions == null; }
        }

        public int FirstLine { get; private set; }
        public int LastLine { get; private set; }

        public void UpdateMetadata()
        {
            if (!IsEmpty)
            {
                FirstLine = Expressions.First().Line;
                LastLine = Expressions.Last().EndLine;
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

        public void Merge(ExpressionGroup from)
        {
            foreach (var expression in from.Expressions)
                AddExpression(expression);

            foreach (var error in from.Errors)
                AddError(error);
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
        bool GetExpressionsForLine(List<ExpressionBase> expressions, int line);
    }
}
