﻿using RATools.Data;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions
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

        internal static ComparisonOperation ReverseComparisonOperation(ComparisonOperation op)
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

        private static bool ExtractBCD(ExpressionBase expression, out ExpressionBase newExpression)
        {
            var bcdWrapper = expression as BinaryCodedDecimalExpression;
            if (bcdWrapper != null)
            {
                newExpression = new MemoryAccessorExpression(bcdWrapper);
                return true;
            }

            newExpression = expression;
            return false;
        }

        private static bool ConvertToBCD(ExpressionBase expression, out ExpressionBase newExpression)
        {
            var integerExpression = expression as IntegerConstantExpression;
            if (integerExpression != null)
            {
                int newValue = 0;
                int modifier = 0;
                int value = integerExpression.Value;
                while (value > 0)
                {
                    newValue |= value % 10 << modifier;
                    modifier += 4;
                    value /= 10;
                }

                // modifier > 32 means the value can't be encoded in a 32-bit BCD value
                if (modifier > 32)
                {
                    newExpression = null;
                    return false;
                }

                newExpression = new IntegerConstantExpression(newValue);
                integerExpression.CopyLocation(newExpression);
                return true;
            }

            newExpression = expression;
            return false;
        }

        private static bool NormalizeBCD(ComparisonExpression comparison, out ExpressionBase result)
        {
            ExpressionBase newLeft;
            ExpressionBase newRight;
            bool leftHasBCD = ExtractBCD(comparison.Left, out newLeft);
            bool rightHasBCD = ExtractBCD(comparison.Right, out newRight);

            if (leftHasBCD || rightHasBCD)
            {
                if (!rightHasBCD)
                {
                    rightHasBCD = ConvertToBCD(comparison.Right, out newRight);
                    if (newRight == null)
                    {
                        // right value cannot be decoded into 32-bits
                        switch (comparison.Operation)
                        {
                            case ComparisonOperation.NotEqual:
                            case ComparisonOperation.LessThan:
                            case ComparisonOperation.LessThanOrEqual:
                                result = new BooleanConstantExpression(true);
                                return false;

                            default:
                                result = new BooleanConstantExpression(false);
                                return false;
                        }
                    }
                }
                else if (!leftHasBCD)
                {
                    leftHasBCD = ConvertToBCD(comparison.Right, out newLeft);
                    if (newLeft == null)
                    {
                        // left value cannot be decoded into 32-bits
                        switch (comparison.Operation)
                        {
                            case ComparisonOperation.NotEqual:
                            case ComparisonOperation.GreaterThan:
                            case ComparisonOperation.GreaterThanOrEqual:
                                result = new BooleanConstantExpression(true);
                                return false;

                            default:
                                result = new BooleanConstantExpression(false);
                                return false;
                        }
                    }
                }

                if (leftHasBCD && rightHasBCD)
                {
                    var newComparison = new ComparisonExpression(newLeft, comparison.Operation, newRight);
                    comparison.CopyLocation(newComparison);
                    result = newComparison;
                    return true;
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
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ErrorExpression" />.
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
            {
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
            }

            var comparison = new ComparisonExpression(left, Operation, right);
            do
            {
                if (comparison.Left.Type != comparison.Right.Type)
                {
                    // attempt to find a common type to perform the comparison
                    var converter = comparison.Left as IUpconvertibleExpression;
                    var newLeft = (converter != null) ? converter.UpconvertTo(comparison.Right.Type) : null;
                    if (newLeft != null)
                    {
                        comparison = new ComparisonExpression(newLeft, comparison.Operation, comparison.Right);
                    }
                    else
                    {
                        converter = comparison.Right as IUpconvertibleExpression;
                        var newRight = (converter != null) ? converter.UpconvertTo(comparison.Left.Type) : null;
                        if (newRight != null)
                            comparison = new ComparisonExpression(comparison.Left, comparison.Operation, newRight);
                    }
                }

                var comparisonNormalize = comparison.Left as IComparisonNormalizeExpression;
                if (comparisonNormalize == null)
                    break;

                var newComparison = comparisonNormalize.NormalizeComparison(comparison.Right, comparison.Operation);
                if (newComparison == null)
                {
                    // could not make any further normalizations, we're done
                    break;
                }

                comparison = newComparison as ComparisonExpression;
                if (comparison == null)
                {
                    result = newComparison;
                    CopyLocation(result);
                    return (result.Type != ExpressionType.Error);
                }
            } while (true);

            // remove bcd() from both sides (if possible)
            if (!NormalizeBCD(comparison, out result))
            {
                CopyLocation(result);
                return result.Type == ExpressionType.BooleanConstant;
            }
            comparison = (ComparisonExpression)result;

            // if the result is unchanged, prevent reprocessing the source and return it
            if (comparison == this)
            {
                _fullyExpanded = true;
                result = this;
                return true;
            }

            // if the expression can be fully evaluated, do so
            ErrorExpression error;
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

        public static ExpressionBase NormalizeFloatComparisonForInteger(ExpressionBase left, ComparisonOperation operation, ExpressionBase right)
        {
            var floatRight = right as FloatConstantExpression;
            if (floatRight != null)
            {
                right = new IntegerConstantExpression((int)floatRight.Value);

                // if comparing against a non-fractional right side, just convert it to an IntegerConstantExpression
                if (System.Math.Floor(floatRight.Value) == floatRight.Value)
                    return new ComparisonExpression(left, operation, right);

                // right has been floored, proceed.
            }

            // should be passed the floored value, or a float which has been floored by this point
            Debug.Assert(right.Type == ExpressionType.IntegerConstant);

            switch (operation)
            {
                case ComparisonOperation.Equal:
                    // integer a == 4.2 can never be true
                    return new ErrorExpression("Result can never be true using integer math");

                case ComparisonOperation.NotEqual:
                    // integer a != 4.2 is always true
                    return new ErrorExpression("Result is always true using integer math");

                case ComparisonOperation.LessThan:
                    // integer a < 4.2 becomes integer a <= 4
                    operation = ComparisonOperation.LessThanOrEqual;
                    break;

                case ComparisonOperation.LessThanOrEqual:
                    // integer a <= 4.2 becomes integer a <= 4
                    break;

                case ComparisonOperation.GreaterThan:
                    // integer a > 4.2 becomes integer a > 4
                    break;

                case ComparisonOperation.GreaterThanOrEqual:
                    // integer a >= 4.2 becomes integer a > 4
                    operation = ComparisonOperation.GreaterThan;
                    break;
            }

            return new ComparisonExpression(left, operation, right);
        }

        /// <summary>
        /// Determines whether the expression evaluates to true for the provided <paramref name="scope" />
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="error">[out] The error that prevented evaluation (or null if successful).</param>
        /// <returns>
        /// The result of evaluating the expression
        /// </returns>
        public override bool? IsTrue(InterpreterScope scope, out ErrorExpression error)
        {
            ExpressionBase left, right;
            if (!Left.ReplaceVariables(scope, out left))
            {
                error = left as ErrorExpression;
                return null;
            }

            if (!Right.ReplaceVariables(scope, out right))
            {
                error = right as ErrorExpression;
                return null;
            }

            error = null;

            if (left.Type == ExpressionType.FloatConstant || right.Type == ExpressionType.FloatConstant)
            {
                ExpressionBase result;
                if (!ConvertToFloat(ref left, ref right, out result))
                    return null;

                var leftFloat = (FloatConstantExpression)left;
                var rightFloat = (FloatConstantExpression)right;

                switch (Operation)
                {
                    case ComparisonOperation.Equal:
                        return leftFloat.Value == rightFloat.Value;
                    case ComparisonOperation.NotEqual:
                        return leftFloat.Value != rightFloat.Value;
                    case ComparisonOperation.GreaterThan:
                        return leftFloat.Value > rightFloat.Value;
                    case ComparisonOperation.GreaterThanOrEqual:
                        return leftFloat.Value >= rightFloat.Value;
                    case ComparisonOperation.LessThan:
                        return leftFloat.Value < rightFloat.Value;
                    case ComparisonOperation.LessThanOrEqual:
                        return leftFloat.Value <= rightFloat.Value;
                    default:
                        return null;
                }
            }

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
                        error = new ErrorExpression("Cannot perform relative comparison on boolean values", this);
                        return null;
                }
            }

            var stringLeft = left as StringConstantExpression;
            if (stringLeft != null)
            {
                var stringRight = right as StringConstantExpression;
                if (stringRight == null)
                    return null;

                switch (Operation)
                {
                    case ComparisonOperation.Equal:
                        return stringLeft.Value == stringRight.Value;
                    case ComparisonOperation.NotEqual:
                        return stringLeft.Value != stringRight.Value;
                    case ComparisonOperation.GreaterThan:
                        return string.Compare(stringLeft.Value, stringRight.Value) > 0;
                    case ComparisonOperation.GreaterThanOrEqual:
                        return string.Compare(stringLeft.Value, stringRight.Value) >= 0;
                    case ComparisonOperation.LessThan:
                        return string.Compare(stringLeft.Value, stringRight.Value) < 0;
                    case ComparisonOperation.LessThanOrEqual:
                        return string.Compare(stringLeft.Value, stringRight.Value) <= 0;
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
