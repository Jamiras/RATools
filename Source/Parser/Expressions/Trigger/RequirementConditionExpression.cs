﻿using RATools.Data;
using RATools.Parser.Internal;
using System;
using System.Diagnostics;
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
            Behavior = source.Behavior;
            Left = Clone(source.Left);
            Comparison = source.Comparison;
            Right = Clone(source.Right);
            HitTarget = source.HitTarget;
        }

        public RequirementType Behavior { get; set; }
        public ExpressionBase Left { get; set; }
        public ComparisonOperation Comparison { get; set; }
        public ExpressionBase Right { get; set; }

        public uint HitTarget { get; set; }

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
            if (HitTarget > 0)
            {
                if (HitTarget == 1)
                    builder.Append("once(");
                else
                    builder.AppendFormat("repeated({0}, ", HitTarget);
            }

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

            Right.AppendString(builder);

            if (HitTarget > 0)
                builder.Append(')');
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as RequirementConditionExpression;
            return (that != null && Comparison == that.Comparison && HitTarget == that.HitTarget &&
                Behavior == that.Behavior && Right == that.Right && Left == that.Left);
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
            lastRequirement.HitCount = HitTarget;
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

        public ExpressionBase Normalize()
        {
            var modifiedMemoryAccessor = Left as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null && modifiedMemoryAccessor.ModifyingOperator != RequirementOperator.None)
            {
                // cannot have comparison and modifier in same line. wrap the modifier in
                // a MemoryValue so it can generate an AddSource chain with a 0
                Left = modifiedMemoryAccessor.UpconvertTo(ExpressionType.MemoryValue);
            }

            var result = NormalizeBCD();

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

            if (Behavior != thatCondition.Behavior || Left != thatCondition.Left)
                return null;

            // cannot merge if either condition has an infinite hit target
            if (HitTarget != thatCondition.HitTarget && (HitTarget == 0 || thatCondition.HitTarget == 0))
                return null;

            var leftField = CreateField(Right);
            var rightField = CreateField(thatCondition.Right);
            if (leftField.Type != rightField.Type || leftField.Type == FieldType.None)
                return null;
            if (leftField.IsMemoryReference && (leftField.Value != rightField.Value || leftField.Size != rightField.Size))
                return null; // reading different addresses or different sizes

            //var leftEx = new RequirementEx();
            //leftEx.Requirements.Add(new Requirement
            //{
            //    Left = new Field { Type = FieldType.MemoryAddress, Size = FieldSize.DWord, Value = 0x1234 },
            //    Operator = ConvertToRequirementOperator(Comparison),
            //    Right = leftField,
            //    HitCount = HitTarget
            //});

            //var rightEx = new RequirementEx();
            //rightEx.Requirements.Add(new Requirement
            //{
            //    Left = new Field { Type = FieldType.MemoryAddress, Size = FieldSize.DWord, Value = 0x1234 },
            //    Operator = ConvertToRequirementOperator(thatClause.Comparison),
            //    Right = rightField,
            //    HitCount = thatClause.HitTarget
            //});

            //var mergedEx = RequirementMerger.MergeRequirements(leftEx, rightEx, condition);

            var leftCondition = ConvertToRequirementOperator(Comparison);
            var rightCondition = ConvertToRequirementOperator(thatCondition.Comparison);
            var merged = RequirementMerger.MergeComparisons(
                leftCondition, leftField.Value, rightCondition, rightField.Value, condition);

            if (merged.Key != RequirementOperator.None)
            {
                if (merged.Key == RequirementOperator.Multiply)
                    return new AlwaysTrueExpression();

                if (merged.Key == RequirementOperator.Divide)
                {
                    // these are allowed to conflict with each other
                    if (HitTarget > 0)
                        return null;
                    if (Behavior == RequirementType.PauseIf)
                        return null;
                    if (Behavior == RequirementType.ResetIf)
                        return null;

                    return new AlwaysFalseExpression();
                }

                var result = new RequirementConditionExpression
                {
                    Behavior = Behavior,
                    HitTarget = Math.Max(HitTarget, thatCondition.HitTarget),
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

                if (result.Comparison == Comparison && result.Right == Right && result.HitTarget == HitTarget)
                    return this;
                if (result.Comparison == thatCondition.Comparison && result.Right == thatCondition.Right && result.HitTarget == thatCondition.HitTarget)
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
