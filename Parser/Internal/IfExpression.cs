using System.Collections.Generic;
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

        private KeywordExpression _keyword;

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
            if (condition.Type == ExpressionType.ParseError)
                return condition;

            if (condition.Type != ExpressionType.Conditional && condition.Type != ExpressionType.Comparison)
                return ParseError(tokenizer, "Expected conditional statement following if", condition);

            var ifExpression = new IfExpression(condition);
            ifExpression._keyword = new KeywordExpression("if", line, column);

            var error = ExpressionBase.ParseStatementBlock(tokenizer, ifExpression.Expressions);
            if (error != null)
                return error;

            ExpressionBase.SkipWhitespace(tokenizer);

            if (tokenizer.Match("else"))
            {
                error = ExpressionBase.ParseStatementBlock(tokenizer, ifExpression.ElseExpressions);
                if (error != null)
                    return error;
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
            builder.Append(')');
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
            var that = (IfExpression)obj;
            return Condition == that.Condition && Expressions == that.Expressions && ElseExpressions == that.ElseExpressions;
        }

        bool INestedExpressions.GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            if (_keyword != null && _keyword.Line == line)
                expressions.Add(_keyword);

            if (!ExpressionGroup.GetExpressionsForLine(expressions, new[] { Condition }, line))
                return false;

            if (!ExpressionGroup.GetExpressionsForLine(expressions, Expressions, line))
                return false;

            return ExpressionGroup.GetExpressionsForLine(expressions, ElseExpressions, line);
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
