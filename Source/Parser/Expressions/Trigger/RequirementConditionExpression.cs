using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class RequirementConditionExpression : RequirementExpressionBase, 
        ICloneableExpression
    {
        public RequirementConditionExpression()
            : base()
        {
        }

        public RequirementConditionExpression(RequirementConditionExpression source)
            : this()
        {
            Left = Clone(source.Left);
            Comparison = source.Comparison;
            Right = Clone(source.Right);
        }

        public ExpressionBase Left { get; set; }
        public ComparisonOperation Comparison { get; set; }
        public ExpressionBase Right { get; set; }

        ExpressionBase ICloneableExpression.Clone()
        {
            return Clone();
        }

        public new RequirementConditionExpression Clone()
        {
            return new RequirementConditionExpression(this);
        }
        
        private static ExpressionBase Clone(ExpressionBase expr)
        {
            var cloneable = expr as ICloneableExpression;
            if (cloneable != null)
                return cloneable.Clone();

            return expr;
        }

        internal override void AppendString(StringBuilder builder)
        {
            Left.AppendString(builder);

            builder.Append(' ');

            switch (Comparison)
            {
                case ComparisonOperation.Equal: builder.Append("=="); break;
                case ComparisonOperation.NotEqual: builder.Append("!="); break;
                case ComparisonOperation.LessThan: builder.Append('<'); break;
                case ComparisonOperation.LessThanOrEqual: builder.Append("<="); break;
                case ComparisonOperation.GreaterThan: builder.Append('>'); break;
                case ComparisonOperation.GreaterThanOrEqual: builder.Append(">="); break;
            }

            builder.Append(' ');

            // special handling: comparisons use unsigned values
            var rightInteger = Right as IntegerConstantExpression;
            if (rightInteger != null && rightInteger.Value < 0)
                builder.Append((uint)rightInteger.Value);
            else
                Right.AppendString(builder);
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as RequirementConditionExpression;
            return (that != null && Comparison == that.Comparison &&
                Right == that.Right && Left == that.Left);
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            ErrorExpression error;
            var right = MemoryValueExpression.ReduceToSimpleExpression(Right) ?? Right;

            var memoryValue = Left as MemoryValueExpression;
            if (memoryValue != null)
            {
                error = memoryValue.BuildTrigger(context, right);
            }
            else
            {
                var trigger = Left as ITriggerExpression;
                if (trigger == null)
                    return new ErrorExpression(string.Format("Cannot compare {0} in a trigger", Left.Type), Left);

                error = trigger.BuildTrigger(context);
            }

            if (error != null)
                return error;

            var lastRequirement = context.LastRequirement;

            var rightAccessor = right as MemoryAccessorExpression;
            if (rightAccessor != null)
            {
                if (rightAccessor.HasPointerChain)
                {
                    if (!rightAccessor.PointerChainMatches(Left))
                        return new ErrorExpression("Cannot compare values with different pointer chains", this);

                    rightAccessor = rightAccessor.Clone();
                    rightAccessor.ClearPointerChain();
                }

                lastRequirement.Right = FieldFactory.CreateField(rightAccessor);
            }
            else
            {
                lastRequirement.Right = FieldFactory.CreateField(right);
            }

            if (lastRequirement.Right.Type == FieldType.None)
                return new ErrorExpression(string.Format("Cannot compare {0} in a trigger", Right.Type), Right);

            lastRequirement.Operator = ConvertToRequirementOperator(Comparison);
            return null;
        }

        private static RequirementOperator ConvertToRequirementOperator(ComparisonOperation op)
        {
            switch (op)
            {
                case ComparisonOperation.Equal: return RequirementOperator.Equal;
                case ComparisonOperation.NotEqual: return RequirementOperator.NotEqual;
                case ComparisonOperation.LessThan: return RequirementOperator.LessThan;
                case ComparisonOperation.LessThanOrEqual: return RequirementOperator.LessThanOrEqual;
                case ComparisonOperation.GreaterThan: return RequirementOperator.GreaterThan;
                case ComparisonOperation.GreaterThanOrEqual: return RequirementOperator.GreaterThanOrEqual;
                default: return RequirementOperator.None;
            }
        }

        private static ComparisonOperation ConvertToComparisonOperation(RequirementOperator op)
        {
            switch (op)
            {
                case RequirementOperator.Equal: return ComparisonOperation.Equal;
                case RequirementOperator.NotEqual: return ComparisonOperation.NotEqual;
                case RequirementOperator.LessThan: return ComparisonOperation.LessThan;
                case RequirementOperator.LessThanOrEqual: return ComparisonOperation.LessThanOrEqual;
                case RequirementOperator.GreaterThan: return ComparisonOperation.GreaterThan;
                case RequirementOperator.GreaterThanOrEqual: return ComparisonOperation.GreaterThanOrEqual;
                default: return ComparisonOperation.None;
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

        private ExpressionBase NormalizeBCD()
        {
            ExpressionBase newLeft;
            ExpressionBase newRight;
            bool leftHasBCD = ExtractBCD(Left, out newLeft);
            bool rightHasBCD = ExtractBCD(Right, out newRight);

            if (!rightHasBCD)
            {
                if (!leftHasBCD)
                    return this;

                rightHasBCD = ConvertToBCD(Right, out newRight);
                if (newRight == null)
                {
                    // right value cannot be decoded into 32-bits
                    switch (Comparison)
                    {
                        case ComparisonOperation.NotEqual:
                        case ComparisonOperation.LessThan:
                        case ComparisonOperation.LessThanOrEqual:
                            return new BooleanConstantExpression(true);

                        default:
                            return new BooleanConstantExpression(false);
                    }
                }
            }
            else if (!leftHasBCD)
            {
                leftHasBCD = ConvertToBCD(Right, out newLeft);
                if (newLeft == null)
                {
                    // left value cannot be decoded into 32-bits
                    switch (Comparison)
                    {
                        case ComparisonOperation.NotEqual:
                        case ComparisonOperation.GreaterThan:
                        case ComparisonOperation.GreaterThanOrEqual:
                            return new BooleanConstantExpression(true);

                        default:
                            return new BooleanConstantExpression(false);
                    }
                }
            }

            if (leftHasBCD && rightHasBCD)
            {
                return new RequirementConditionExpression(this)
                {
                    Left = newLeft,
                    Right = newRight,
                };
            }

            return this;
        }

        private void NormalizeLimits(ref ExpressionBase expression)
        {
            var condition = expression as RequirementConditionExpression;
            if (condition == null)
                return;

            var rightValue = condition.Right as IntegerConstantExpression;
            if (rightValue == null)
                return;

            long min = 0, max = 0xFFFFFFFF;
            long value = (long)(uint)rightValue.Value;

            var memoryAccessor = condition.Left as MemoryAccessorExpression;
            if (memoryAccessor != null)
            {
                memoryAccessor.GetMinMax(out min, out max);
            }
            else
            {
                var modifiedMemoryAccessor = condition.Left as ModifiedMemoryAccessorExpression;
                if (modifiedMemoryAccessor != null)
                {
                    modifiedMemoryAccessor.GetMinMax(out min, out max);
                }
                else
                {
                    var memoryValue = condition.Left as MemoryValueExpression;
                    if (memoryValue != null)
                        memoryValue.GetMinMax(out min, out max);
                }
            }

            var newComparison = Comparison;

            if (value < min)
            {
                switch (Comparison)
                {
                    case ComparisonOperation.LessThan:
                    case ComparisonOperation.LessThanOrEqual:
                    case ComparisonOperation.Equal:
                        expression = new AlwaysFalseExpression();
                        return;

                    case ComparisonOperation.GreaterThan:
                    case ComparisonOperation.GreaterThanOrEqual:
                    case ComparisonOperation.NotEqual:
                        expression = new AlwaysTrueExpression();
                        return;
                }
            }
            else if (value == min)
            {
                switch (Comparison)
                {
                    case ComparisonOperation.LessThan:
                        expression = new AlwaysFalseExpression();
                        return;

                    case ComparisonOperation.GreaterThanOrEqual:
                        expression = new AlwaysTrueExpression();
                        return;

                    case ComparisonOperation.LessThanOrEqual:
                    case ComparisonOperation.Equal:
                        newComparison = ComparisonOperation.Equal;
                        break;

                    case ComparisonOperation.GreaterThan:
                        if (max == min + 1)
                        {
                            // bitX(A) > 0  =>  bitX(A) == 1
                            newComparison = ComparisonOperation.Equal;
                            rightValue = new IntegerConstantExpression((int)max);
                        }
                        else
                        {
                            newComparison = ComparisonOperation.GreaterThan;
                        }
                        break;

                    case ComparisonOperation.NotEqual:
                        if (max == min + 1)
                        {
                            // bitX(A) != 0  =>  bitX(A) == 1
                            newComparison = ComparisonOperation.Equal;
                            rightValue = new IntegerConstantExpression((int)max);
                        }
                        else
                        {
                            newComparison = ComparisonOperation.NotEqual;
                        }
                        break;
                }
            }
            else if (value > max)
            {
                switch (Comparison)
                {
                    case ComparisonOperation.GreaterThan:
                    case ComparisonOperation.GreaterThanOrEqual:
                    case ComparisonOperation.Equal:
                        expression = new AlwaysFalseExpression();
                        return;

                    case ComparisonOperation.LessThan:
                    case ComparisonOperation.LessThanOrEqual:
                    case ComparisonOperation.NotEqual:
                        expression = new AlwaysTrueExpression();
                        return;
                }
            }
            else if (value == max)
            {
                switch (Comparison)
                {
                    case ComparisonOperation.GreaterThan:
                        expression = new AlwaysFalseExpression();
                        return;

                    case ComparisonOperation.LessThanOrEqual:
                        expression = new AlwaysTrueExpression();
                        return;

                    case ComparisonOperation.GreaterThanOrEqual:
                    case ComparisonOperation.Equal:
                        newComparison = ComparisonOperation.Equal;
                        break;

                    case ComparisonOperation.LessThan:
                        if (max == min + 1)
                        {
                            // bitX(A) < 1  =>  bitX(A) == 0
                            newComparison = ComparisonOperation.Equal;
                            rightValue = new IntegerConstantExpression((int)min);
                        }
                        else
                        {
                            newComparison = ComparisonOperation.LessThan;
                        }
                        break;

                    case ComparisonOperation.NotEqual:
                        if (max == min + 1)
                        {
                            // bitX(A) != 1  =>  bitX(A) == 0
                            newComparison = ComparisonOperation.Equal;
                            rightValue = new IntegerConstantExpression((int)min);
                        }
                        else
                        {
                            newComparison = ComparisonOperation.NotEqual;
                        }
                        break;
                }
            }

            if (newComparison != Comparison || !ReferenceEquals(rightValue, condition.Right))
            {
                expression = new RequirementConditionExpression
                {
                    Left = Left,
                    Comparison = newComparison,
                    Right = rightValue,
                    Location = condition.Location
                };
            }
        }

        public ExpressionBase Normalize()
        {
            if (Left.IsLiteralConstant && !Right.IsLiteralConstant)
            {
                var reversed = new RequirementConditionExpression
                {
                    Left = Right,
                    Comparison = ComparisonExpression.ReverseComparisonOperation(Comparison),
                    Right = Left
                };
                return reversed.Normalize();
            }

            var modifiedMemoryAccessor = Left as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null && modifiedMemoryAccessor.ModifyingOperator != RequirementOperator.None)
            {
                // cannot have comparison and modifier in same line. wrap the modifier in
                // a MemoryValue so it can generate an AddSource chain with a 0
                Left = modifiedMemoryAccessor.UpconvertTo(ExpressionType.MemoryValue);
            }

            var result = NormalizeBCD();
            NormalizeLimits(ref result);

            if (!ReferenceEquals(result, this))
                CopyLocation(result);
            return result;
        }

        private static Field CreateField(ExpressionBase expr)
        {
            switch (expr.Type)
            {
                default:
                    return FieldFactory.CreateField(expr);

                case ExpressionType.ModifiedMemoryAccessor:
                    return FieldFactory.CreateField(((ModifiedMemoryAccessorExpression)expr).MemoryAccessor);

                case ExpressionType.MemoryValue:
                    return FieldFactory.CreateField(((MemoryValueExpression)expr).MemoryAccessors.Last().MemoryAccessor);
            }
        }

        public override RequirementExpressionBase LogicalIntersect(RequirementExpressionBase that, ConditionalOperation condition)
        {
            var thatCondition = that as RequirementConditionExpression;
            if (thatCondition == null)
            {
                var thatClause = that as RequirementClauseExpression;
                return (thatClause != null) ? thatClause.LogicalIntersect(this, condition) : null;
            }

            if (Left != thatCondition.Left)
                return null;

            var leftField = CreateField(Right);
            var rightField = CreateField(thatCondition.Right);
            if (leftField.Type != rightField.Type || leftField.Type == FieldType.None)
                return null;
            if (leftField.IsMemoryReference && (leftField.Value != rightField.Value || leftField.Size != rightField.Size))
                return null; // reading different addresses or different sizes

            var leftCondition = ConvertToRequirementOperator(Comparison);
            var rightCondition = ConvertToRequirementOperator(thatCondition.Comparison);
            var merged = RequirementMerger.MergeComparisons(
                leftCondition, leftField.Value, rightCondition, rightField.Value, condition);

            if (merged.Key != RequirementOperator.None)
            {
                if (merged.Key == RequirementOperator.Multiply)
                    return new AlwaysTrueExpression();

                if (merged.Key == RequirementOperator.Divide)
                    return new AlwaysFalseExpression();

                var result = new RequirementConditionExpression
                {
                    Left = Left,
                    Comparison = ConvertToComparisonOperation(merged.Key),
                };

                switch (leftField.Type)
                {
                    case FieldType.Value:
                        result.Right = new IntegerConstantExpression((int)merged.Value);
                        break;

                    default:
                        // already ensured the addresses are the same
                        result.Right = new MemoryAccessorExpression(leftField);
                        break;
                }

                if (result.Comparison == Comparison && result.Right == Right)
                    return this;
                if (result.Comparison == thatCondition.Comparison && result.Right == thatCondition.Right)
                    return thatCondition;

                return result;
            }

            return base.LogicalIntersect(that, condition);
        }

        public override RequirementExpressionBase InvertLogic()
        {
            var condition = Clone();
            condition.Comparison = ComparisonExpression.GetOppositeComparisonOperation(condition.Comparison);
            return condition;
        }
    }
}
