using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jamiras.Components;

namespace RATools.Parser.Internal
{
    internal class IfExpression : ExpressionBase, INestedExpressions
    {
        public IfExpression(ExpressionBase condition)
            : base(ExpressionType.If)
        {
            Condition = condition;
            Expressions = new List<ExpressionBase>();
            ElseExpressions = new List<ExpressionBase>();
        }

        private KeywordExpression _keyword, _elseKeyword;

        /// <summary>
        /// Gets the condition expression.
        /// </summary>
        public ExpressionBase Condition { get; private set; }

        /// <summary>
        /// Gets the expressions to execute if the condition evaluates true.
        /// </summary>
        public ICollection<ExpressionBase> Expressions { get; private set; }

        /// <summary>
        /// Gets the expressions to execute if the condition evaluates false.
        /// </summary>
        public ICollection<ExpressionBase> ElseExpressions { get; private set; }

        /// <summary>
        /// Parses a if definition.
        /// </summary>
        /// <remarks>
        /// Assumes the 'if' keyword has already been consumed.
        /// </remarks>
        internal static ExpressionBase Parse(PositionalTokenizer tokenizer, int line = 0, int column = 0)
        {
            ExpressionBase.SkipWhitespace(tokenizer);

            var condition = ExpressionBase.Parse(tokenizer);
            if (condition.Type == ExpressionType.Error)
                return condition;

            var ifExpression = new IfExpression(condition);
            ifExpression._keyword = new KeywordExpression("if", line, column);

            var error = ExpressionBase.ParseStatementBlock(tokenizer, ifExpression.Expressions);
            if (error != null)
                return error;

            ExpressionBase.SkipWhitespace(tokenizer);

            if (tokenizer.MatchSubstring("else") == 4)
            {
                tokenizer.PushState();
                bool isElse = tokenizer.ReadIdentifier() == "else";
                tokenizer.PopState();

                if (isElse)
                {
                    ifExpression._elseKeyword = new KeywordExpression("else", tokenizer.Line, tokenizer.Column);
                    tokenizer.Advance(4);

                    error = ExpressionBase.ParseStatementBlock(tokenizer, ifExpression.ElseExpressions);
                    if (error != null)
                        return error;
                }
            }

            return ifExpression;
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("if (");
            Condition.AppendString(builder);
            builder.Append(") { ... }");

            if (ElseExpressions.Count > 0)
                builder.Append(" else { ... }");
        }

        /// <summary>
        /// Determines whether the specified <see cref="IfExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="IfExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="IfExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as IfExpression;
            return that != null && Condition == that.Condition && ExpressionsEqual(Expressions, that.Expressions) && 
                ExpressionsEqual(ElseExpressions, that.ElseExpressions);
        }

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                if (_keyword != null)
                    yield return _keyword;

                if (Condition != null)
                    yield return Condition;

                foreach (var expression in Expressions)
                    yield return expression;

                if (_elseKeyword != null)
                    yield return _elseKeyword;

                foreach (var expression in ElseExpressions)
                    yield return expression;
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            var nested = Condition as INestedExpressions;
            if (nested != null)
                nested.GetDependencies(dependencies);

            foreach (var expression in Expressions)
            {
                nested = expression as INestedExpressions;
                if (nested != null)
                    nested.GetDependencies(dependencies);
            }

            foreach (var expression in ElseExpressions)
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

            foreach (var expression in ElseExpressions)
            {
                var nested = expression as INestedExpressions;
                if (nested != null)
                    nested.GetModifications(modifies);
            }
        }
    }
}
