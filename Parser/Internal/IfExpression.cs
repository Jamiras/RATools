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

        public ExpressionBase Condition { get; private set; }
        public ICollection<ExpressionBase> Expressions { get; private set; }
        public ICollection<ExpressionBase> ElseExpressions { get; private set; }

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

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("if (");
            Condition.AppendString(builder);
            builder.Append(')');
        }
    }
}
