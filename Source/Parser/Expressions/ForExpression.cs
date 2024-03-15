using Jamiras.Components;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class ForExpression : ExpressionBase, INestedExpressions, IExecutableExpression
    {
        public ForExpression(VariableDefinitionExpression iteratorName, ExpressionBase range)
            : base(ExpressionType.For)
        {
            IteratorName = iteratorName;
            Range = range;
            Expressions = new List<ExpressionBase>();
        }

        private KeywordExpression _keywordFor, _keywordIn;

        /// <summary>
        /// Gets the name of the iterator variable.
        /// </summary>
        public VariableDefinitionExpression IteratorName { get; private set; }

        /// <summary>
        /// Gets the expression that defines the values for each iteration.
        /// </summary>
        public ExpressionBase Range { get; private set; }

        /// <summary>
        /// Gets the expressions to evaluate for each iteration.
        /// </summary>
        public ICollection<ExpressionBase> Expressions { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("for ");
            IteratorName.AppendString(builder);
            builder.Append(" in ");
            Range.AppendString(builder);
        }

        /// <summary>
        /// Parses a for loop.
        /// </summary>
        /// <remarks>
        /// Assumes the 'for' keyword has already been consumed.
        /// </remarks>
        internal static ExpressionBase Parse(PositionalTokenizer tokenizer, int line = 0, int column = 0)
        {
            SkipWhitespace(tokenizer);
            var keywordFor = new KeywordExpression("for", line, column);

            line = tokenizer.Line;
            column = tokenizer.Column;
            var iteratorName = tokenizer.ReadIdentifier();
            if (iteratorName.IsEmpty)
                return ParseError(tokenizer, "Invalid function name", line, column);
            var iterator = new VariableDefinitionExpression(iteratorName.ToString(), line, column);

            SkipWhitespace(tokenizer);

            line = tokenizer.Line;
            column = tokenizer.Column;
            if (!tokenizer.Match("in"))
                return ParseError(tokenizer, "Expected 'in' after loop variable");
            var keywordIn = new KeywordExpression("in", line, column);

            var range = ExpressionBase.Parse(tokenizer);
            if (range.Type == ExpressionType.Error)
                return range;

            var loop = new ForExpression(iterator, range);

            var error = ParseStatementBlock(tokenizer, loop.Expressions);
            if (error != null)
            {
                var expressionTokenizer = tokenizer as ExpressionTokenizer;
                if (expressionTokenizer == null)
                    return error;

                expressionTokenizer.AddError((ErrorExpression)error);
            }

            loop._keywordFor = keywordFor;
            loop._keywordIn = keywordIn;
            loop.Location = new TextRange(keywordFor.Location.Start, tokenizer.Location);

            return loop;
        }

        /// <summary>
        /// Determines whether the specified <see cref="ForExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="ForExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="ForExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as ForExpression;
            return that != null && IteratorName == that.IteratorName && Range == that.Range && ExpressionsEqual(Expressions, that.Expressions);
        }

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                if (_keywordFor != null)
                    yield return _keywordFor;
                if (IteratorName != null)
                    yield return IteratorName;
                if (_keywordIn != null)
                    yield return _keywordIn;

                yield return Range;

                foreach (var expression in Expressions)
                    yield return expression;
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            var nested = Range as INestedExpressions;
            if (nested != null)
                nested.GetDependencies(dependencies);

            foreach (var expression in Expressions)
            {
                nested = expression as INestedExpressions;
                if (nested != null)
                    nested.GetDependencies(dependencies);
            }

            dependencies.Remove(IteratorName.Name);
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
            foreach (var expression in Expressions)
            {
                var nested = expression as INestedExpressions;
                if (nested != null)
                    nested.GetModifications(modifies);
            }
        }

        public ErrorExpression Execute(InterpreterScope scope)
        {
            ExpressionBase range;
            if (!Range.ReplaceVariables(scope, out range))
                return (ErrorExpression)range;

            var iterableExpression = range as IIterableExpression;
            if (iterableExpression == null)
                return new ErrorExpression("Cannot iterate over " + Range.Type.ToLowerString(), Range);

            var iterator = IteratorName;
            var iteratorScope = new InterpreterScope(scope);
            var iteratorVariable = new VariableExpression(iterator.Name);

            foreach (var entry in iterableExpression.IterableExpressions())
            {
                iteratorScope.Context = new AssignmentExpression(iteratorVariable, entry);

                ExpressionBase key;
                if (!entry.ReplaceVariables(iteratorScope, out key))
                    return (ErrorExpression)key;

                var loopScope = new InterpreterScope(scope);
                loopScope.DefineVariable(iterator, key);

                var error = AchievementScriptInterpreter.Evaluate(Expressions, loopScope);
                if (error != null)
                    return error;

                if (loopScope.IsComplete)
                {
                    if (loopScope.ReturnValue != null)
                    {
                        scope.ReturnValue = loopScope.ReturnValue;
                        scope.IsComplete = true;
                    }
                    break;
                }
            }

            return null;
        }
    }

    internal interface IIterableExpression
    {
        IEnumerable<ExpressionBase> IterableExpressions();
    }
}
