using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class ReturnExpression : ExpressionBase, INestedExpressions
    {
        public ReturnExpression(ExpressionBase value)
            : base(ExpressionType.Return)
        {
            Value = value;

            Line = value.Line;
            Column = value.Column;
            EndLine = value.EndLine;
            EndColumn = value.EndColumn;
        }

        public ReturnExpression(KeywordExpression keyword, ExpressionBase value)
            : this(value)
        {
            _keyword = keyword;
            Line = keyword.Line;
            Column = keyword.Column;
        }

        internal KeywordExpression Keyword
        {
            get { return _keyword; }
        }
        private readonly KeywordExpression _keyword;

        /// <summary>
        /// Gets the value to be returned.
        /// </summary>
        public ExpressionBase Value { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("return ");
            Value.AppendString(builder);
        }

        /// <summary>
        /// Replaces the variables in the expression with values from <paramref name="scope" />.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] The new expression containing the replaced variables.</param>
        /// <returns>
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ParseErrorExpression" />.
        /// </returns>
        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            ExpressionBase value;
            if (!Value.ReplaceVariables(scope, out value))
            {
                result = value;
                return false;
            }

            result = new ReturnExpression(value);
            CopyLocation(result);
            return true;
        }

        /// <summary>
        /// Determines whether the specified <see cref="ReturnExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="ReturnExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="ReturnExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = (ReturnExpression)obj;
            return Value == that.Value;
        }

        bool INestedExpressions.GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            if (_keyword != null && _keyword.Line == line)
                expressions.Add(_keyword);

            return ExpressionGroup.GetExpressionsForLine(expressions, new[] { Value }, line);
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            var nested = Value as INestedExpressions;
            if (nested != null)
                nested.GetDependencies(dependencies);
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
        }
    }
}
