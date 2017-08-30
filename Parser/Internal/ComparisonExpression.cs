using System;
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

        public override bool IsTrue(InterpreterScope scope, out ParseErrorExpression error)
        {
            ExpressionBase left, right;
            if (!Left.ReplaceVariables(scope, out left))
            {
                error = left as ParseErrorExpression;
                return false;
            }

            if (!Right.ReplaceVariables(scope, out right))
            {
                error = right as ParseErrorExpression;
                return false;
            }

            error = null;

            var integerLeft = left as IntegerConstantExpression;
            if (integerLeft != null)
            {
                var integerRight = right as IntegerConstantExpression;
                if (integerRight == null)
                    return false;

                switch (Operation)
                {
                    case ComparisonOperation.Equal:
                        return integerLeft.Value == integerRight.Value;
                    case ComparisonOperation.NotEqual:
                        return integerLeft.Value != integerRight.Value;
                    case ComparisonOperation.GreaterThan:
                        return integerLeft.Value > integerRight.Value;
                    case ComparisonOperation.GreaterThanOrEqual:
                        return integerLeft.Value >= integerRight.Value;
                    case ComparisonOperation.LessThan:
                        return integerLeft.Value < integerRight.Value;
                    case ComparisonOperation.LessThanOrEqual:
                        return integerLeft.Value <= integerRight.Value;
                    default:
                        return false;
                }
            }

            var stringLeft = left as StringConstantExpression;
            if (stringLeft != null)
            {
                var stringRight = right as StringConstantExpression;
                if (stringRight == null)
                    return false;

                switch (Operation)
                {
                    case ComparisonOperation.Equal:
                        return stringLeft.Value == stringRight.Value;
                    case ComparisonOperation.NotEqual:
                        return stringLeft.Value != stringRight.Value;
                    case ComparisonOperation.GreaterThan:
                        return String.Compare(stringLeft.Value, stringRight.Value) > 0;
                    case ComparisonOperation.GreaterThanOrEqual:
                        return String.Compare(stringLeft.Value, stringRight.Value) >= 0;
                    case ComparisonOperation.LessThan:
                        return String.Compare(stringLeft.Value, stringRight.Value) < 0;
                    case ComparisonOperation.LessThanOrEqual:
                        return String.Compare(stringLeft.Value, stringRight.Value) <= 0;
                    default:
                        return false;
                }
            }

            return false;
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
