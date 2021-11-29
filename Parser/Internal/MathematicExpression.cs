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

            bool mergeResult = MergeOperands(left, Operation, right, out result);

            if (result.Location.IsEmpty)
                CopyLocation(result);

            return mergeResult;
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
                if (left.Type == ExpressionType.ParseError)
                    return left;
            }

            var mathematicRight = right as MathematicExpression;
            if (mathematicRight != null)
            {
                right = mathematicRight.MergeOperands();
                if (right.Type == ExpressionType.ParseError)
                    return right;
            }

            MergeOperands(Left, Operation, Right, out result);
            CopyLocation(result);
            return result;
        }

        private static bool MergeOperands(ExpressionBase left, MathematicOperation operation, ExpressionBase right, out ExpressionBase result)
        {
            // ASSERT: expression tree has already been rebalanced and variables have been replaced

            if (!left.IsLiteralConstant)
            {
                // cannot combine non-constant, leave as is.
                if (right.IsLiteralConstant)
                {
                    // attempt to merge the right constant into the left mathematic expression
                    var mathematicLeft = left as MathematicExpression;
                    if (mathematicLeft != null && MergeNonConstantMathematic(mathematicLeft, operation, right, out result))
                        return true;

                    // attempt to eliminate the right constant
                    if (MergeIdentity(left, operation, right, out result))
                        return true;

                    if (result is ParseErrorExpression)
                        return false;
                }
            }
            else if (!right.IsLiteralConstant)
            {
                // cannot combine non-constant. when possible, prefer constants on the right.
                switch (operation)
                {
                    case MathematicOperation.Add:
                    case MathematicOperation.Multiply:
                        if (MergeIdentity(right, operation, left, out result))
                            return true;

                        if (result is ParseErrorExpression)
                            return false;

                        var temp = left;
                        left = right;
                        right = temp;
                        break;

                    default:
                        // cannot swap
                        break;
                }
            }
            else
            {
                switch (operation)
                {
                    case MathematicOperation.Add:
                        return MergeAddition(left, right, out result);

                    case MathematicOperation.Subtract:
                        return MergeSubtraction(left, right, out result);

                    case MathematicOperation.Multiply:
                        return MergeMultiplication(left, right, out result);

                    case MathematicOperation.Divide:
                        return MergeDivision(left, right, out result);

                    case MathematicOperation.Modulus:
                        return MergeModulus(left, right, out result);
                }
            }

            result = new MathematicExpression(left, operation, right);
            return true;
        }

        private static bool ConvertToFloat(ref ExpressionBase left, ref ExpressionBase right, out ExpressionBase result)
        {
            left = FloatConstantExpression.ConvertFrom(left);
            if (left.Type != ExpressionType.FloatConstant)
            {
                result = left;
                return false;
            }

            right = FloatConstantExpression.ConvertFrom(right);
            if (right.Type != ExpressionType.FloatConstant)
            {
                result = right;
                return false;
            }

            result = null;
            return true;
        }

        private static bool MergeAddition(ExpressionBase left, ExpressionBase right, out ExpressionBase result)
        {
            // if either side is a string, combine to a larger string
            if (left.Type == ExpressionType.StringConstant || right.Type == ExpressionType.StringConstant)
            {
                var builder = new StringBuilder();

                if (left.Type == ExpressionType.StringConstant)
                    builder.Append(((StringConstantExpression)left).Value); // StringConstant.AppendString includes quotes
                else
                    left.AppendString(builder);

                if (right.Type == ExpressionType.StringConstant)
                    builder.Append(((StringConstantExpression)right).Value); // StringConstant.AppendString includes quotes
                else
                    right.AppendString(builder);

                result = new StringConstantExpression(builder.ToString());
                return true;
            }

            // if either side is a float, convert both to float
            if (left.Type == ExpressionType.FloatConstant || right.Type == ExpressionType.FloatConstant)
            {
                if (!ConvertToFloat(ref left, ref right, out result))
                    return false;

                result = new FloatConstantExpression(((FloatConstantExpression)left).Value + ((FloatConstantExpression)right).Value);
                return true;
            }

            if (left.Type == ExpressionType.IntegerConstant && right.Type == ExpressionType.IntegerConstant)
            {
                result = new IntegerConstantExpression(((IntegerConstantExpression)left).Value + ((IntegerConstantExpression)right).Value);
                return true;
            }

            result = new ParseErrorExpression("Cannot add expressions");
            return false;
        }

        private static bool MergeSubtraction(ExpressionBase left, ExpressionBase right, out ExpressionBase result)
        {
            // if either side is a float, convert both to float
            if (left.Type == ExpressionType.FloatConstant || right.Type == ExpressionType.FloatConstant)
            {
                if (!ConvertToFloat(ref left, ref right, out result))
                    return false;

                result = new FloatConstantExpression(((FloatConstantExpression)left).Value - ((FloatConstantExpression)right).Value);
                return true;
            }

            if (left.Type == ExpressionType.IntegerConstant && right.Type == ExpressionType.IntegerConstant)
            {
                result = new IntegerConstantExpression(((IntegerConstantExpression)left).Value - ((IntegerConstantExpression)right).Value);
                return true;
            }

            result = new ParseErrorExpression("Cannot subtract expressions");
            return false;
        }

        private static bool MergeMultiplication(ExpressionBase left, ExpressionBase right, out ExpressionBase result)
        {
            // if either side is a float, convert both to float
            if (left.Type == ExpressionType.FloatConstant || right.Type == ExpressionType.FloatConstant)
            {
                if (!ConvertToFloat(ref left, ref right, out result))
                    return false;

                result = new FloatConstantExpression(((FloatConstantExpression)left).Value * ((FloatConstantExpression)right).Value);
                return true;
            }

            if (left.Type == ExpressionType.IntegerConstant && right.Type == ExpressionType.IntegerConstant)
            {
                result = new IntegerConstantExpression(((IntegerConstantExpression)left).Value * ((IntegerConstantExpression)right).Value);
                return true;
            }

            result = new ParseErrorExpression("Cannot multiply expressions");
            return false;
        }

        private static bool MergeDivision(ExpressionBase left, ExpressionBase right, out ExpressionBase result)
        {
            // if either side is a float, convert both to float
            if (left.Type == ExpressionType.FloatConstant || right.Type == ExpressionType.FloatConstant)
            {
                if (!ConvertToFloat(ref left, ref right, out result))
                    return false;

                if (((FloatConstantExpression)right).Value == 0.0)
                {
                    result = new ParseErrorExpression("Division by zero");
                    return false;
                }

                result = new FloatConstantExpression(((FloatConstantExpression)left).Value / ((FloatConstantExpression)right).Value);
                return true;
            }

            if (left.Type == ExpressionType.IntegerConstant && right.Type == ExpressionType.IntegerConstant)
            {
                if (((IntegerConstantExpression)right).Value == 0.0)
                {
                    result = new ParseErrorExpression("Division by zero");
                    return false;
                }

                result = new IntegerConstantExpression(((IntegerConstantExpression)left).Value / ((IntegerConstantExpression)right).Value);
                return true;
            }

            result = new ParseErrorExpression("Cannot divide expressions");
            return false;
        }

        private static bool MergeModulus(ExpressionBase left, ExpressionBase right, out ExpressionBase result)
        {
            // if either side is a float, convert both to float
            if (left.Type == ExpressionType.FloatConstant || right.Type == ExpressionType.FloatConstant)
            {
                if (!ConvertToFloat(ref left, ref right, out result))
                    return false;

                if (((FloatConstantExpression)right).Value == 0.0)
                {
                    result = new ParseErrorExpression("Division by zero");
                    return false;
                }

                result = new FloatConstantExpression(((FloatConstantExpression)left).Value % ((FloatConstantExpression)right).Value);
                return true;
            }

            if (left.Type == ExpressionType.IntegerConstant && right.Type == ExpressionType.IntegerConstant)
            {
                if (((IntegerConstantExpression)right).Value == 0.0)
                {
                    result = new ParseErrorExpression("Division by zero");
                    return false;
                }

                result = new IntegerConstantExpression(((IntegerConstantExpression)left).Value % ((IntegerConstantExpression)right).Value);
                return true;
            }

            result = new ParseErrorExpression("Cannot modulus expressions");
            return false;
        }

        private static bool MergeNonConstantMathematic(MathematicExpression mathematicLeft, MathematicOperation operation, ExpressionBase right, out ExpressionBase result)
        {
            var left = mathematicLeft.Right;
            result = null;

            var newLeft = mathematicLeft.Left;
            var newOperation = mathematicLeft.Operation;
            ExpressionBase newRight;

            switch (mathematicLeft.Operation)
            {
                case MathematicOperation.Add:
                    if (operation == MathematicOperation.Add)
                    {
                        // (a + 3) + 2 => a + (3 + 2)
                        if (!MergeAddition(left, right, out newRight))
                        {
                            result = newRight;
                            return false;
                        }
                    }
                    else if (operation == MathematicOperation.Subtract)
                    {
                        if (IsGreater(left, right))
                        {
                            // (a + 3) - 2 => a + (3 - 2)
                            if (!MergeSubtraction(left, right, out newRight))
                            {
                                result = newRight;
                                return false;
                            }
                        }
                        else
                        {
                            // (a + 2) - 3 => a - (3 - 2)
                            if (!MergeSubtraction(right, left, out newRight))
                            {
                                result = newRight;
                                return false;
                            }

                            newOperation = MathematicOperation.Subtract;
                        }
                    }
                    else
                    {
                        return false;
                    }
                    break;

                case MathematicOperation.Subtract:
                    if (operation == MathematicOperation.Add)
                    {
                        if (IsGreater(left, right))
                        {
                            // (a - 3) + 2 => a - (3 - 2)
                            if (!MergeSubtraction(left, right, out newRight))
                            {
                                result = newRight;
                                return false;
                            }
                        }
                        else
                        {
                            // (a - 2) + 3 => a + (3 - 2)
                            if (!MergeSubtraction(right, left, out newRight))
                            {
                                result = newRight;
                                return false;
                            }

                            newOperation = MathematicOperation.Add;
                        }
                    }
                    else if (operation == MathematicOperation.Subtract)
                    {
                        // (a - 3) - 2 => a - (3 + 2)
                        if (!MergeAddition(left, right, out newRight))
                        {
                            result = newRight;
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                    break;

                case MathematicOperation.Multiply:
                    switch (operation)
                    {
                        case MathematicOperation.Multiply:
                            // (a * 3) * 2 => a * (3 * 2)
                            if (!MergeMultiplication(left, right, out newRight))
                            {
                                result = newRight;
                                return false;
                            }
                            break;

                        case MathematicOperation.Divide:
                            if (left.Type == ExpressionType.FloatConstant)
                            {
                                right = FloatConstantExpression.ConvertFrom(right);
                                if (right.Type == ExpressionType.ParseError)
                                    return false;
                            }
                            else if (right.Type == ExpressionType.FloatConstant)
                            {
                                left = FloatConstantExpression.ConvertFrom(left);
                                if (left.Type == ExpressionType.ParseError)
                                    return false;
                            }
                            else
                            {
                                // (a * 8) / 4 => a * (8 / 4) => a * 2

                                // can only merge these if the constant on the left is a multiple of the constant on the right
                                if (!IsMultiple(left, right))
                                    return false;
                            }

                            if (!MergeDivision(left, right, out newRight))
                            {
                                result = newRight;
                                return false;
                            }
                            break;

                        case MathematicOperation.Modulus:
                            // (a * 8) % 4 => a % 4
                            // can only merge these if the constant on the left is a multiple of the constant on the right
                            if (!IsMultiple(left, right))
                                return false;

                            newRight = right;
                            newOperation = MathematicOperation.Modulus;
                            break;

                        default:
                            return false;
                    }
                    break;

                case MathematicOperation.Divide:
                    if (operation == MathematicOperation.Divide)
                    {
                        // (a / 3) / 2 => a / (3 * 2)
                        var multiplication = new MathematicExpression(left, MathematicOperation.Multiply, right);
                        if (!MergeMultiplication(left, right, out newRight))
                        {
                            result = newRight;
                            return false;
                        }
                    }
                    else if (operation == MathematicOperation.Multiply)
                    {
                        if (left.Type == ExpressionType.FloatConstant || right.Type == ExpressionType.FloatConstant)
                        {
                            // (a / 3.0) * 2.0 => a * (2.0 / 3.0)
                            if (!MergeDivision(right, left, out newRight))
                            {
                                result = newRight;
                                return false;
                            }

                            newOperation = MathematicOperation.Multiply;
                        }
                        else
                        {
                            // (a / 3) * 2 => a * (2 / 3)

                            // when integer division is performed first, the result may be floored before applying
                            // the multiplication, so don't automatically merge them.
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                    break;

                default:
                    return false;
            }

            return MergeOperands(newLeft, newOperation, newRight, out result);
        }

        private static bool IsGreater(ExpressionBase left, ExpressionBase right)
        {
            var leftInteger = left as IntegerConstantExpression;
            var rightInteger = right as IntegerConstantExpression;
            if (leftInteger != null && rightInteger != null)
                return (leftInteger.Value > rightInteger.Value);

            var leftFloat = FloatConstantExpression.ConvertFrom(left) as FloatConstantExpression;
            var rightFloat = FloatConstantExpression.ConvertFrom(right) as FloatConstantExpression;
            if (leftFloat == null || rightFloat == null)
                return false;

            return (leftFloat.Value > rightFloat.Value);
        }

        private static bool IsMultiple(ExpressionBase left, ExpressionBase right)
        {
            var leftInteger = left as IntegerConstantExpression;
            var rightInteger = right as IntegerConstantExpression;
            if (leftInteger != null && rightInteger != null)
                return ((leftInteger.Value % rightInteger.Value) == 0);

            var leftFloat = FloatConstantExpression.ConvertFrom(left) as FloatConstantExpression;
            var rightFloat = FloatConstantExpression.ConvertFrom(right) as FloatConstantExpression;
            if (leftFloat == null || rightFloat == null)
                return false;

            return ((leftFloat.Value % rightFloat.Value) == 0.0f);
        }

        private static bool MergeIdentity(ExpressionBase left, MathematicOperation operation, ExpressionBase right, out ExpressionBase result)
        {
            bool isZero = false;
            bool isOne = false;
            var integerRight = right as IntegerConstantExpression;
            if (integerRight != null)
            {
                isZero = (integerRight.Value == 0);
                isOne = (integerRight.Value == 1);
            }
            else
            {
                var floatRight = right as FloatConstantExpression;
                if (floatRight != null)
                {
                    isZero = floatRight.Value == 0.0f;
                    isOne = floatRight.Value == 1.0f;
                }
            }

            if (isZero)
            {
                switch (operation)
                {
                    case MathematicOperation.Add:
                    case MathematicOperation.Subtract:
                        // anything plus or minus 0 is itself
                        result = left;
                        return true;

                    case MathematicOperation.Multiply:
                        // anything times 0 is 0
                        result = right;
                        return true;

                    case MathematicOperation.Divide:
                    case MathematicOperation.Modulus:
                        result = new ParseErrorExpression("Division by zero");
                        return false;
                }
            }
            else if (isOne)
            {
                switch (operation)
                {
                    case MathematicOperation.Multiply:
                    case MathematicOperation.Divide:
                        // anything multiplied or divided by 1 is itself
                        result = left;
                        return true;

                    case MathematicOperation.Modulus:
                        // anything modulus 1 is 0
                        result = new IntegerConstantExpression(0);
                        return true;
                }
            }

            result = null;
            return false;
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
