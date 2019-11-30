using RATools.Data;
using RATools.Parser.Functions;
using System;
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

        private bool MoveConstantsToRightHandSide(ComparisonExpression comparisonExpression, InterpreterScope scope, out ExpressionBase result)
        {
            ComparisonExpression newRoot;
            var mathematic = (MathematicExpression)comparisonExpression.Left;

            // if the rightmost expression of the left side of the comparison is an integer, shift it to the 
            // right side of the equation and attempt to merge it with whatever is already there.
            var integer = mathematic.Right as IntegerConstantExpression;
            if (integer == null)
            {
                result = comparisonExpression;
                return true;
            }

            var mathematicRight = comparisonExpression.Right as MathematicExpression;
            var mathematicLeft = comparisonExpression.Left as MathematicExpression;
            if (mathematicLeft != null && mathematicRight != null &&
                mathematicLeft.Operation == mathematicRight.Operation &&
                mathematicLeft.Right == mathematicRight.Right)
            {
                // same operation being applied to both sides, just cancel it out
                newRoot = new ComparisonExpression(mathematicLeft.Left, comparisonExpression.Operation, mathematicRight.Left);
            }
            else
            {
                // apply the inverse of the mathematical operation to generate a new right side
                var operation = MathematicExpression.GetOppositeOperation(mathematic.Operation);
                var right = new MathematicExpression(comparisonExpression.Right, operation, mathematic.Right);
                if (!right.ReplaceVariables(scope, out result))
                    return false;

                var newRight = result;
                var comparisonOperation = comparisonExpression.Operation;

                // multiplication is converted to division. if the division is not exact, modify the comparison 
                // so its still logically valid (if possible).
                if (operation == MathematicOperation.Divide && newRight is IntegerConstantExpression)
                {
                    var reversed = new MathematicExpression(result, MathematicOperation.Multiply, mathematic.Right);
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
                                // a * 10 < 9999 becomes a < 999
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
                newRoot = new ComparisonExpression(mathematic.Left, comparisonOperation, newRight);
            }

            // recurse if applicable
            if (newRoot.Left.Type == ExpressionType.Mathematic)
                return MoveConstantsToRightHandSide(newRoot, scope, out result);

            comparisonExpression.CopyLocation(newRoot);
            result = newRoot;
            return true;
        }

        private bool EnsureSingleExpressionOnRightHandSide(ComparisonExpression comparisonExpression, InterpreterScope scope, ref int underflowAdjustment, out ExpressionBase result)
        {
            MathematicExpression newLeft;
            ComparisonExpression newRoot;

            // if the right hand side of the comparison is a mathematic, shift part of it to the left hand side so 
            // the right hand side eventually has a single value/address
            var mathematic = (MathematicExpression)comparisonExpression.Right;
            bool moveLeft = true;

            switch (mathematic.Operation)
            {
                case MathematicOperation.Add:
                    // the leftmost part of the right side will be moved to the left side as a subtraction.
                    // when subtracting a memory accessor, the result could be negative. since we're using
                    // unsigned values, this could result in a very high positive number. if not doing an exact
                    // comparison, check for potential underflow and offset total calculation to prevent it.
                    if (comparisonExpression.Operation != ComparisonOperation.Equal && comparisonExpression.Operation != ComparisonOperation.NotEqual)
                    {
                        var functionCall = mathematic.Left as FunctionCallExpression;
                        if (functionCall != null)
                        {
                            var memoryAccessor = scope.GetFunction(functionCall.FunctionName.Name) as MemoryAccessorFunction;
                            if (memoryAccessor != null && memoryAccessor.Size != FieldSize.DWord)
                                underflowAdjustment += (int)Field.GetMaxValue(memoryAccessor.Size);
                        }
                    }

                    if (mathematic.Right is IntegerConstantExpression)
                        moveLeft = false;
                    break;

                case MathematicOperation.Subtract:
                    // the rightmost part of the right side will be moved to the left side as an addition
                    moveLeft = false;
                    break;

                default:
                    result = new ParseErrorExpression("Cannot eliminate " + MathematicExpression.GetOperatorType(mathematic.Operation) +
                        " from right side of comparison", comparisonExpression);
                    return false;
            }

            if (moveLeft)
            {
                // left side is implicitly added on the right, so explicitly subtract it on the left
                newLeft = new MathematicExpression(comparisonExpression.Left, MathematicOperation.Subtract, mathematic.Left);
                newRoot = new ComparisonExpression(newLeft, comparisonExpression.Operation, mathematic.Right);
            }
            else
            {
                // invert the operation when moving the right side to the left
                newLeft = new MathematicExpression(comparisonExpression.Left, MathematicExpression.GetOppositeOperation(mathematic.Operation), mathematic.Right);
                newRoot = new ComparisonExpression(newLeft, comparisonExpression.Operation, mathematic.Left);
            }

            // ensure the IntegerConstant is the rightmost element of the left side
            mathematic = comparisonExpression.Left as MathematicExpression;
            if (mathematic != null && mathematic.Right is IntegerConstantExpression)
            {
                newLeft.Left = mathematic.Left;
                mathematic.Left = newLeft;
                newRoot.Left = mathematic;
            }

            // recurse if necessary
            if (newRoot.Right is MathematicExpression)
                return EnsureSingleExpressionOnRightHandSide(newRoot, scope, ref underflowAdjustment, out result);

            result = newRoot;
            return true;
        }

        private static int CalculateUnderflow(MathematicExpression mathematic, InterpreterScope scope, bool invert, bool hasSubtract)
        {
            int underflowAdjustment = 0;

            var subsourceOperation = invert ? MathematicOperation.Add : MathematicOperation.Subtract;
            if (mathematic.Operation == subsourceOperation)
            {
                var functionCall = mathematic.Right as FunctionCallExpression;
                if (functionCall != null)
                {
                    var memoryAccessor = scope.GetFunction(functionCall.FunctionName.Name) as MemoryAccessorFunction;
                    if (memoryAccessor != null && memoryAccessor.Size != FieldSize.DWord)
                        underflowAdjustment += (int)Field.GetMaxValue(memoryAccessor.Size);
                }
            }
            else if (hasSubtract)
            {
                var functionCall = mathematic.Left as FunctionCallExpression;
                if (functionCall != null)
                {
                    var memoryAccessor = scope.GetFunction(functionCall.FunctionName.Name) as MemoryAccessorFunction;
                    if (memoryAccessor != null && memoryAccessor.Size != FieldSize.DWord)
                        underflowAdjustment += (int)Field.GetMaxValue(memoryAccessor.Size);
                }
            }

            var mathematicLeft = mathematic.Left as MathematicExpression;
            if (mathematicLeft != null)
                underflowAdjustment += CalculateUnderflow(mathematicLeft, scope, invert, hasSubtract);

            var mathematicRight = mathematic.Right as MathematicExpression;
            if (mathematicRight != null)
            {
                if (mathematic.Operation == MathematicOperation.Subtract)
                    underflowAdjustment += CalculateUnderflow(mathematicRight, scope, !invert, true);
                else
                    underflowAdjustment += CalculateUnderflow(mathematicRight, scope, invert, hasSubtract);
            }

            return underflowAdjustment;
        }

        private static bool MergeAddSource(ExpressionBase expression, int adjustment)
        {
            var mathematic = expression as MathematicExpression;
            if (mathematic != null)
            {
                if (mathematic.Operation == MathematicOperation.Add || mathematic.Operation == MathematicOperation.Subtract)
                {
                    var left = mathematic.Left as IntegerConstantExpression;
                    if (left != null)
                    {
                        mathematic.Left = new IntegerConstantExpression(left.Value + adjustment);
                        return true;
                    }

                    var right = mathematic.Right as IntegerConstantExpression;
                    if (right != null)
                    {
                        if (mathematic.Operation == MathematicOperation.Add)
                        {
                            mathematic.Right = new IntegerConstantExpression(right.Value + adjustment);
                            return true;
                        }

                        if (mathematic.Operation == MathematicOperation.Subtract)
                        {
                            mathematic.Right = new IntegerConstantExpression(right.Value - adjustment);
                            return true;
                        }
                    }
                }

                if (MergeAddSource(mathematic.Left, adjustment))
                    return true;
                if (MergeAddSource(mathematic.Right, adjustment))
                    return true;
            }

            return false;
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
                var memoryAccessor = scope.GetFunction(functionCall.FunctionName.Name) as MemoryAccessorFunction;
                if (memoryAccessor != null && memoryAccessor.Size == FieldSize.DWord)
                    return true;

                foreach (var parameter in functionCall.Parameters)
                {
                    if (HasDword(parameter, scope))
                        return true;
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

            // bubble any integer constants up to the top-most node in the tree
            if (mathematicRight != null)
                right = MathematicExpression.BubbleUpIntegerConstant(mathematicRight);

            int leftConstant = 0;
            if (mathematicLeft != null)
            {
                var newLeft = MathematicExpression.BubbleUpIntegerConstant(mathematicLeft);
                if (newLeft.Right.Type == ExpressionType.IntegerConstant)
                    leftConstant = ((IntegerConstantExpression)newLeft.Right).Value;
                left = newLeft;
            }

            // if the comparison is not direct equality/inequality, need to check for underflow
            int underflowAdjustment = 0;
            if (Operation != ComparisonOperation.Equal && Operation != ComparisonOperation.NotEqual)
            {
                mathematicLeft = left as MathematicExpression;
                if (mathematicLeft != null)
                    underflowAdjustment = CalculateUnderflow(mathematicLeft, scope, false, false);
            }

            // transfer the integer constants to the right side of the comparison
            var comparison = new ComparisonExpression(left, Operation, right);
            if (!MoveConstantsToRightHandSide(comparison, scope, out result))
                return false;
            comparison = (ComparisonExpression)result;

            // only one field is allowed on right side of the comparison
            if (comparison.Right.Type == ExpressionType.Mathematic)
            {
                if (!EnsureSingleExpressionOnRightHandSide(comparison, scope, ref underflowAdjustment, out result))
                    return false;

                comparison = (ComparisonExpression)result;
            }

            // if the value being compared against is negative, adjust it up to zero
            if (comparison.Right.Type == ExpressionType.IntegerConstant)
            {
                var value = ((IntegerConstantExpression)comparison.Right).Value;
                if (value < 0)
                {
                    if (HasSubtract(comparison.Left))
                    {
                        // if the left side has a subtraction, the result could be negative, adjust it up to zero
                        underflowAdjustment += -value;
                    }
                    else if (HasDword(comparison.Left, scope))
                    {
                        // if the left side has a 32-bit read, the value may not actually be signed, don't adjust it
                        underflowAdjustment = 0;
                    }
                    else
                    {
                        // no 32-bit values and no subtraction - cannot generate a value where the sign bit is set
                        result = new ParseErrorExpression("Expression can never be true", this);
                        return false;
                    }
                }
            }

            // incorporate the underflow adjustment
            if (underflowAdjustment > 0)
            {
                // if an adjustment is necessary for a less than comparison, and the user has already provided one, use it.                if (Operation == ComparisonOperation.LessThan || Operation == ComparisonOperation.LessThanOrEqual)
                if (leftConstant != 0 && (Operation == ComparisonOperation.LessThan || Operation == ComparisonOperation.LessThanOrEqual))
                    underflowAdjustment = leftConstant;

                // add a dummy variable to the right side and rebalance again to move the existing right hand side to the left hand side
                var newRight = new MathematicExpression(comparison.Right, MathematicOperation.Add, new VariableExpression("unused"));
                comparison = new ComparisonExpression(comparison.Left, comparison.Operation, newRight);

                if (!EnsureSingleExpressionOnRightHandSide(comparison, scope, ref underflowAdjustment, out result))
                    return false;

                comparison = (ComparisonExpression)result;
                System.Diagnostics.Debug.Assert(comparison.Right is VariableExpression);

                // add the new underflow to both sides of the comparison
                MathematicExpression newLeft = comparison.Left as MathematicExpression;
                if (newLeft != null && newLeft.Right is IntegerConstantExpression)
                {
                    var value = ((IntegerConstantExpression)newLeft.Right).Value;
                    if (newLeft.Operation == MathematicOperation.Add)
                    {
                        newLeft.Right = new IntegerConstantExpression(value + underflowAdjustment);
                        comparison = new ComparisonExpression(newLeft, comparison.Operation, new IntegerConstantExpression(underflowAdjustment));
                    }
                    else if (newLeft.Operation == MathematicOperation.Subtract)
                    {
                        if (MergeAddSource(newLeft.Left, underflowAdjustment))
                        {
                            comparison = new ComparisonExpression(newLeft.Left, comparison.Operation, new IntegerConstantExpression(underflowAdjustment + value)); ;
                        }
                        else
                        {
                            newLeft = new MathematicExpression(newLeft.Left, MathematicOperation.Add, new IntegerConstantExpression(underflowAdjustment));
                            comparison = new ComparisonExpression(newLeft, comparison.Operation, new IntegerConstantExpression(underflowAdjustment + value));
                        }
                    }
                }
                else
                {
                    newLeft = new MathematicExpression(comparison.Left, MathematicOperation.Add, new IntegerConstantExpression(underflowAdjustment));
                    comparison = new ComparisonExpression(newLeft, comparison.Operation, new IntegerConstantExpression(underflowAdjustment));
                }
            }

            // copy the location information to the final result and return it
            result = comparison;
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

        /// <summary>
        /// Determines whether the specified <see cref="ComparisonExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="ComparisonExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="ComparisonExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = (ComparisonExpression)obj;
            return Operation == that.Operation && Left == that.Left && Right == that.Right;
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
