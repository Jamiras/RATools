using System.Text;

namespace RATools.Parser.Internal
{
    internal class ComparisonExpression : ExpressionBase
    {
        public ComparisonExpression(ExpressionBase left, ComparisonOperation operation, ExpressionBase right)
            : base(ExpressionType.Comparison)
        {
            Left = left;
            Operation = operation;
            Right = right;
        }

        public ExpressionBase Left { get; internal set; }
        public ComparisonOperation Operation { get; private set; }
        public ExpressionBase Right { get; private set; }

        internal override void AppendString(StringBuilder builder)
        {
            Left.AppendString(builder);
            builder.Append(' ');

            switch (Operation)
            {
                case ComparisonOperation.Equal:
                    builder.Append("==");
                    break;
                case ComparisonOperation.NotEqual:
                    builder.Append("!=");
                    break;
                case ComparisonOperation.LessThan:
                    builder.Append('<');
                    break;
                case ComparisonOperation.LessThanOrEqual:
                    builder.Append("<=");
                    break;
                case ComparisonOperation.GreaterThan:
                    builder.Append('>');
                    break;
                case ComparisonOperation.GreaterThanOrEqual:
                    builder.Append(">=");
                    break;
            }

            builder.Append(' ');
            Right.AppendString(builder);
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            ExpressionBase left;
            if (!Left.ReplaceVariables(scope, out left))
            {
                result = left;
                return false;
            }

            ExpressionBase right;
            if (!Right.ReplaceVariables(scope, out right))
            {
                result = right;
                return false;
            }

            var comparison = new ComparisonExpression(left, Operation, right);
            comparison.Line = Line;
            comparison.Column = Column;
            result = comparison;
            return true;
        }

        internal override ExpressionBase Rebalance()
        {
            var conditionalRight = Right as ConditionalExpression;
            if (conditionalRight != null)
            {
                Right = conditionalRight.Left;
                conditionalRight.Left = this;
                return conditionalRight;
            }

            return base.Rebalance();
        }
    }

    public enum ComparisonOperation
    {
        None = 0,
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
    }
}
