using Jamiras.Components;
using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class ForExpression : ExpressionBase, INestedExpressions
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
            ExpressionBase.SkipWhitespace(tokenizer);
            var keywordFor = new KeywordExpression("for", line, column);

            line = tokenizer.Line;
            column = tokenizer.Column;
            var iteratorName = tokenizer.ReadIdentifier();
            if (iteratorName.IsEmpty)
                return ParseError(tokenizer, "Invalid function name", line, column);
            var iterator = new VariableDefinitionExpression(iteratorName.ToString(), line, column);

            ExpressionBase.SkipWhitespace(tokenizer);

            line = tokenizer.Line;
            column = tokenizer.Column;
            if (!tokenizer.Match("in"))
                return ParseError(tokenizer, "Expected 'in' after loop variable");
            var keywordIn = new KeywordExpression("in", line, column);

            var range = ExpressionBase.Parse(tokenizer);
            if (range.Type == ExpressionType.ParseError)
                return range;

            var loop = new ForExpression(iterator, range);

            var error = ExpressionBase.ParseStatementBlock(tokenizer, loop.Expressions);
            if (error != null)
                return error;

            loop._keywordFor = keywordFor;
            loop._keywordIn = keywordIn;
            loop.Line = keywordFor.Line;
            loop.Column = keywordFor.Column;
            loop.EndLine = tokenizer.Line;
            loop.EndColumn = tokenizer.Column;

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
            var that = (ForExpression)obj;
            return IteratorName == that.IteratorName && Range == that.Range && Expressions == that.Expressions;
        }

        bool INestedExpressions.GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            if (_keywordFor != null && _keywordFor.Line == line)
                expressions.Add(_keywordFor);
            if (IteratorName.Line == line)
                expressions.Add(IteratorName);
            if (_keywordIn != null && _keywordIn.Line == line)
                expressions.Add(_keywordIn);

            if (Range.Line == line)
            {
                var nestedExpressions = Range as INestedExpressions;
                if (nestedExpressions != null)
                    nestedExpressions.GetExpressionsForLine(expressions, line);
                else
                    expressions.Add(Range);
            }

            return ExpressionGroup.GetExpressionsForLine(expressions, Expressions, line);
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
    }
}
