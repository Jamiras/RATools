using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System;
using System.Diagnostics;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class ComparisonExpression : LeftRightExpressionBase
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

            var funcRefLeft = left as FunctionReferenceExpression;
            var funcRefRight = right as FunctionReferenceExpression;
            if (funcRefLeft != null || funcRefRight != null)
            {
                if (funcRefLeft != null && funcRefRight != null)
                {
                    result = new BooleanConstantExpression(funcRefLeft.Name == funcRefRight.Name);
                    CopyLocation(result);
                    return true;
                }

                if (funcRefLeft != null)
                    result = new ErrorExpression(string.Format("Cannot compare function reference and {0}", right.Type), this.Location);
                else
                    result = new ErrorExpression(string.Format("Cannot compare {0} and function reference", left.Type), this.Location);
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

            bool canModifyRight = true;
            // if the right side is only a constant, check to see if we're in a measured.
            // if we are, the right side is the measured target, and we don't want to modify that.
            if (right.Type == ExpressionType.IntegerConstant || right.Type == ExpressionType.FloatConstant)
            {
                var initializationContext = scope.GetContext<ParameterInitializationContext>();
                if (initializationContext != null)
                {
                    var format = initializationContext.GetParameter<StringConstantExpression>(scope, "format");
                    if (format != null && format.Value == "raw")
                    {
                        // capturing raw measured value. don't modify comparison
                        canModifyRight = false;
                    }
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

                var newComparison = comparisonNormalize.NormalizeComparison(comparison.Right, comparison.Operation, canModifyRight);
                if (newComparison == null)
                {
                    // could not make any further normalizations, we're done
                    break;
                }

                comparison = newComparison as ComparisonExpression;
                if (comparison == null)
                {
                    // result of normalization is error or constant, return it
                    result = newComparison;
                    CopyLocation(result);
                    return (result.Type != ExpressionType.Error);
                }
            } while (true);

            // if it's a memory comparison, wrap it is a RequirementClause
            switch (comparison.Left.Type)
            {
                case ExpressionType.MemoryAccessor:
                    var requirement = new RequirementConditionExpression
                    {
                        Left = comparison.Left,
                        Comparison = comparison.Operation,
                        Right = comparison.Right,
                        Location = Location
                    };
                    result = requirement.Normalize();
                    if (result is ErrorExpression)
                        return false;
                    result.Location = Location;
                    return true;
            }

            switch (comparison.Right.Type)
            {
                case ExpressionType.MemoryAccessor:
                    var requirement = new RequirementConditionExpression
                    {
                        Left = comparison.Right,
                        Comparison = ReverseComparisonOperation(comparison.Operation),
                        Right = comparison.Left,
                        Location = Location
                    };
                    result = requirement.Normalize();
                    if (result is ErrorExpression)
                        return false;
                    result.Location = Location;
                    return true;
            }

            // if the result is unchanged, prevent reprocessing the source and return it
            if (comparison == this)
            {
                _fullyExpanded = true;
                result = this;
                return true;
            }

            // prevent reprocessing the result and return it
            comparison._fullyExpanded = true;
            result = comparison;
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

            var normalizeComparison = left as IComparisonNormalizeExpression;
            if (normalizeComparison != null)
            {
                var result = normalizeComparison.NormalizeComparison(right, Operation, true);
                var boolResult = result as BooleanConstantExpression;
                if (boolResult != null)
                    return boolResult.Value;

                // memory reference (or something similar) that can't be determined at processing time
                if (!left.IsLiteralConstant)
                    return null;
            }

            // type doesn't implement IComparisonNormalizeExpression, or comparison didn't collapse to
            // a boolean expression. if both sides have the same type, do a strict equality comparison
            if (left.Type == right.Type)
            {
                switch (Operation)
                {
                    case ComparisonOperation.Equal:
                        return (left == right);

                    case ComparisonOperation.NotEqual:
                        return !(left == right);

                    default:
                        error = new ErrorExpression(String.Format("Cannot perform relative comparison on {0}", left.Type), this);
                        return null;
                }
            }

            // different types are always not equal to each other, even if they could be coerced.
            // allow a direct equality/inequality check, but error if a relative comparison is being attemped.
            switch (Operation)
            {
                case ComparisonOperation.Equal:
                    return false;

                case ComparisonOperation.NotEqual:
                    return true;

                default:
                    error = new ErrorExpression(String.Format("Cannot compare {0} and {1}", left.Type, right.Type), this);
                    return null;
            }
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
