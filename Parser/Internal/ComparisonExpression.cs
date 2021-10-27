using RATools.Data;
using RATools.Parser.Functions;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class ComparisonExpression : LeftRightExpressionBase
    {
        public ComparisonExpression(ExpressionBase left, ComparisonOperation operation, ExpressionBase right)
            : base(left, right, ExpressionType.Comparison)
        {
            Operation = operation;
        }

        /// <summary>
        /// Gets the comparison operation.
        /// </summary>
        public ComparisonOperation Operation { get; private set; }

        private bool _fullyExpanded = false;

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            Left.AppendString(builder);
            builder.Append(' ');

            builder.Append(GetOperatorString(Operation));

            builder.Append(' ');
            Right.AppendString(builder);
        }

        internal static string GetOperatorString(ComparisonOperation operation)
        {
            switch (operation)
            {
                case ComparisonOperation.Equal: return "==";
                case ComparisonOperation.NotEqual: return "!=";
                case ComparisonOperation.LessThan: return "<";
                case ComparisonOperation.LessThanOrEqual: return "<=";
                case ComparisonOperation.GreaterThan: return ">";
                case ComparisonOperation.GreaterThanOrEqual: return ">=";
                default: return null;
            }
        }

        internal static ComparisonOperation GetOppositeComparisonOperation(ComparisonOperation op)
        {
            switch (op)
            {
                case ComparisonOperation.Equal: return ComparisonOperation.NotEqual;
                case ComparisonOperation.NotEqual: return ComparisonOperation.Equal;
                case ComparisonOperation.LessThan: return ComparisonOperation.GreaterThanOrEqual;
                case ComparisonOperation.LessThanOrEqual: return ComparisonOperation.GreaterThan;
                case ComparisonOperation.GreaterThan: return ComparisonOperation.LessThanOrEqual;
                case ComparisonOperation.GreaterThanOrEqual: return ComparisonOperation.LessThan;
                default: return ComparisonOperation.None;
            }
        }

        private static ComparisonOperation InvertComparisonOperation(ComparisonOperation op)
        {
            switch (op)
            {
                case ComparisonOperation.LessThan: return ComparisonOperation.GreaterThan;
                case ComparisonOperation.LessThanOrEqual: return ComparisonOperation.GreaterThanOrEqual;
                case ComparisonOperation.GreaterThan: return ComparisonOperation.LessThan;
                case ComparisonOperation.GreaterThanOrEqual: return ComparisonOperation.LessThanOrEqual;
                default: return op;
            }
        }

        private static bool MoveConstantsToRightHandSide(ComparisonExpression comparisonExpression, InterpreterScope scope, out ExpressionBase result)
        {
            ComparisonExpression newRoot;

            // if left side is not mathematic, we can't move anything
            var mathematicLeft = comparisonExpression.Left as MathematicExpression;
            if (mathematicLeft == null)
            {
                // if left side is an integer and the right side is not, swap the sides
                if (comparisonExpression.Left.Type == ExpressionType.IntegerConstant &&
                    comparisonExpression.Right.Type != ExpressionType.IntegerConstant)
                {
                    var operation = InvertComparisonOperation(comparisonExpression.Operation);
                    comparisonExpression = new ComparisonExpression(comparisonExpression.Right, operation, comparisonExpression.Left);

                    // recurse if necessary
                    if (comparisonExpression.Left.Type == ExpressionType.Mathematic)
                        return MoveConstantsToRightHandSide(comparisonExpression, scope, out result);

                    result = comparisonExpression;
                    return true;
                }

                result = comparisonExpression;
                return true;
            }

            var mathematicRight = comparisonExpression.Right as MathematicExpression;
            if (mathematicRight != null &&
                mathematicLeft.Operation == mathematicRight.Operation &&
                mathematicLeft.Right == mathematicRight.Right)
            {
                // same operation being applied to both sides, just cancel it out
                newRoot = new ComparisonExpression(mathematicLeft.Left, comparisonExpression.Operation, mathematicRight.Left);
            }
            else
            {
                // if it's not an integer, we can't merge it, so don't move it
                if (mathematicLeft.Right.Type != ExpressionType.IntegerConstant)
                {
                    result = comparisonExpression;
                    return true;
                }

                // move it to the other side by applying the inverse of the mathematical operation
                var operation = MathematicExpression.GetOppositeOperation(mathematicLeft.Operation);
                var right = new MathematicExpression(comparisonExpression.Right, operation, mathematicLeft.Right);
                if (!right.ReplaceVariables(scope, out result))
                    return false;

                // if the operators could not be merged, keep them where they were
                if (result is MathematicExpression)
                {
                    result = comparisonExpression;
                    return true;
                }

                var newRight = result;
                var comparisonOperation = comparisonExpression.Operation;

                // multiplication is converted to division. if the division is not exact, modify the comparison 
                // so its still logically valid (if possible).
                if (operation == MathematicOperation.Divide && newRight is IntegerConstantExpression)
                {
                    var reversed = new MathematicExpression(result, MathematicOperation.Multiply, mathematicLeft.Right);
                    if (!reversed.ReplaceVariables(scope, out result))
                        return false;

                    if (comparisonExpression.Right != result)
                    {
                        // division was not exact
                        switch (comparisonOperation)
                        {
                            case ComparisonOperation.Equal:
                                // a * 10 == 9999 can never be true
                                result = new ParseErrorExpression("Result can never be true using integer math", comparisonExpression);
                                return false;

                            case ComparisonOperation.NotEqual:
                                // a * 10 != 9999 is always true
                                result = new ParseErrorExpression("Result is always true using integer math", comparisonExpression);
                                return false;

                            case ComparisonOperation.LessThan:
                                // a * 10 < 9999 becomes a <= 999
                                comparisonOperation = ComparisonOperation.LessThanOrEqual;
                                break;

                            case ComparisonOperation.LessThanOrEqual:
                                // a * 10 <= 9999 becomes a <= 999
                                break;

                            case ComparisonOperation.GreaterThan:
                                // a * 10 > 9999 becomes a > 999
                                break;

                            case ComparisonOperation.GreaterThanOrEqual:
                                // a * 10 >= 9999 becomes a > 999
                                comparisonOperation = ComparisonOperation.GreaterThan;
                                break;
                        }
                    }
                }

                // construct the new equation
                newRoot = new ComparisonExpression(mathematicLeft.Left, comparisonOperation, newRight);
            }

            // recurse if applicable
            if (newRoot.Left.Type == ExpressionType.Mathematic)
                return MoveConstantsToRightHandSide(newRoot, scope, out result);

            comparisonExpression.CopyLocation(newRoot);
            result = newRoot;
            return true;
        }

        private static ComparisonExpression EnsureSingleExpressionOnRightHandSide(ComparisonExpression comparison, InterpreterScope scope)
        {
            MathematicExpression newLeft;
            ComparisonExpression newRoot;

            // if the right hand side of the comparison is a mathematic, shift part of it to the left
            // hand side so the right hand side eventually has a single value/address
            var mathematic = comparison.Right as MathematicExpression;
            if (mathematic == null)
                return comparison;

            bool moveLeft = true;

            switch (mathematic.Operation)
            {
                case MathematicOperation.Add:
                    if (mathematic.Right.Type == ExpressionType.IntegerConstant)
                    {
                        if (mathematic.Left.Type == ExpressionType.Mathematic)
                        {
                            var temp = new ComparisonExpression(comparison.Left, comparison.Operation, mathematic.Left);
                            temp = EnsureSingleExpressionOnRightHandSide(temp, scope);

                            mathematic = new MathematicExpression(temp.Right, MathematicOperation.Add, mathematic.Right);
                            comparison = new ComparisonExpression(temp.Left, temp.Operation, mathematic);
                        }

                        // prefer to move the IntegerConstant unless a MemoryAccessor can be moved
                        // note: prev() hides the memory accessor, so the IntegerConstant is given preference
                        moveLeft = false;
                    }
                    break;

                case MathematicOperation.Subtract:
                    // the rightmost part of the right side will be moved to the left side as an addition
                    moveLeft = false;
                    break;

                default:
                    //result = new ParseErrorExpression("Cannot eliminate " + MathematicExpression.GetOperatorType(mathematic.Operation) +
                    //    " from right side of comparison", comparisonExpression);
                    //return false;
                    return comparison;
            }

            if (moveLeft)
            {
                // left side is implicitly added on the right, so explicitly subtract it on the left
                newLeft = new MathematicExpression(comparison.Left, MathematicOperation.Subtract, mathematic.Left);
                newRoot = new ComparisonExpression(newLeft, comparison.Operation, mathematic.Right);
            }
            else
            {
                // invert the operation when moving the right side to the left
                newLeft = new MathematicExpression(comparison.Left, MathematicExpression.GetOppositeOperation(mathematic.Operation), mathematic.Right);
                newRoot = new ComparisonExpression(newLeft, comparison.Operation, mathematic.Left);
            }

            // ensure the IntegerConstant is the rightmost element of the left side
            mathematic = comparison.Left as MathematicExpression;
            if (mathematic != null && mathematic.Right is IntegerConstantExpression)
            {
                newLeft.Left = mathematic.Left;
                mathematic.Left = newLeft;
                newRoot.Left = mathematic;
            }

            // recurse if necessary
            return EnsureSingleExpressionOnRightHandSide(newRoot, scope);
        }

        private static MemoryAccessorFunction GetMemoryAccessor(ExpressionBase expression, InterpreterScope scope)
        {
            var functionCall = expression as FunctionCallExpression;
            if (functionCall != null && functionCall.Parameters.Count > 0)
            {
                var functionDefinition = scope.GetFunction(functionCall.FunctionName.Name);
                var memoryAccesor = functionDefinition as MemoryAccessorFunction;
                if (memoryAccesor != null)
                    return memoryAccesor;

                var prevPriorFunction = functionDefinition as PrevPriorFunction;
                if (prevPriorFunction != null)
                    return GetMemoryAccessor(functionCall.Parameters.First(), scope);
            }

            return null;
        }

        private static void GetMinMax(ExpressionBase expression, InterpreterScope scope, out long min, out long max)
        {
            switch (expression.Type)
            {
                case ExpressionType.Mathematic:
                    var mathematic = (MathematicExpression)expression;

                    long leftMin, leftMax;
                    GetMinMax(mathematic.Left, scope, out leftMin, out leftMax);

                    long rightMin, rightMax;
                    GetMinMax(mathematic.Right, scope, out rightMin, out rightMax);

                    switch (mathematic.Operation)
                    {
                        default:
                        case MathematicOperation.Add:
                            min = leftMin + rightMin;
                            max = leftMax + rightMax;
                            break;

                        case MathematicOperation.Subtract:
                            min = leftMin - rightMax;
                            max = leftMax - rightMin;
                            break;

                        case MathematicOperation.Multiply:
                            min = leftMin * rightMin;
                            max = leftMax * rightMax;
                            break;

                        case MathematicOperation.Divide:
                            if (mathematic.Left == mathematic.Right)
                            {
                                // A/A is either 0 or 1
                                min = 0;
                                max = 1;
                            }
                            else
                            {
                                // division by 0 will always return 0, so assume division by 1
                                min = (rightMax == 0) ? leftMin : leftMin / rightMax;
                                max = (rightMin == 0) ? leftMax : leftMax / rightMin;
                            }
                            break;

                        case MathematicOperation.Modulus:
                            // modulus will return at most right-1
                            min = 0;
                            max = rightMax - 1;
                            break;
                    }
                    break;

                case ExpressionType.IntegerConstant:
                    min = max = (uint)((IntegerConstantExpression)expression).Value;
                    break;

                case ExpressionType.FunctionCall:
                    min = 0;
                    var memoryAccessor = GetMemoryAccessor(expression, scope);
                    if (memoryAccessor != null)
                        max = Field.GetMaxValue(memoryAccessor.Size);
                    else
                        max = 0xFFFFFFFF; // unknown function, assume it can return the full range of values
                    break;

                default:
                    min = 0;
                    max = 0xFFFFFFFF;
                    break;
            }
        }

        private static bool HasDword(ExpressionBase expression, InterpreterScope scope)
        {
            var mathematic = expression as MathematicExpression;
            if (mathematic != null)
            {
                if (HasDword(mathematic.Left, scope) || HasDword(mathematic.Right, scope))
                    return true;
            }

            var functionCall = expression as FunctionCallExpression;
            if (functionCall != null)
            {
                var memoryAccessor = GetMemoryAccessor(functionCall, scope);
                if (memoryAccessor != null)
                {
                    if (memoryAccessor.Size == FieldSize.DWord)
                        return true;
                }
                else
                {
                    // not a memory accessor function, assume the parameters might become reads. check them too
                    foreach (var parameter in functionCall.Parameters)
                    {
                        if (HasDword(parameter, scope))
                            return true;
                    }
                }
            }

            return false;
        }

        private static bool HasSubtract(ExpressionBase expression)
        {
            var mathematic = expression as MathematicExpression;
            if (mathematic != null)
            {
                if (mathematic.Operation == MathematicOperation.Subtract)
                    return true;

                if (HasSubtract(mathematic.Left) || HasSubtract(mathematic.Right))
                    return true;
            }

            return false;
        }

        static ExpressionBase AddConstant(ExpressionBase expr, int constant)
        {
            var integerConstant = expr as IntegerConstantExpression;
            if (integerConstant != null)
                return new IntegerConstantExpression(integerConstant.Value + constant);

            var mathematic = expr as MathematicExpression;
            if (mathematic != null)
            {
                integerConstant = mathematic.Right as IntegerConstantExpression;
                if (integerConstant != null)
                {
                    if (mathematic.Operation == MathematicOperation.Add)
                    {
                        integerConstant = new IntegerConstantExpression(integerConstant.Value + constant);
                        return new MathematicExpression(mathematic.Left, MathematicOperation.Add, integerConstant);
                    }
                    else if (mathematic.Operation == MathematicOperation.Subtract)
                    {
                        if (integerConstant.Value > constant)
                        {
                            integerConstant = new IntegerConstantExpression(integerConstant.Value - constant);
                            return new MathematicExpression(mathematic.Left, MathematicOperation.Subtract, integerConstant);
                        }
                        else
                        {
                            integerConstant = new IntegerConstantExpression(constant - integerConstant.Value);
                            return new MathematicExpression(mathematic.Left, MathematicOperation.Add, integerConstant);
                        }
                    }
                }
            }

            return new MathematicExpression(expr, MathematicOperation.Add, new IntegerConstantExpression(constant));
        }

        static int ExtractConstant(ref ExpressionBase expr)
        {
            // ASSERT: BubbleUpIntegerConstants was previously called and any integer that is present
            // will be the right part of the root node, or the root node itself
            var mathematic = expr as MathematicExpression;
            if (mathematic != null)
            {
                var integerConstant = mathematic.Right as IntegerConstantExpression;
                if (integerConstant != null)
                {
                    // if an integer constant addition/subtraction was found, remove it
                    switch (mathematic.Operation)
                    {
                        case MathematicOperation.Add:
                            expr = mathematic.Left;
                            return integerConstant.Value;

                        case MathematicOperation.Subtract:
                            expr = mathematic.Left;
                            return -integerConstant.Value;

                        default:
                            break;
                    }
                }
            }
            else
            {
                // if there's only an integer constant, replace it with 0 and return its value
                var integerConstant = expr as IntegerConstantExpression;
                if (integerConstant != null)
                {
                    expr = new IntegerConstantExpression(0);
                    return integerConstant.Value;
                }
            }

            return 0;
        }

        private static bool HasUnderflowAdjustment(ExpressionBase expression, bool invert, 
            ref bool hasConstant, ref bool hasSubtractedFunction, ref bool hasNonSubtractedFunction)
        {
            var mathematic = expression as MathematicExpression;
            if (mathematic != null)
            {
                if (mathematic.Operation == MathematicOperation.Add || mathematic.Operation == MathematicOperation.Subtract)
                {
                    if (mathematic.Left.Type == ExpressionType.IntegerConstant ||
                        mathematic.Right.Type == ExpressionType.IntegerConstant)
                    {
                        // explicit modification - assume underflow adjustment
                        hasConstant = true;
                        if (hasSubtractedFunction && hasNonSubtractedFunction)
                            return true;
                    }

                    if (HasUnderflowAdjustment(mathematic.Left, invert, ref hasConstant,
                        ref hasSubtractedFunction, ref hasNonSubtractedFunction))
                    {
                        return true;
                    }

                    if (mathematic.Operation == MathematicOperation.Subtract)
                        invert = !invert;

                    if (HasUnderflowAdjustment(mathematic.Right, invert, ref hasConstant,
                        ref hasSubtractedFunction, ref hasNonSubtractedFunction))
                    {
                        return true;
                    }
                }
            }
            else
            {
                var functionCall = expression as FunctionCallExpression;
                if (functionCall != null)
                {
                    if (invert)
                        hasSubtractedFunction = true;
                    else
                        hasNonSubtractedFunction = true;

                    if (hasConstant && hasSubtractedFunction && hasNonSubtractedFunction)
                        return true;
                }
            }

            return false;
        }

        private static bool HasUnderflowAdjustment(MathematicExpression expression)
        {
            bool hasConstant = false;
            bool hasSubtractedFunction = false;
            bool hasNonSubtractedFunction = false;
            return HasUnderflowAdjustment(expression, false, ref hasConstant, ref hasSubtractedFunction, ref hasNonSubtractedFunction);
        }

        private static ComparisonExpression RearrangeToAvoidSubtraction(ComparisonExpression comparison)
        {
            var operation = comparison.Operation;

            // extract integer constant from the left side
            var left = comparison.Left;
            var leftConstant = ExtractConstant(ref left);

            // extract integer constant from the right side
            var right = comparison.Right;
            var rightConstant = ExtractConstant(ref right);

            // ExtractConstant should remove the constants from both sides. if any are left, the expression
            // was only a constant. if both were only constants, don't do any further processing.
            if (left.Type == ExpressionType.IntegerConstant && right.Type == ExpressionType.IntegerConstant)
                return comparison;

            // merge the left constant with the right constant
            rightConstant -= leftConstant;

            // if no constant remains, just construct the comparison
            if (rightConstant == 0)
                return new ComparisonExpression(left, operation, right);

            if (rightConstant > 0)
            {
                var integerConstant = new IntegerConstantExpression(rightConstant);
                if (right.Type == ExpressionType.Mathematic)
                {
                    // A - N > B - C
                    var newLeft = new MathematicExpression(left, MathematicOperation.Subtract, right);
                    return new ComparisonExpression(newLeft, operation, integerConstant);
                }
                else
                {
                    var mathematicLeft = left as MathematicExpression;
                    if (mathematicLeft == null)
                    {
                        if (operation == ComparisonOperation.Equal || operation == ComparisonOperation.NotEqual)
                        {
                            // A - N == B  ~>  A - N == B
                            var newLeft = new MathematicExpression(left, MathematicOperation.Subtract, integerConstant);
                            return new ComparisonExpression(newLeft, operation, right);
                        }
                        else
                        {
                            // A - N < B  ~>  B + N > A
                            var newLeft = new MathematicExpression(right, MathematicOperation.Add, integerConstant);
                            return new ComparisonExpression(newLeft, InvertComparisonOperation(operation), left);
                        }
                    }

                    if (mathematicLeft.Operation == MathematicOperation.Subtract)
                    {
                        var newRight = AddConstant(right, rightConstant);
                        switch (operation)
                        {
                            case ComparisonOperation.GreaterThan:
                            case ComparisonOperation.GreaterThanOrEqual:
                            {
                                // A - B > N  ~>  N + B < A
                                var newLeft = new MathematicExpression(mathematicLeft.Right, MathematicOperation.Add, newRight);
                                return new ComparisonExpression(newLeft, InvertComparisonOperation(operation), mathematicLeft.Left);
                            }

                            default:
                                if (right.Type != ExpressionType.IntegerConstant)
                                {
                                    // A - B == C + N  ~>  A - B - C == N
                                    // A - B <= C + N  ~>  A - B - C <= N  [will have underflow added later]
                                    var newLeft = new MathematicExpression(left, MathematicOperation.Subtract, right);
                                    return new ComparisonExpression(newLeft, operation, integerConstant);
                                }
                                else
                                {
                                    // A - B == N  ~>  A - B == N
                                    // A - B <= N  ~>  A - B <= N  [will have underflow added later]
                                    return new ComparisonExpression(left, operation, newRight);
                                }
                        }
                    }

                    // A + B > N  ~>  A + B > N
                    return new ComparisonExpression(left, operation, integerConstant);
                }
            }
            else // if (rightConstant < 0)
            {
                var integerConstant = new IntegerConstantExpression(-rightConstant);

                var mathematicLeft = left as MathematicExpression;
                if (mathematicLeft == null)
                {
                    // A + N < B
                    var newLeft = new MathematicExpression(left, MathematicOperation.Add, integerConstant);
                    return new ComparisonExpression(newLeft, operation, right);
                }

                if (mathematicLeft.Operation == MathematicOperation.Subtract && mathematicLeft.Left.Type != ExpressionType.Mathematic)
                {
                    if (mathematicLeft.Left.Type == ExpressionType.IntegerConstant)
                    {
                        // 0 - A < -N  ~>  A > N

                        // any other value would have been removed by ExtractConstant.
                        // the 0 is necessary to perform the subtraction.
                        Debug.Assert(((IntegerConstantExpression)mathematicLeft.Left).Value == 0);

                        return new ComparisonExpression(mathematicLeft.Right, InvertComparisonOperation(operation), integerConstant);
                    }

                    // A - B < -N  ~>  A + N < B    [right is not mathematic per condition above]
                    var newRight = AddConstant(right, -rightConstant);
                    var newLeft = new MathematicExpression(mathematicLeft.Left, MathematicOperation.Add, newRight);
                    return new ComparisonExpression(newLeft, operation, mathematicLeft.Right);
                }
                else
                {
                    // A - B - C < -N  ~>  no change, let underflow handle it
                    var newRight = AddConstant(right, rightConstant);
                    return new ComparisonExpression(left, operation, newRight);
                }
            }
        }

        private static bool CheckForUnderflow(ComparisonExpression comparison, InterpreterScope scope, out ExpressionBase result)
        {
            var right = comparison.Right;
            var mathematicRight = right as MathematicExpression;
            var left = comparison.Left;
            var mathematicLeft = left as MathematicExpression;
            bool checkForUnderflow = true;

            if (mathematicLeft == null && mathematicRight == null)
            {
                // simple values on both sides
                checkForUnderflow = false;
            }
            else
            {
                // if there's an explicit modification on the left hand side, and it's not an equality
                // comparison, and there's a possibility of underflow (subtraction present), then assume
                // it's a user-defined underflow adjustment and don't make any further modifications.
                if (comparison.Operation != ComparisonOperation.Equal &&
                    comparison.Operation != ComparisonOperation.NotEqual &&
                    mathematicLeft != null && HasUnderflowAdjustment(mathematicLeft))
                {
                    checkForUnderflow = false;
                }

                // bubble up integer constants on both sides.
                if (mathematicLeft != null)
                    left = MathematicExpression.BubbleUpIntegerConstant(mathematicLeft);

                if (mathematicRight != null)
                    right = MathematicExpression.BubbleUpIntegerConstant(mathematicRight);

                // BubbleUpIntegerConstant modifies the tree. update comparison
                comparison = new ComparisonExpression(left, comparison.Operation, right);

                // if the result of subtracting two bytes is negative, it becomes a very large positive number.
                // so a check for less than some byte value may fail. Attempt to eliminate subtractions.
                if (checkForUnderflow)
                    comparison = RearrangeToAvoidSubtraction(comparison);
            }

            // shift stuff around so there's only a single expression on the right hand side
            comparison = EnsureSingleExpressionOnRightHandSide(comparison, scope);

            // if subtractions still exist, add a constant to both sides of the equation to prevent the
            // subtraction from resulting in a negative number.
            switch (comparison.Operation)
            {
                case ComparisonOperation.Equal:
                case ComparisonOperation.NotEqual:
                    // direct comparisons aren't affected by underflow.
                    checkForUnderflow = false;
                    break;

                case ComparisonOperation.GreaterThan:
                case ComparisonOperation.GreaterThanOrEqual:
                    // greater than comparisons are always affected by underflow, even if a user-defined adjustment is provided
                    checkForUnderflow = true;
                    break;
            }

            // if the right side is a negative constant, attempt to adjust for it by forcing an underflow check
            var integerConstant = comparison.Right as IntegerConstantExpression;
            if (integerConstant != null && integerConstant.Value < 0)
            {
                if (HasSubtract(comparison.Left))
                {
                    // if the left side has a subtraction, the result could be negative. handle it with underflow
                    checkForUnderflow = true;
                }
                else if (HasDword(comparison.Left, scope))
                {
                    // if there's a 32-bit read, the value might not be signed. don't adjust it
                }
                else
                {
                    // no 32-bit reads, and no subtraction - the comparison will never be true
                    result = new ParseErrorExpression("Expression can never be true");
                    return false;
                }
            }

            if (checkForUnderflow)
            {
                // if the equation has a 32-bit read, the value may not actually be signed
                mathematicLeft = comparison.Left as MathematicExpression;
                if (mathematicLeft != null && !HasDword(mathematicLeft, scope))
                {
                    long min, max;
                    GetMinMax(mathematicLeft, scope, out min, out max);
                    var underflowAdjustment = -(int)min;

                    // attempt to adjust the negative value up to 0.
                    if (integerConstant != null && integerConstant.Value < 0)
                    {
                        var negativeAdjustment = -integerConstant.Value;
                        if (underflowAdjustment < negativeAdjustment)
                            underflowAdjustment = negativeAdjustment;
                    }

                    if (underflowAdjustment > 0)
                    {
                        if (comparison.Right is IntegerConstantExpression)
                        {
                            var newLeft = AddConstant(comparison.Left, underflowAdjustment);
                            var newRight = AddConstant(comparison.Right, underflowAdjustment);
                            comparison = new ComparisonExpression(newLeft, comparison.Operation, newRight);
                        }
                        else
                        {
                            GetMinMax(comparison.Right, scope, out min, out max);
                            underflowAdjustment += (int)max;

                            ExpressionBase newLeft = new MathematicExpression(comparison.Left, MathematicOperation.Subtract, comparison.Right);
                            newLeft = AddConstant(newLeft, underflowAdjustment);
                            var newRight = new IntegerConstantExpression(underflowAdjustment);
                            comparison = new ComparisonExpression(newLeft, comparison.Operation, newRight);
                        }
                    }
                }
            }

            result = comparison;
            return true;
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
            if (_fullyExpanded)
            {
                result = this;
                return true;
            }

            // start with simple substitution
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

            // if the same operation is being applied to both sides, just cancel it out
            var mathematicRight = right as MathematicExpression;
            var mathematicLeft = left as MathematicExpression;
            while (mathematicLeft != null && mathematicRight != null &&
                mathematicLeft.Operation == mathematicRight.Operation &&
                mathematicLeft.Right == mathematicRight.Right)
            {
                left = mathematicLeft.Left;
                right = mathematicRight.Left;

                mathematicRight = right as MathematicExpression;
                mathematicLeft = left as MathematicExpression;
            }

            // prefer "a == 1" over "1 == a"
            var comparison = new ComparisonExpression(left, Operation, right);
            if (!MoveConstantsToRightHandSide(comparison, scope, out result))
                return false;
            comparison = (ComparisonExpression)result;

            // check for underflow
            if (!CheckForUnderflow(comparison, scope, out result))
            {
                CopyLocation(result);
                return false;
            }
            comparison = (ComparisonExpression)result;

            // if the result is unchanged, prevent reprocessing the source and return it
            if (comparison == this)
            {
                this._fullyExpanded = true;
                result = this;
                return true;
            }

            // if the expression can be fully evaluated, do so
            ParseErrorExpression error;
            var comparisonResult = comparison.IsTrue(scope, out error);
            if (error != null)
            {
                result = error;
                return false;
            }

            if (comparisonResult != null)
            {
                // result of comparison is known, return a boolean
                result = new BooleanConstantExpression(comparisonResult.GetValueOrDefault());
            }
            else
            {
                // prevent reprocessing the result and return it
                comparison._fullyExpanded = true;
                result = comparison;
            }

            CopyLocation(result);
            return true;
        }

        /// <summary>
        /// Rebalances this expression based on the precendence of operators.
        /// </summary>
        /// <returns>
        /// Rebalanced expression
        /// </returns>
        internal override ExpressionBase Rebalance()
        {
            if (!Right.IsLogicalUnit)
            {
                var conditionalRight = Right as ConditionalExpression;
                if (conditionalRight != null)
                    return Rebalance(conditionalRight);
            }

            return base.Rebalance();
        }

        /// <summary>
        /// Determines whether the expression evaluates to true for the provided <paramref name="scope" />
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="error">[out] The error that prevented evaluation (or null if successful).</param>
        /// <returns>
        /// The result of evaluating the expression
        /// </returns>
        public override bool? IsTrue(InterpreterScope scope, out ParseErrorExpression error)
        {
            ExpressionBase left, right;
            if (!Left.ReplaceVariables(scope, out left))
            {
                error = left as ParseErrorExpression;
                return null;
            }

            if (!Right.ReplaceVariables(scope, out right))
            {
                error = right as ParseErrorExpression;
                return null;
            }

            error = null;

            var integerLeft = left as IntegerConstantExpression;
            if (integerLeft != null)
            {
                var integerRight = right as IntegerConstantExpression;
                if (integerRight == null)
                    return null;

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
                        return null;
                }
            }

            var booleanLeft = left as BooleanConstantExpression;
            if (booleanLeft != null)
            {
                var booleanRight = right as BooleanConstantExpression;
                if (booleanRight == null)
                    return null;

                switch (Operation)
                {
                    case ComparisonOperation.Equal:
                        return booleanLeft.Value == booleanRight.Value;
                    case ComparisonOperation.NotEqual:
                        return booleanLeft.Value != booleanRight.Value;
                    default:
                        error = new ParseErrorExpression("Cannot perform relative comparison on boolean values", this);
                        return null;
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
                        return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether the specified <see cref="ComparisonExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="ComparisonExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="ComparisonExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as ComparisonExpression;
            return that != null && Operation == that.Operation && Left == that.Left && Right == that.Right;
        }
    }

    /// <summary>
    /// Specifies how the two sides of the <see cref="ComparisonExpression"/> should be compared.
    /// </summary>
    public enum ComparisonOperation
    {
        /// <summary>
        /// Unspecified.
        /// </summary>
        None = 0,

        /// <summary>
        /// The left and right values are equivalent.
        /// </summary>
        Equal,

        /// <summary>
        /// The left and right values are not equivalent.
        /// </summary>
        NotEqual,

        /// <summary>
        /// The left value is less than the right value.
        /// </summary>
        LessThan,

        /// <summary>
        /// The left value is less than or equal to the right value.
        /// </summary>
        LessThanOrEqual,

        /// <summary>
        /// The left value is greater than the right value.
        /// </summary>
        GreaterThan,

        /// <summary>
        /// The left value is greater than or equal to the right value.
        /// </summary>
        GreaterThanOrEqual,
    }
}
