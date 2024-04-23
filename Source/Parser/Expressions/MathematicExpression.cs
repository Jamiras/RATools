using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Diagnostics;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class MathematicExpression : LeftRightExpressionBase, 
        IMathematicCombineExpression, IComparisonNormalizeExpression, IValueExpression
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
            if (Operation == MathematicOperation.BitwiseInvert)
            {
                builder.Append('~');
                Right.AppendString(builder);
                return;
            }

            var memoryValue = Left as MemoryValueExpression;
            if (memoryValue != null && memoryValue.HasConstant)
            {
                builder.Append('(');
                Left.AppendString(builder);
                builder.Append(')');
            }
            else
            {
                Left.AppendString(builder);
            }

            builder.Append(' ');

            builder.Append(GetOperatorCharacter(Operation));

            builder.Append(' ');

            if (Operation != MathematicOperation.Add && Right.Type == ExpressionType.Mathematic)
            {
                builder.Append('(');
                Right.AppendString(builder);
                builder.Append(')');
            }
            else
            {
                Right.AppendString(builder);
            }
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
                case MathematicOperation.BitwiseAnd: return '&';
                case MathematicOperation.BitwiseXor: return '^';
                case MathematicOperation.BitwiseInvert: return '~';
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
                case MathematicOperation.BitwiseAnd: return "bitwise and";
                case MathematicOperation.BitwiseXor: return "bitwise xor";
                case MathematicOperation.BitwiseInvert: return "bitwise invert";
                default: return "mathematic";
            }
        }

        internal static string GetOperatorVerb(MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Add: return "add";
                case MathematicOperation.Subtract: return "subtract";
                case MathematicOperation.Multiply: return "multiply";
                case MathematicOperation.Divide: return "divide";
                case MathematicOperation.Modulus: return "modulus";
                case MathematicOperation.BitwiseAnd: return "bitwise and";
                case MathematicOperation.BitwiseXor: return "bitwise xor";
                case MathematicOperation.BitwiseInvert: return "bitwise invert";
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

                case MathematicOperation.BitwiseAnd:
                    return MathematicPriority.BitwiseAnd;

                case MathematicOperation.BitwiseXor:
                    return MathematicPriority.BitwiseXor;

                case MathematicOperation.BitwiseInvert:
                    return MathematicPriority.BitwiseInvert;

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
        /// Evaluates an expression
        /// </summary>
        /// <returns><see cref="ErrorExpression"/> indicating the failure, or the result of evaluating the expression.</returns>
        public ExpressionBase Evaluate(InterpreterScope scope)
        {
            ExpressionBase result;
            ReplaceVariables(scope, out result);
            return result;
        }

        /// <summary>
        /// Replaces the variables in the expression with values from <paramref name="scope" />.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] The new expression containing the replaced variables.</param>
        /// <returns>
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ErrorExpression" />.
        /// </returns>
        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            ExpressionBase right;

            if (Operation == MathematicOperation.BitwiseInvert)
            {
                if (!Right.ReplaceVariables(scope, out right))
                {
                    result = right;
                    return false;
                }

                var integerRight = right as IntegerConstantExpression;
                if (integerRight != null)
                {
                    result = new IntegerConstantExpression(~integerRight.Value);
                }
                else
                {
                    var memoryAccessor = right as MemoryAccessorExpression;
                    if (memoryAccessor != null)
                    {
                        result = new BitwiseInvertExpression(memoryAccessor);
                    }
                    else
                    {
                        result = new ErrorExpression("Cannot bitwise invert " + right.Type, Right);
                    }
                }
            }
            else
            {
                ExpressionBase left;
                if (!Left.ReplaceVariables(scope, out left))
                {
                    result = left;
                    return false;
                }

                if (!Right.ReplaceVariables(scope, out right))
                {
                    result = right;
                    return false;
                }

                var combinable = left as IMathematicCombineExpression;
                result = (combinable != null) ? combinable.Combine(right, Operation) : null;
                if (result == null)
                {
                    var inverseCombinable = right as IMathematicCombineInverseExpression;
                    if (inverseCombinable != null)
                        result = inverseCombinable.CombineInverse(left, Operation);

                    if (result == null)
                        result = CreateCannotCombineError(left, Operation, right);
                }
            }

            if (result.Location.IsEmpty)
                CopyLocation(result);

            result.IsLogicalUnit = IsLogicalUnit;
            return (result is not ErrorExpression);
        }

        /// <summary>
        /// Attempts to merge the operands without evaluating them.
        /// </summary>
        internal ExpressionBase MergeOperands()
        {
            ExpressionBase result;
            var left = Left;
            var right = Right;

            var mathematicLeft = left as MathematicExpression;
            if (mathematicLeft != null)
            {
                left = mathematicLeft.MergeOperands();
                if (left.Type == ExpressionType.Error)
                    return left;
            }

            var mathematicRight = right as MathematicExpression;
            if (mathematicRight != null)
            {
                right = mathematicRight.MergeOperands();
                if (right.Type == ExpressionType.Error)
                    return right;
            }

            MergeOperands(Left, Operation, Right, out result);
            CopyLocation(result);
            return result;
        }

        private static ErrorExpression CreateCannotCombineError(ExpressionBase left, MathematicOperation operation, ExpressionBase right)
        {
            return new ErrorExpression(string.Format("Cannot {0} {1} and {2}", GetOperatorVerb(operation), left.Type.ToLowerString(), right.Type.ToLowerString()));
        }

        private static bool MergeOperands(ExpressionBase left, MathematicOperation operation, ExpressionBase right, out ExpressionBase result)
        {
            // ASSERT: expression tree has already been rebalanced and variables have been replaced
            result = null;

            var combinable = left as IMathematicCombineExpression;
            if (combinable != null)
                result = combinable.Combine(right, operation);

            if (result == null)
            {
                var inverseCombinable = right as IMathematicCombineInverseExpression;
                if (inverseCombinable != null)
                    result = inverseCombinable.CombineInverse(left, operation);

                if (result == null)
                    result = CreateCannotCombineError(left, operation, right);
            }

            result.Location = left.Location.Union(right.Location);
            return (result is not ErrorExpression);
        }

        /// <summary>
        /// Combines the current expression with the <paramref name="right"/> expression using the <paramref name="operation"/> operator.
        /// </summary>
        /// <param name="right">The expression to combine with the current expression.</param>
        /// <param name="operation">How to combine the expressions.</param>
        /// <returns>
        /// An expression representing the combined values on success, or <c>null</c> if the expressions could not be combined.
        /// </returns>
        public ExpressionBase Combine(ExpressionBase right, MathematicOperation operation)
        {
            if (GetPriority(operation) >= GetPriority(Operation))
            {
                var combining = Right as IMathematicCombineExpression;
                if (combining != null)
                {
                    var newRight = combining.Combine(right, operation);
                    if (newRight == null || newRight.Type == ExpressionType.Error)
                        return newRight;

                    return Combine(Left, Operation, newRight);
                }
            }

            return null;
        }

        public static ExpressionBase Combine(ExpressionBase left, MathematicOperation operation, ExpressionBase right)
        {
            var integerRight = right as IntegerConstantExpression;
            if (integerRight != null)
            {
                switch (operation)
                {
                    case MathematicOperation.Add:
                    case MathematicOperation.Subtract:
                        if (integerRight.Value == 0)
                            return left;
                        break;

                    case MathematicOperation.Multiply:
                        if (integerRight.Value == 0)
                            return integerRight;
                        if (integerRight.Value == 1)
                            return left;
                        break;

                    case MathematicOperation.Divide:
                        if (integerRight.Value == 0)
                            return new ErrorExpression("Division by zero");
                        if (integerRight.Value == 1)
                            return left;
                        break;

                    case MathematicOperation.Modulus:
                        if (integerRight.Value == 0)
                            return new ErrorExpression("Division by zero");
                        if (integerRight.Value == 1)
                            return new IntegerConstantExpression(0);
                        break;

                    case MathematicOperation.BitwiseAnd:
                        if (integerRight.Value == 0)
                            return integerRight;
                        break;

                    case MathematicOperation.BitwiseXor:
                        if (integerRight.Value == 0)
                            return left;
                        break;
                }
            }

            var floatRight = right as IntegerConstantExpression;
            if (floatRight != null)
            {
                switch (operation)
                {
                    case MathematicOperation.Add:
                    case MathematicOperation.Subtract:
                        if (floatRight.Value == 0.0)
                            return left;
                        break;

                    case MathematicOperation.Multiply:
                        if (floatRight.Value == 0.0)
                            return integerRight;
                        if (floatRight.Value == 1.0)
                            return left;
                        break;

                    case MathematicOperation.Divide:
                        if (floatRight.Value == 0.0)
                            return new ErrorExpression("Division by zero");
                        if (floatRight.Value == 1.0)
                            return left;
                        break;

                    case MathematicOperation.Modulus:
                        if (floatRight.Value == 0.0)
                            return new ErrorExpression("Division by zero");
                        break;
                }
            }

            return new MathematicExpression(left, operation, right);
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
                        if (mathematic.Right is IntegerConstantExpression)
                            return mathematic;

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
                    if (MergeOperands(mathematic.Left, mathematic.Operation, mathematic.Right, out result))
                    {
                        mathematicLeft.Right = result;

                        if (MergeOperands(mathematicLeft.Left, mathematicLeft.Operation, mathematicLeft.Right, out result))
                            mathematicLeft = (MathematicExpression)result;

                        mathematic = mathematicLeft;
                    }
                }
            }

            var integer = mathematic.Left as IntegerConstantExpression;
            if (integer != null)
            {
                switch (mathematic.Operation)
                {
                    case MathematicOperation.Add:
                    case MathematicOperation.Multiply:
                        // switch the order so the constant is on the right
                        mathematic = new MathematicExpression(mathematic.Right, mathematic.Operation, mathematic.Left);
                        break;

                    case MathematicOperation.Subtract:
                        if (integer.Value == 0)
                            break;

                        // change "N - func" to "0 - func + N" so N is on the right. the 0 will be optimized out later
                        mathematic = new MathematicExpression(
                            new MathematicExpression(new IntegerConstantExpression(0), MathematicOperation.Subtract, mathematic.Right),
                            MathematicOperation.Add,
                            integer);
                        break;

                    default:
                        break;
                }
            }

            return mathematic;
        }

        /// <summary>
        /// Normalizes the comparison between the current expression and the <paramref name="right"/> expression using the <paramref name="operation"/> operator.
        /// </summary>
        /// <param name="right">The expression to compare with the current expression.</param>
        /// <param name="operation">How to compare the expressions.</param>
        /// <param name="canModifyRight"><c>true</c> if <paramref name="right"/> can be changed, <c>false</c> if not.</param>
        /// <returns>
        /// An expression representing the normalized comparison, or <c>null</c> if normalization did not occur.
        /// </returns>
        public ExpressionBase NormalizeComparison(ExpressionBase right, ComparisonOperation operation, bool canModifyRight)
        {
            if (!canModifyRight)
                return null;

            var mathematicRight = right as MathematicExpression;
            if (mathematicRight != null)
            {
                // same operation/operands on both cancel each other out
                if (Operation == mathematicRight.Operation && Right == mathematicRight.Right)
                    return new ComparisonExpression(Left, operation, mathematicRight.Left);
            }

            var rightCombining = right as IMathematicCombineExpression;
            if (rightCombining != null)
            {
                ExpressionBase newRight = null;
                switch (Operation)
                {
                    case MathematicOperation.Add:
                        // a + x < b   =>   a < b - x
                        newRight = rightCombining.Combine(Right, MathematicOperation.Subtract);
                        break;

                    case MathematicOperation.Subtract:
                        // a - x < b   =>   a < b + x
                        newRight = rightCombining.Combine(Right, MathematicOperation.Add);
                        break;

                    case MathematicOperation.Divide:
                        // a / x < b   =>   a < b * x
                        newRight = rightCombining.Combine(Right, MathematicOperation.Multiply);
                        if (newRight != null && newRight is ModifiedMemoryAccessorExpression &&
                            right is MemoryAccessorExpression)
                        {
                            // don't cuase an unmodified memory accessor to become modified
                            newRight = null;
                        }
                        break;

                    case MathematicOperation.Multiply:
                        // a * x < b   =>   a < b / x
                        newRight = rightCombining.Combine(Right, MathematicOperation.Divide);
                        if (newRight != null)
                        {
                            if (newRight is ModifiedMemoryAccessorExpression &&
                                right is MemoryAccessorExpression)
                            {
                                // don't cuase an unmodified memory accessor to become modified
                                newRight = null;
                            }
                            else if (MemoryValueExpression.HasFloat(Left))
                            {
                                // left side has a float, so allow the division. if the division was done
                                // using integers, convert to float
                                if (newRight.Type == ExpressionType.IntegerConstant && right.Type == ExpressionType.IntegerConstant)
                                {
                                    var floatRight = new FloatConstantExpression((float)((IntegerConstantExpression)right).Value);
                                    if (floatRight != null)
                                        newRight = floatRight.Combine(Right, MathematicOperation.Divide);
                                }
                            }
                            else
                            {
                                // left side cannot generate a float. only perform the division
                                // if there's no remainder
                                var remainder = rightCombining.Combine(Right, MathematicOperation.Modulus);
                                if (remainder != null)
                                {
                                    switch (remainder.Type)
                                    {
                                        case ExpressionType.IntegerConstant:
                                            if (((IntegerConstantExpression)remainder).Value != 0)
                                                return ComparisonExpression.NormalizeFloatComparisonForInteger(Left, operation, newRight);
                                            break;

                                        case ExpressionType.FloatConstant:
                                            if (((FloatConstantExpression)remainder).Value != 0.0)
                                                return ComparisonExpression.NormalizeFloatComparisonForInteger(Left, operation, newRight);
                                            break;
                                    }
                                }
  
                                // no remainder, allow
                            }
                        }
                        break;
                }

                if (newRight != null)
                {
                    // don't allow normalization to convert a constant on the right to a non-constant
                    if (newRight is LiteralConstantExpressionBase || right is not LiteralConstantExpressionBase)
                        return new ComparisonExpression(Left, operation, newRight);
                }
            }

            return null;
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
            var that = obj as MathematicExpression;
            return that != null && Operation == that.Operation && Left == that.Left && Right == that.Right;
        }
    }

    /// <summary>
    /// Specifies how the two sides of the <see cref="MathematicExpression"/> should be combined.
    /// </summary>
    public enum MathematicOperation
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

        /// <summary>
        /// Gets the bits that are set in both the first value and the second.
        /// </summary>
        BitwiseAnd,

        /// <summary>
        /// Gets the bits that are set in either the first or and the second, but not both.
        /// </summary>
        BitwiseXor,

        /// <summary>
        /// Toggles all bits in the first value (effectively XOR 0xFFFFFFFF)
        /// </summary>
        BitwiseInvert,
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

        /// <summary>
        /// Bitwise AND
        /// </summary>
        BitwiseAnd,

        /// <summary>
        /// Bitwise XOR
        /// </summary>
        BitwiseXor,

        /// <summary>
        /// Bitwise inversion
        /// </summary>
        BitwiseInvert,
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

                case MathematicOperation.BitwiseAnd:
                    return value & amount;

                case MathematicOperation.BitwiseXor:
                    return value ^ amount;

                case MathematicOperation.BitwiseInvert:
                    return ~value;

                default:
                    return 0;
            }
        }
    }
}
