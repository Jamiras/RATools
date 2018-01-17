using System.Diagnostics;
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

        /// <summary>
        /// Gets the left side of the equation.
        /// </summary>
        public ExpressionBase Left { get; private set; }

        /// <summary>
        /// Gets the mathematic operation.
        /// </summary>
        public MathematicOperation Operation { get; private set; }

        /// <summary>
        /// Gets the right side of the equation.
        /// </summary>
        public ExpressionBase Right { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
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
                case MathematicOperation.Modulus:
                    builder.Append('%');
                    break;
            }

            builder.Append(' ');
            Right.AppendString(builder);
        }

        internal static MathematicPriority GetPriority(MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Add:
                case MathematicOperation.Subtract:
                    return MathematicPriority.Add;

                case MathematicOperation.Multiply:
                case MathematicOperation.Divide:
                case MathematicOperation.Modulus:
                    return MathematicPriority.Multiply;

                default:
                    return MathematicPriority.None;
            }
        }

        /// <summary>
        /// Rebalances this expression based on the precendence of operators.
        /// </summary>
        internal override ExpressionBase Rebalance()
        {
            if (!Right.IsLogicalUnit)
            {
                var mathematicRight = Right as MathematicExpression;
                if (mathematicRight != null)
                {
                    if (GetPriority(Operation) > GetPriority(mathematicRight.Operation))
                    {
                        var newLeft = new MathematicExpression(Left, Operation, mathematicRight.Left);
                        return new MathematicExpression(newLeft, mathematicRight.Operation, mathematicRight.Right);
                    }
                }

                var comparisonRight = Right as ComparisonExpression;
                if (comparisonRight != null)
                {
                    Right = comparisonRight.Left;
                    comparisonRight.Left = this.Rebalance();
                    return comparisonRight;
                }

                var conditionalRight = Right as ConditionalExpression;
                if (conditionalRight != null)
                {
                    Right = conditionalRight.Left;
                    conditionalRight.Left = this.Rebalance();
                    return conditionalRight;
                }
            }

            return base.Rebalance();
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

                case MathematicOperation.Modulus:
                    if (integerLeft != null && integerRight != null)
                    {
                        result = new IntegerConstantExpression(integerLeft.Value % integerRight.Value);
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

        /// <summary>
        /// Determines whether the specified <see cref="MathematicExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="MathematicExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="MathematicExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = (MathematicExpression)obj;
            return Operation == that.Operation && Left == that.Left && Right == that.Right;
        }
    }

    /// <summary>
    /// Specifies how the two sides of the <see cref="MathematicExpression"/> should be combined.
    /// </summary>
    internal enum MathematicOperation
    {
        /// <summary>
        /// Unspecified
        /// </summary>
        None = 0,

        /// <summary>
        /// Add the two values.
        /// </summary>
        Add,

        /// <summary>
        /// Subtract the second value from the first.
        /// </summary>
        Subtract,

        /// <summary>
        /// Multiply the two values.
        /// </summary>
        Multiply,

        /// <summary>
        /// Divide the first value by the second.
        /// </summary>
        Divide,

        /// <summary>
        /// Get the remainder from dividing the first value by the second.
        /// </summary>
        Modulus,
    }

    /// <summary>
    /// Gets the priority of a mathematic operation
    /// </summary>
    internal enum MathematicPriority
    {
        /// <summary>
        /// Unspecified
        /// </summary>
        None = 0,

        /// <summary>
        /// Add/Subtract
        /// </summary>
        Add,

        /// <summary>
        /// Multiply/Divide/Modulus
        /// </summary>
        Multiply,
    }

    /// <summary>
    /// Defines a mathematic modified to apply to a value
    /// </summary>
    [DebuggerDisplay("ValueModifier: {Operation} {Amount}")]
    internal struct ValueModifier
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValueModifier"/> struct.
        /// </summary>
        /// <param name="operation">The operation to apply.</param>
        /// <param name="amount">The amount to apply.</param>
        public ValueModifier(MathematicOperation operation, int amount)
        {
            Operation = operation;
            Amount = amount;
        }

        /// <summary>
        /// Gets or sets the operation to apply.
        /// </summary>
        public MathematicOperation Operation { get; set; }

        /// <summary>
        /// Gets or sets the amount to apply.
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// Applies the specified operation and amount to the provided <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value to modified.</param>
        /// <returns>The modified value.</returns>
        public int Apply(int value)
        {
            return Apply(value, Operation, Amount);
        }

        /// <summary>
        /// Applies the specified <paramref name="operation"/> and <paramref name="amount"/> to the provided <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value to modified.</param>
        /// <param name="operation">The operation to apply.</param>
        /// <param name="amount">The amount to apply.</param>
        /// <returns>The modified value.</returns>
        public static int Apply(int value, MathematicOperation operation, int amount)
        {
            switch (operation)
            {
                case MathematicOperation.Add:
                    return value + amount;

                case MathematicOperation.Subtract:
                    return value - amount;

                case MathematicOperation.Multiply:
                    return value * amount;

                case MathematicOperation.Divide:
                    return value / amount;

                case MathematicOperation.Modulus:
                    return value % amount;

                default:
                    return 0;
            }
        }
    }
}
