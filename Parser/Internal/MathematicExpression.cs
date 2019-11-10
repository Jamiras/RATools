using System.Diagnostics;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class MathematicExpression : LeftRightExpressionBase
    {
        public MathematicExpression(ExpressionBase left, MathematicOperation operation, ExpressionBase right)
            : base(left, right, ExpressionType.Mathematic)
        {
            Operation = operation;
        }

        /// <summary>
        /// Gets the mathematic operation.
        /// </summary>
        public MathematicOperation Operation { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            Left.AppendString(builder);
            builder.Append(' ');

            builder.Append(GetOperatorCharacter(Operation));

            builder.Append(' ');
            Right.AppendString(builder);
        }

        internal static char GetOperatorCharacter(MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Add: return '+';
                case MathematicOperation.Subtract: return '-';
                case MathematicOperation.Multiply: return '*';
                case MathematicOperation.Divide: return '/';
                case MathematicOperation.Modulus: return '%';
                default: return '?';
            }
        }

        internal static string GetOperatorType(MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Add: return "addition";
                case MathematicOperation.Subtract: return "subtraction";
                case MathematicOperation.Multiply: return "multiplication";
                case MathematicOperation.Divide: return "division";
                case MathematicOperation.Modulus: return "modulus";
                default: return "mathematic";
            }
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

        internal static MathematicOperation GetOppositeOperation(MathematicOperation op)
        {
            switch (op)
            {
                case MathematicOperation.Add: return MathematicOperation.Subtract;
                case MathematicOperation.Subtract: return MathematicOperation.Add;
                case MathematicOperation.Multiply: return MathematicOperation.Divide;
                case MathematicOperation.Divide: return MathematicOperation.Multiply;
                default: return MathematicOperation.None;
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
                if (mathematicRight != null && !(Left is StringConstantExpression))
                {
                    // multiply and divide should happen before add or subtract.
                    // at the same priority, they should happen left-to-right.
                    if (GetPriority(Operation) >= GetPriority(mathematicRight.Operation))
                    {
                        var newLeft = new MathematicExpression(Left, Operation, mathematicRight.Left);
                        newLeft = newLeft.Rebalance() as MathematicExpression;
                        return new MathematicExpression(newLeft, mathematicRight.Operation, mathematicRight.Right);
                    }
                }

                var comparisonRight = Right as ComparisonExpression;
                if (comparisonRight != null)
                    return Rebalance(comparisonRight);

                var conditionalRight = Right as ConditionalExpression;
                if (conditionalRight != null)
                    return Rebalance(conditionalRight);
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

            return MergeOperands(left, right, out result);
        }

        private bool MergeOperands(ExpressionBase left, ExpressionBase right, out ExpressionBase result)
        { 
            var integerLeft = left as IntegerConstantExpression;
            var integerRight = right as IntegerConstantExpression;

            switch (Operation)
            {
                case MathematicOperation.Add:
                    var stringLeft = left as StringConstantExpression;
                    var stringRight = right as StringConstantExpression;
                    if (stringLeft != null)
                    {
                        if (stringRight != null)
                        {
                            result = new StringConstantExpression(stringLeft.Value + stringRight.Value);
                            CopyLocation(result);
                            return true;
                        }

                        if (integerRight != null)
                        {
                            result = new StringConstantExpression(stringLeft.Value + integerRight.Value.ToString());
                            CopyLocation(result);
                            return true;
                        }
                    }
                    else if (stringRight != null)
                    {
                        if (integerLeft != null)
                        {
                            result = new StringConstantExpression(integerLeft.Value.ToString() + stringRight.Value);
                            CopyLocation(result);
                            return true;
                        }
                    }

                    // prefer constants on right
                    if (integerLeft != null && integerRight == null)
                    {
                        var temp = left;
                        left = right;
                        right = temp;
                        integerRight = integerLeft;
                        integerLeft = null;
                    }

                    if (integerRight != null)
                    {
                        if (integerRight.Value == 0) // anything plus 0 is itself
                        {
                            result = left;
                            return true;
                        }

                        if (integerLeft != null)
                        {
                            result = new IntegerConstantExpression(integerLeft.Value + integerRight.Value);
                            CopyLocation(result);
                            return true;
                        }

                        var mathematicLeft = left as MathematicExpression;
                        if (mathematicLeft != null)
                        {
                            integerLeft = mathematicLeft.Right as IntegerConstantExpression;
                            if (integerLeft != null)
                            {
                                if (mathematicLeft.Operation == MathematicOperation.Add)
                                {
                                    result = new MathematicExpression(mathematicLeft.Left, MathematicOperation.Add, 
                                        new IntegerConstantExpression(integerLeft.Value + integerRight.Value));
                                    CopyLocation(result);
                                    return true;
                                }

                                if (mathematicLeft.Operation == MathematicOperation.Subtract)
                                {
                                    if (integerLeft.Value == integerRight.Value)
                                    {
                                        result = mathematicLeft.Left;
                                        return true;
                                    }

                                    if (integerLeft.Value > integerRight.Value)
                                    {
                                        result = new MathematicExpression(mathematicLeft.Left, MathematicOperation.Subtract,
                                            new IntegerConstantExpression(integerLeft.Value - integerRight.Value));
                                    }
                                    else
                                    {
                                        result = new MathematicExpression(mathematicLeft.Left, MathematicOperation.Add,
                                            new IntegerConstantExpression(integerRight.Value - integerLeft.Value));
                                    }

                                    CopyLocation(result);
                                    return true;
                                }
                            }
                        }
                    }
                    break;

                case MathematicOperation.Subtract:
                    if (integerRight != null)
                    {
                        if (integerRight.Value == 0) // anything minus 0 is itself
                        {
                            result = left;
                            return true;
                        }

                        if (integerLeft != null)
                        {
                            result = new IntegerConstantExpression(integerLeft.Value - integerRight.Value);
                            CopyLocation(result);
                            return true;
                        }

                        var mathematicLeft = left as MathematicExpression;
                        if (mathematicLeft != null)
                        {
                            integerLeft = mathematicLeft.Right as IntegerConstantExpression;
                            if (integerLeft != null)
                            {
                                if (mathematicLeft.Operation == MathematicOperation.Subtract)
                                {
                                    result = new MathematicExpression(mathematicLeft.Left, MathematicOperation.Subtract,
                                        new IntegerConstantExpression(integerLeft.Value + integerRight.Value));
                                    CopyLocation(result);
                                    return true;
                                }

                                if (mathematicLeft.Operation == MathematicOperation.Add)
                                {
                                    if (integerLeft.Value == integerRight.Value)
                                    {
                                        result = mathematicLeft.Left;
                                        return true;
                                    }

                                    if (integerLeft.Value > integerRight.Value)
                                    {
                                        result = new MathematicExpression(mathematicLeft.Left, MathematicOperation.Add,
                                            new IntegerConstantExpression(integerLeft.Value - integerRight.Value));
                                    }
                                    else
                                    {
                                        result = new MathematicExpression(mathematicLeft.Left, MathematicOperation.Subtract,
                                            new IntegerConstantExpression(integerRight.Value - integerLeft.Value));
                                    }

                                    CopyLocation(result);
                                    return true;
                                }
                            }
                        }
                    }
                    break;

                case MathematicOperation.Multiply:
                    // prefer constants on right
                    if (integerLeft != null && integerRight == null)
                    {
                        var temp = left;
                        left = right;
                        right = temp;
                        integerRight = integerLeft;
                        integerLeft = null;
                    }

                    if (integerRight != null)
                    {
                        if (integerRight.Value == 0) // anything times 0 is 0
                        {
                            result = right;
                            return true;
                        }

                        if (integerRight.Value == 1) // anything times 1 is itself
                        {
                            result = left;
                            return true;
                        }

                        if (integerLeft != null)
                        {
                            result = new IntegerConstantExpression(integerLeft.Value * integerRight.Value);
                            CopyLocation(result);
                            return true;
                        }

                        var mathematicLeft = left as MathematicExpression;
                        if (mathematicLeft != null)
                        {
                            integerLeft = mathematicLeft.Right as IntegerConstantExpression;
                            if (integerLeft != null)
                            {
                                if (mathematicLeft.Operation == MathematicOperation.Multiply)
                                {
                                    result = new MathematicExpression(mathematicLeft.Left, MathematicOperation.Multiply,
                                        new IntegerConstantExpression(integerLeft.Value * integerRight.Value));
                                    CopyLocation(result);
                                    return true;
                                }
                            }
                        }
                    }
                    break;

                case MathematicOperation.Divide:
                    if (integerRight != null)
                    {
                        if (integerRight.Value == 0) // division by 0 is impossible
                        {
                            result = new ParseErrorExpression("division by zero", this);
                            return false;
                        }

                        if (integerRight.Value == 1) // anything divided by 1 is itself
                        {
                            result = left;
                            return true;
                        }

                        if (integerLeft != null)
                        {
                            result = new IntegerConstantExpression(integerLeft.Value / integerRight.Value);
                            CopyLocation(result);
                            return true;
                        }

                        var mathematicLeft = left as MathematicExpression;
                        if (mathematicLeft != null)
                        {
                            integerLeft = mathematicLeft.Right as IntegerConstantExpression;
                            if (integerLeft != null)
                            {
                                if (mathematicLeft.Operation == MathematicOperation.Divide)
                                {
                                    result = new MathematicExpression(mathematicLeft.Left, MathematicOperation.Divide,
                                       new IntegerConstantExpression(integerLeft.Value * integerRight.Value));
                                    CopyLocation(result);
                                    return true;
                                }

                                if (mathematicLeft.Operation == MathematicOperation.Multiply && (integerLeft.Value % integerRight.Value == 0))
                                {
                                    if (integerLeft.Value == integerRight.Value)
                                    {
                                        result = mathematicLeft.Left;
                                        return true;
                                    }

                                    result = new MathematicExpression(mathematicLeft.Left, MathematicOperation.Multiply,
                                        new IntegerConstantExpression(integerLeft.Value / integerRight.Value));
                                    CopyLocation(result);
                                    return true;
                                }
                            }
                        }
                    }
                    break;

                case MathematicOperation.Modulus:
                    if (integerRight != null)
                    {
                        if (integerRight.Value == 0) // division by 0 is impossible
                        {
                            result = new ParseErrorExpression("division by zero", this);
                            return false;
                        }

                        if (integerRight.Value == 1) // anything modulus 1 is 0
                        {
                            result = new IntegerConstantExpression(0);
                            CopyLocation(result);
                            return true;
                        }

                        if (integerLeft != null)
                        {
                            result = new IntegerConstantExpression(integerLeft.Value % integerRight.Value);
                            CopyLocation(result);
                            return true;
                        }
                    }
                    break;
            }

            result = new MathematicExpression(left, Operation, right);
            CopyLocation(result);
            return true;
        }

        /// <summary>
        /// Moves the IntegerConstant (if present) to be the Right operand of the root node.
        /// </summary>
        /// <remarks>Combines nodes where possible.</remarks>
        internal static MathematicExpression BubbleUpIntegerConstant(MathematicExpression mathematic)
        { 
            var priority = GetPriority(mathematic.Operation);

            var mathematicLeft = mathematic.Left as MathematicExpression;
            if (mathematicLeft != null)
            {
                if (GetPriority(mathematicLeft.Operation) == priority)
                {
                    mathematic.Left = mathematicLeft = BubbleUpIntegerConstant(mathematicLeft);
                    if (mathematicLeft.Right is IntegerConstantExpression)
                    {
                        mathematic.Left = mathematicLeft.Left;
                        mathematicLeft.Left = BubbleUpIntegerConstant(mathematic);
                        mathematic = mathematicLeft;
                    }
                }
            }

            var mathematicRight = mathematic.Right as MathematicExpression;
            if (mathematicRight != null)
            {
                if (GetPriority(mathematicRight.Operation) == priority)
                {
                    mathematic.Right = mathematicRight = BubbleUpIntegerConstant(mathematicRight);
                    if (mathematicRight.Right is IntegerConstantExpression)
                    {
                        if (mathematic.Operation == MathematicOperation.Add)
                        {
                            mathematic.Right = mathematicRight.Left;
                            mathematicRight.Left = BubbleUpIntegerConstant(mathematic);
                            mathematic = mathematicRight;
                        }
                        else if (mathematic.Operation == MathematicOperation.Subtract)
                        {
                            mathematic.Right = mathematicRight.Left;
                            mathematicRight.Left = BubbleUpIntegerConstant(mathematic);
                            mathematicRight.Right = new IntegerConstantExpression(-((IntegerConstantExpression)mathematicRight.Right).Value);
                            mathematic = mathematicRight;
                        }
                    }
                }
            }

            if (mathematic.Right is IntegerConstantExpression)
            {
                mathematicLeft = mathematic.Left as MathematicExpression;
                if (mathematicLeft != null && GetPriority(mathematicLeft.Operation) == priority &&
                    mathematicLeft.Right is IntegerConstantExpression)
                {
                    ExpressionBase result;

                    mathematic.Left = mathematicLeft.Right;
                    if (mathematic.MergeOperands(mathematic.Left, mathematic.Right, out result))
                    {
                        mathematicLeft.Right = result;

                        if (mathematicLeft.MergeOperands(mathematicLeft.Left, mathematicLeft.Right, out result))
                            mathematicLeft = (MathematicExpression)result;

                        mathematic = mathematicLeft;
                    }
                }
            }

            return mathematic;
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
        /// <param name="value">The value to modify.</param>
        /// <returns>The modified value.</returns>
        public int Apply(int value)
        {
            return Apply(value, Operation, Amount);
        }

        /// <summary>
        /// Reverses application of the specified operation and amount to the provided <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value to modify.</param>
        /// <returns>The modified value.</returns>
        public int Remove(int value)
        {
            return Apply(value, MathematicExpression.GetOppositeOperation(Operation), Amount);
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
