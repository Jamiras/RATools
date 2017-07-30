
using System.Text;
namespace RATools.Parser.Internal
{
    internal class MathematicExpression : ExpressionBase
    {
        public MathematicExpression(ExpressionBase left, MathematicOperation operation, ExpressionBase right)
            : base(ExpressionType.Mathematic)
        {
            Left = left;
            Operation = operation;
            Right = right;
        }

        public ExpressionBase Left { get; private set; }
        public MathematicOperation Operation { get; private set; }
        public ExpressionBase Right { get; private set; }

        internal override void AppendString(StringBuilder builder)
        {
            Left.AppendString(builder);
            builder.Append(' ');

            switch (Operation)
            {
                case MathematicOperation.Add:
                    builder.Append('+');
                    break;                
                case MathematicOperation.Subtract:
                    builder.Append('-');
                    break;                
                case MathematicOperation.Multiply:
                    builder.Append('*');
                    break;
                case MathematicOperation.Divide:
                    builder.Append('/');
                    break;
            }

            builder.Append(' ');
            Right.AppendString(builder);
        }

        internal override ExpressionBase Rebalance()
        {
            var mathematicRight = Right as MathematicExpression;
            if (mathematicRight != null)
            {
                if (Operation == MathematicOperation.Multiply || Operation == MathematicOperation.Divide)
                {
                    if (mathematicRight.Operation == MathematicOperation.Add || mathematicRight.Operation == MathematicOperation.Subtract)
                    {
                        var newLeft = new MathematicExpression(this, Operation, mathematicRight.Left);
                        return new MathematicExpression(newLeft, mathematicRight.Operation, mathematicRight.Right);
                    }
                }
            }

            var comparisonRight = Right as ComparisonExpression;
            if (comparisonRight != null)
            {
                Right = comparisonRight.Left;
                comparisonRight.Left = this;
                return comparisonRight;
            }

            var conditionalRight = Right as ConditionalExpression;
            if (conditionalRight != null)
            {
                Right = conditionalRight.Left;
                conditionalRight.Left = this;
                return conditionalRight;
            }

            return base.Rebalance();
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

            var integerLeft = left as IntegerConstantExpression;
            var integerRight = right as IntegerConstantExpression;

            switch (Operation)
            {
                case MathematicOperation.Add:
                    if (integerLeft != null && integerRight != null)
                    {
                        result = new IntegerConstantExpression(integerLeft.Value + integerRight.Value);
                        return true;
                    }
                   
                    var stringLeft = left as StringConstantExpression;
                    var stringRight = right as StringConstantExpression;
                    if (stringLeft != null)
                    {
                        if (stringRight != null)
                        {
                            result = new StringConstantExpression(stringLeft.Value + stringRight.Value);
                            return true;
                        }

                        if (integerRight != null)
                        {
                            result = new StringConstantExpression(stringLeft.Value + integerRight.Value.ToString());
                            return true;
                        }
                    }
                    else if (stringRight != null)
                    {
                        if (integerLeft != null)
                        {
                            result = new StringConstantExpression(integerLeft.Value.ToString() + stringRight.Value);
                            return true;
                        }
                    }
                    break;

                case MathematicOperation.Subtract:
                    if (integerLeft != null && integerRight != null)
                    {
                        result = new IntegerConstantExpression(integerLeft.Value - integerRight.Value);
                        return true;
                    }
                    break;

                case MathematicOperation.Multiply:
                    if (integerLeft != null && integerRight != null)
                    {
                        result = new IntegerConstantExpression(integerLeft.Value * integerRight.Value);
                        return true;
                    }
                    break;

                case MathematicOperation.Divide:
                    if (integerLeft != null && integerRight != null)
                    {
                        result = new IntegerConstantExpression(integerLeft.Value / integerRight.Value);
                        return true;
                    }
                    break;
            }

            var mathematic = new MathematicExpression(left, Operation, right);
            mathematic.Line = Line;
            mathematic.Column = Column;
            result = mathematic;
            return true;
        }
    }

    public enum MathematicOperation
    {
        None = 0,
        Add,
        Subtract,
        Multiply,
        Divide,
    }
}
