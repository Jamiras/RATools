using RATools.Data;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class RequirementClauseExpression : ExpressionBase, 
        ITriggerExpression, ICloneableExpression, IComparisonNormalizeExpression
    {
        public RequirementClauseExpression()
            : base(ExpressionType.RequirementClause)
        {
        }

        public RequirementClauseExpression(RequirementClauseExpression source)
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

        public RequirementClauseExpression Clone()
        {
            return new RequirementClauseExpression(this);
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
            var that = obj as RequirementClauseExpression;
            return (that != null && Comparison == that.Comparison && HitTarget == that.HitTarget &&
                Behavior == that.Behavior && Right == that.Right && Left == that.Left);
        }

        public ErrorExpression BuildTrigger(TriggerBuilderContext context)
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

            switch (Comparison)
            {
                case ComparisonOperation.Equal: lastRequirement.Operator = RequirementOperator.Equal; break;
                case ComparisonOperation.NotEqual: lastRequirement.Operator = RequirementOperator.NotEqual; break;
                case ComparisonOperation.LessThan: lastRequirement.Operator = RequirementOperator.LessThan; break;
                case ComparisonOperation.LessThanOrEqual: lastRequirement.Operator = RequirementOperator.LessThanOrEqual; break;
                case ComparisonOperation.GreaterThan: lastRequirement.Operator = RequirementOperator.GreaterThan; break;
                case ComparisonOperation.GreaterThanOrEqual: lastRequirement.Operator = RequirementOperator.GreaterThanOrEqual; break;
            }

            lastRequirement.HitCount = HitTarget;
            return null;
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
                return new RequirementClauseExpression(this)
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

        ExpressionBase IComparisonNormalizeExpression.NormalizeComparison(ExpressionBase right, ComparisonOperation operation)
        {
            return new ErrorExpression("Cannot chain comparisons", this);
        }
    }
}
