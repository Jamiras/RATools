using System.Text;

namespace RATools.Parser.Internal
{
    internal class ConditionalExpression : ExpressionBase
    {
        public ConditionalExpression(ExpressionBase left, ConditionalOperation operation, ExpressionBase right)
            : base(ExpressionType.Conditional)
        {
            Left = left;
            Operation = operation;
            Right = right;
        }

        public ExpressionBase Left { get; internal set; }
        public ConditionalOperation Operation { get; private set; }
        public ExpressionBase Right { get; private set; }

        internal override void AppendString(StringBuilder builder)
        {
            if (Operation == ConditionalOperation.Not)
            {
                builder.Append('!');
                Right.AppendString(builder);
                return;
            }

            Left.AppendString(builder);
            builder.Append(' ');

            switch (Operation)
            {
                case ConditionalOperation.And:
                    builder.Append("&&");
                    break;
                case ConditionalOperation.Or:
                    builder.Append("||");
                    break;
                case ConditionalOperation.Not:
                    builder.Append('!');
                    break;
            }

            builder.Append(' ');
            Right.AppendString(builder);
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            ExpressionBase left = null;
            if (Left != null && !Left.ReplaceVariables(scope, out left))
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

            var condition = new ConditionalExpression(left, Operation, right);
            condition.Line = Line;
            condition.Column = Column;
            result = condition;
            return true;
        }

        public override bool IsTrue(InterpreterScope scope, out ParseErrorExpression error)
        {
            bool result = Left.IsTrue(scope, out error);
            if (error != null)
                return false;

            switch (Operation)
            {
                case ConditionalOperation.And:
                    if (result)
                        result = Right.IsTrue(scope, out error);
                    break;

                case ConditionalOperation.Or:
                    if (!result)
                        result = Right.IsTrue(scope, out error);
                    break;

                case ConditionalOperation.Not:
                    result = !result;
                    break;
            }

            return result;
        }
    }

    public enum ConditionalOperation
    {
        None = 0,
        And,
        Or,
        Not,
    }
}
