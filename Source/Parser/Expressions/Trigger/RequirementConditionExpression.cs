﻿using RATools.Data;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
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
            Left = source.Left;
            Comparison = source.Comparison;
            Right = source.Right;
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
            if (rightInteger != null && rightInteger.IsNegative)
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
            var comparison = Comparison;
            var left = Left;
            var right = MemoryAccessorExpressionBase.ReduceToSimpleExpression(Right);
            bool sharedPointerChain = false;

            var rightAccessor = MemoryAccessorExpression.Extract(right);
            if (rightAccessor != null && rightAccessor.HasPointerChain)
            {
                if (rightAccessor.PointerChainMatches(left as MemoryAccessorExpressionBase))
                {
                    rightAccessor = rightAccessor.Clone();
                    rightAccessor.ClearPointerChain();
                    sharedPointerChain = true;
                }
                else
                {
                    var leftMemoryValue = left as MemoryValueExpression;
                    if (leftMemoryValue != null && leftMemoryValue.MemoryAccessors.All(m => m.ModifyingOperator != RequirementOperator.None))
                    {
                        // all elements on left side are modified, so we'd need a 0 placeholder for the comparison
                        // attempt to avoid that by making the right value the AddSource element.
                        //
                        //     A / 2 > B   ->   - A / 2 + B < 0
                        //
                        // move the left side to the right side and invert
                        var newLeft = new MemoryValueExpression();
                        newLeft = newLeft.Combine(right, MathematicOperation.Add) as MemoryValueExpression;
                        left = newLeft.Combine(left, MathematicOperation.Subtract);
                        comparison = ComparisonExpression.ReverseComparisonOperation(comparison);
                    }
                    else
                    {
                        // move the right side to the left side and compare to zero.
                        //
                        //     A > B / 2   ->   - B / 2 + A > 0
                        //
                        var newLeft = new MemoryValueExpression();
                        newLeft = newLeft.Combine(left, MathematicOperation.Add) as MemoryValueExpression;
                        left = newLeft.Combine(right, MathematicOperation.Subtract);
                    }

                    if (comparison == ComparisonOperation.LessThan || comparison == ComparisonOperation.LessThanOrEqual)
                    {
                        // can't comare "<0" or "<=0". invert comparison
                        var newLeft = new MemoryValueExpression();
                        left = ((MemoryValueExpression)left).InvertAndMigrateAccessorsTo(newLeft);
                        comparison = ComparisonExpression.ReverseComparisonOperation(comparison);
                    }

                    right = new IntegerConstantExpression(0);
                    rightAccessor = null;
                }
            }

            var memoryValue = left as MemoryValueExpression;
            if (memoryValue != null)
            {
                if (right is ModifiedMemoryAccessorExpression)
                {
                    memoryValue = (MemoryValueExpression)memoryValue.Combine(right, MathematicOperation.Subtract);
                    rightAccessor = null;
                    right = new IntegerConstantExpression(0);
                }

                error = memoryValue.BuildTrigger(context, right);
            }
            else
            {
                var trigger = left as ITriggerExpression;
                if (trigger == null)
                    return new ErrorExpression(string.Format("Cannot compare {0} in a trigger", left.Type.ToLowerString()), left);

                error = trigger.BuildTrigger(context);
            }

            if (error != null)
                return error;

            var lastRequirement = context.LastRequirement;

            if (rightAccessor != null)
            {
                // if the right side is a non-pointer chain memory reference and the left side
                // has a pointer chain, we have to inject an extra condition so the pointer
                // chain isn't applied to the right side.
                if (!sharedPointerChain && rightAccessor.Field.IsMemoryReference && !rightAccessor.HasPointerChain)
                {
                    var leftAccessor = left as MemoryAccessorExpression;
                    if (leftAccessor != null && leftAccessor.HasPointerChain)
                    {
                        lastRequirement.Type = RequirementType.AddSource;

                        lastRequirement = new Requirement();
                        lastRequirement.Left = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 0 };
                        lastRequirement.Operator = RequirementOperator.Equal;
                        context.Trigger.Add(lastRequirement);
                    }
                }

                lastRequirement.Right = FieldFactory.CreateField(rightAccessor);
            }
            else
            {
                lastRequirement.Right = FieldFactory.CreateField(right);
            }

            if (lastRequirement.Right.Type == FieldType.None)
                return new ErrorExpression(string.Format("Cannot compare {0} in a trigger", Right.Type.ToLowerString()), Right);

            lastRequirement.Operator = ConvertToRequirementOperator(comparison);
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

        internal static ComparisonOperation ConvertToComparisonOperation(RequirementOperator op)
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
            var simpleExpression = MemoryAccessorExpressionBase.ReduceToSimpleExpression(expression);

            var bcdWrapper = simpleExpression as BinaryCodedDecimalExpression;
            if (bcdWrapper != null)
            {
                // this removes the wrapper from the BCD expression by copying it into
                // an unwrapped MemoryAccessorExpression
                newExpression = new MemoryAccessorExpression(bcdWrapper);
                return true;
            }

            var memoryValue = simpleExpression as MemoryValueExpression;
            if (memoryValue != null && memoryValue.MemoryAccessors.Count() == 1)
            {
                bcdWrapper = memoryValue.MemoryAccessors.First().MemoryAccessor as BinaryCodedDecimalExpression;
                if (bcdWrapper != null)
                {
                    // this removes the wrapper from the BCD expression by copying it into
                    // an unwrapped MemoryAccessorExpression
                    newExpression = new MemoryAccessorExpression(bcdWrapper);

                    var newMemoryValue = new MemoryValueExpression();
                    newMemoryValue.ApplyMathematic(newExpression, MathematicOperation.Add);

                    if (memoryValue.HasConstant)
                    {
                        if (!ConvertToBCD(memoryValue.ExtractConstant(), out newExpression))
                        {
                            newExpression = expression;
                            return false;
                        }

                        newMemoryValue.ApplyMathematic(newExpression, MathematicOperation.Add);
                    }

                    newExpression = newMemoryValue;
                    return true;
                }
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

        private ErrorExpression NormalizeBCD(out RequirementExpressionBase result)
        {
            ExpressionBase newLeft;
            ExpressionBase newRight;
            bool leftHasBCD = ExtractBCD(Left, out newLeft);
            bool rightHasBCD = ExtractBCD(Right, out newRight);

            if (!rightHasBCD)
            {
                if (!leftHasBCD)
                {
                    result = this;
                    return null;
                }

                rightHasBCD = ConvertToBCD(Right, out newRight);
                if (newRight == null)
                {
                    // right value cannot be decoded into 32-bits
                    switch (Comparison)
                    {
                        case ComparisonOperation.NotEqual:
                        case ComparisonOperation.LessThan:
                        case ComparisonOperation.LessThanOrEqual:
                            result = new AlwaysTrueExpression();
                            return null;

                        default:
                            result = new AlwaysFalseExpression();
                            return null;
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
                            result = new AlwaysTrueExpression();
                            return null;

                        default:
                            result = new AlwaysFalseExpression();
                            return null;
                    }
                }
            }

            if (leftHasBCD && rightHasBCD)
            {
                if (Comparison == ComparisonOperation.Equal || Comparison == ComparisonOperation.NotEqual)
                {
                    var leftMemoryValue = newLeft as MemoryValueExpression;
                    if (leftMemoryValue != null && leftMemoryValue.HasConstant)
                    {
                        result = null;
                        return new ErrorExpression("Cannot eliminate bcd from equality comparison with modifier", leftMemoryValue);
                    }

                    var rightMemoryValue = newLeft as MemoryValueExpression;
                    if (rightMemoryValue != null && rightMemoryValue.HasConstant)
                    {
                        result = null;
                        return new ErrorExpression("Cannot eliminate bcd from equality comparison with modifier", rightMemoryValue);
                    }
                }

                result = new RequirementConditionExpression()
                {
                    Left = newLeft,
                    Comparison = Comparison,
                    Right = newRight,
                    Location = Location,
                };

                return null;
            }

            result = this;
            return null;
        }

        private static bool ExtractInversion(ExpressionBase expression, out ExpressionBase newExpression)
        {
            var simpleExpression = MemoryAccessorExpressionBase.ReduceToSimpleExpression(expression);

            var invertWrapper = simpleExpression as BitwiseInvertExpression;
            if (invertWrapper != null)
            {
                // this removes the wrapper from the inverted expression by copying it into
                // an unwrapped MemoryAccessorExpression
                newExpression = new MemoryAccessorExpression(invertWrapper);
                return true;
            }

            newExpression = expression;
            return false;
        }

        private static void NormalizeInvert(ref RequirementExpressionBase expression)
        {
            var condition = expression as RequirementConditionExpression;
            if (condition == null)
                return;

            ExpressionBase newLeft;
            ExpressionBase newRight;
            bool leftHasInvert = ExtractInversion(condition.Left, out newLeft);
            if (!leftHasInvert)
                return;

            bool rightHasInvert = ExtractInversion(condition.Right, out newRight);
            if (!rightHasInvert)
            {
                var rightInteger = newRight as IntegerConstantExpression;
                if (rightInteger != null)
                {
                    var leftMemoryAccessor = newLeft as MemoryAccessorExpression;
                    if (leftMemoryAccessor != null)
                    {
                        long min, max;
                        leftMemoryAccessor.GetMinMax(out min, out max);
                        if ((max & 0x01) != 0)
                        {
                            newRight = new IntegerConstantExpression(~rightInteger.Value & (int)max);
                            rightHasInvert = true;
                        }
                    }
                }
            }

            if (rightHasInvert)
            {
                expression = new RequirementConditionExpression()
                {
                    Left = newLeft,
                    Comparison = ComparisonExpression.ReverseComparisonOperation(condition.Comparison),
                    Right = newRight,
                    Location = condition.Location,
                };
            }
        }

        private static void NormalizeLimits(ref RequirementExpressionBase expression)
        {
            var condition = expression as RequirementConditionExpression;
            if (condition == null)
                return;

            var rightValue = condition.Right as IntegerConstantExpression;
            if (rightValue == null)
                return;

            if (MemoryValueExpression.HasFloat(condition.Left))
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

            var newComparison = condition.Comparison;

            if (value < min)
            {
                switch (condition.Comparison)
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
                switch (condition.Comparison)
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
                if (value > Int32.MaxValue && min < 0)
                {
                    // value is implicitly negative, ignore it.
                }
                else
                {
                    switch (condition.Comparison)
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
            }
            else if (value == max)
            {
                switch (condition.Comparison)
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

            if (newComparison != condition.Comparison || !ReferenceEquals(rightValue, condition.Right))
            {
                expression = new RequirementConditionExpression
                {
                    Left = condition.Left,
                    Comparison = newComparison,
                    Right = rightValue,
                    Location = condition.Location
                };
            }
        }

        public ExpressionBase Normalize()
        {
            if (Left is LiteralConstantExpressionBase && Right is not LiteralConstantExpressionBase)
            {
                var reversed = new RequirementConditionExpression
                {
                    Left = Right,
                    Comparison = ComparisonExpression.ReverseComparisonOperation(Comparison),
                    Right = Left
                };
                return reversed.Normalize();
            }

            var integerRight = Right as IntegerConstantExpression;
            if (integerRight != null && integerRight.IsNegative && integerRight.Value > -100000)
            {
                var memoryValue = Left as MemoryValueExpression;
                if (memoryValue != null && memoryValue.MemoryAccessors.Any(a => a.CombiningOperator == RequirementType.SubSource))
                {
                    // A - B < -2  =>  B - A > 2
                    var newMemoryValue = new MemoryValueExpression();
                    newMemoryValue = memoryValue.InvertAndMigrateAccessorsTo(newMemoryValue);
                    var integerConstant = new IntegerConstantExpression(-integerRight.Value);
                    var normalized = new RequirementConditionExpression
                    {
                        Left = newMemoryValue,
                        Comparison = ComparisonExpression.ReverseComparisonOperation(Comparison),
                        Right = integerConstant
                    };
                    return normalized.Normalize();
                }
            }

            var modifiedMemoryAccessor = Left as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null && modifiedMemoryAccessor.ModifyingOperator != RequirementOperator.None)
            {
                // cannot have comparison and modifier in same line. wrap the modifier in
                // a MemoryValue so it can generate an AddSource chain with a 0
                Left = new MemoryValueExpression(modifiedMemoryAccessor);
            }

            RequirementExpressionBase result;
            var error = NormalizeBCD(out result);
            if (error != null)
                return error;

            NormalizeInvert(ref result);
            NormalizeLimits(ref result);

            if (!ReferenceEquals(result, this))
                CopyLocation(result);
            return result;
        }

        private static Field CreateField(ExpressionBase expr)
        {
            var memoryValue = expr as MemoryValueExpression;
            if (memoryValue != null)
                return FieldFactory.CreateField(memoryValue.MemoryAccessors.Last().MemoryAccessor);

            var modifiedMemoryAccessor = expr as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null)
                return FieldFactory.CreateField(modifiedMemoryAccessor.MemoryAccessor);

            return FieldFactory.CreateField(expr);
        }

        public override RequirementExpressionBase LogicalIntersect(RequirementExpressionBase that, ConditionalOperation condition)
        {
            var thatCondition = that as RequirementConditionExpression;
            if (thatCondition == null)
            {
                var thatClause = that as RequirementClauseExpression;
                if (thatClause != null)
                    return thatClause.LogicalIntersect(this, condition);

                var thatBehavior = that as BehavioralRequirementExpression;
                if (thatBehavior != null)
                    return thatBehavior.LogicalIntersect(this, condition);

                return null;
            }

            if (Left.GetType() == thatCondition.Left.GetType())
            {
                if (Left != thatCondition.Left)
                    return null;
            }
            else
            {
                var leftConverted = MemoryAccessorExpressionBase.WrapInMemoryValue(Left);
                var thatLeftConverted = MemoryAccessorExpressionBase.WrapInMemoryValue(thatCondition.Left);
                if (leftConverted != thatLeftConverted)
                    return null;
            }

            var leftField = CreateField(Right);
            var rightField = CreateField(thatCondition.Right);
            if (leftField.Type != rightField.Type || leftField.Type == FieldType.None)
                return null;
            if (leftField.IsMemoryReference && (leftField.Value != rightField.Value || leftField.Size != rightField.Size))
                return null; // reading different addresses or different sizes

            var leftCondition = ConvertToRequirementOperator(Comparison);
            var rightCondition = ConvertToRequirementOperator(thatCondition.Comparison);

            KeyValuePair<RequirementOperator, IComparable> merged;

            if (leftField.Type != FieldType.Float && rightField.Type != FieldType.Float)
            {
                merged = RequirementMerger.MergeComparisons(
                    leftCondition, leftField.Value, rightCondition, rightField.Value, condition);
            }
            else
            {
                var leftFloat = (leftField.Type == FieldType.Float) ? leftField.Float : (float)leftField.Value;
                var rightFloat = (rightField.Type == FieldType.Float) ? rightField.Float : (float)rightField.Value;
                merged = RequirementMerger.MergeComparisons(
                    leftCondition, leftFloat, rightCondition, rightFloat, condition);
            }

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
                        result.Right = new IntegerConstantExpression((int)(uint)merged.Value);
                        break;

                    case FieldType.Float:
                        result.Right = new FloatConstantExpression((float)merged.Value);
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

            RequirementExpressionBase result = condition;
            NormalizeLimits(ref result);
            result.Location = Location;
            return result;
        }
    }
}
