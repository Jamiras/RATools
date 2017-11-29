using System.Collections.Generic;
using System.Text;
using Jamiras.Components;

namespace RATools.Parser.Internal
{
    internal class IfExpression : ExpressionBase
    {
        public IfExpression(ExpressionBase condition)
            : base(ExpressionType.If)
        {
            Condition = condition;
            Expressions = new List<ExpressionBase>();
            ElseExpressions = new List<ExpressionBase>();
        }

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
        internal new static ExpressionBase Parse(PositionalTokenizer tokenizer)
        {
            ExpressionBase.SkipWhitespace(tokenizer);

            var condition = ExpressionBase.Parse(tokenizer);
            if (condition.Type == ExpressionType.ParseError)
                return condition;

            if (condition.Type != ExpressionType.Conditional && condition.Type != ExpressionType.Comparison)
                return new ParseErrorExpression("Expected conditional statement following if");

            var ifExpression = new IfExpression(condition);

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
    }
}
