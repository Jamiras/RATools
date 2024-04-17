using RATools.Data;
using RATools.Parser.Internal;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    /// <summary>
    /// Modifies a <see cref="MemoryAccessor"/> by scaling or masking it.
    /// </summary>
    internal class ModifiedMemoryAccessorExpression : MemoryAccessorExpressionBase,
        ITriggerExpression, IExecutableExpression,
        IMathematicCombineExpression, IMathematicCombineInverseExpression,
        IComparisonNormalizeExpression, ICloneableExpression
    {
        public ModifiedMemoryAccessorExpression()
            : base()
        {
        }

        public ModifiedMemoryAccessorExpression(MemoryAccessorExpression source)
            : this()
        {
            MemoryAccessor = source;
            Location = source.Location;
        }

        public ModifiedMemoryAccessorExpression(ModifiedMemoryAccessorExpression source)
            : this()
        {
            MemoryAccessor = source.MemoryAccessor;
            CombiningOperator = source.CombiningOperator;
            ModifyingOperator = source.ModifyingOperator;
            Modifier = source.Modifier;
            Location = source.Location;
        }

        /// <summary>
        /// Gets or sets the <see cref="MemoryAccessor"/> that is to be modified.
        /// </summary>
        public MemoryAccessorExpression MemoryAccessor
        {
            get { return _memoryAccessor; }
            set
            {
                Debug.Assert(!IsReadOnly);
                _memoryAccessor = value;
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MemoryAccessorExpression _memoryAccessor;

        /// <summary>
        /// Gets or sets how the <see cref="MemoryAccessor"/> will be modified.
        /// </summary>
        public RequirementOperator ModifyingOperator
        {
            get { return _modifyingOperator; }
            set
            {
                Debug.Assert(!IsReadOnly);
                _modifyingOperator = value;
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private RequirementOperator _modifyingOperator;

        /// <summary>
        /// Gets or sets the value to be applied to the <see cref="MemoryAccessor"/>.
        /// </summary>
        public Field Modifier
        {
            get { return _modifier; }
            set
            {
                Debug.Assert(!IsReadOnly);
                _modifier = value;
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Field _modifier;

        /// <summary>
        /// Gets or sets the operator used to combine this <see cref="ModifiedMemoryAccessorExpression"/>
        /// with other <see cref="ModifiedMemoryAccessorExpression"/>s in a <see cref="MemoryValueExpression"/>.
        /// </summary>
        public RequirementType CombiningOperator
        {
            get { return _combiningOperator; }
            set
            {
                Debug.Assert(!IsReadOnly);
                _combiningOperator = value;
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private RequirementType _combiningOperator;

        /// <summary>
        /// Creates a clone of the <see cref="ModifiedMemoryAccessorExpression"/> with the opposing <see cref="CombiningOperator"/>.
        /// </summary>
        public ModifiedMemoryAccessorExpression InvertCombiningOperator()
        {
            var clone = Clone();

            if (CombiningOperator == RequirementType.SubSource)
                clone.CombiningOperator = RequirementType.AddSource;
            else
                clone.CombiningOperator = RequirementType.SubSource;

            return clone;
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as ModifiedMemoryAccessorExpression;
            return (that != null &&
                    ModifyingOperator == that.ModifyingOperator &&
                    MemoryAccessor == that.MemoryAccessor &&
                    (ModifyingOperator == RequirementOperator.None || Modifier == that.Modifier));
        }

        internal override void AppendString(StringBuilder builder)
        {
            switch (CombiningOperator)
            {
                case RequirementType.AddSource:
                    builder.Append("+ ");
                    break;

                case RequirementType.SubSource:
                    builder.Append("- ");
                    break;
            }

            MemoryAccessor.AppendString(builder);

            switch (ModifyingOperator)
            {
                case RequirementOperator.Multiply:
                    builder.Append(" * ");
                    break;

                case RequirementOperator.Divide:
                    builder.Append(" / ");
                    break;

                case RequirementOperator.BitwiseAnd:
                    builder.Append(" & ");
                    break;

                case RequirementOperator.BitwiseXor:
                    builder.Append(" ^ ");
                    break;

                default:
                    return;
            }

            if (Modifier.IsMemoryReference && MemoryAccessor.PointerChain.Any())
            {
                var clone = MemoryAccessor.Clone();
                clone.Field = Modifier;
                clone.AppendString(builder);
            }
            else
            {
                switch (ModifyingOperator)
                {
                    case RequirementOperator.BitwiseAnd:
                    case RequirementOperator.BitwiseXor:
                        Modifier.AppendString(builder, NumberFormat.Hexadecimal);
                        break;

                    case RequirementOperator.Multiply:
                    case RequirementOperator.Divide:
                        if (Modifier.Type != FieldType.Value)
                            goto default; // default formating for floats

                        // print signed values for integer multiplication and division
                        builder.Append((int)Modifier.Value);
                        break;

                    default:
                        Modifier.AppendString(builder, NumberFormat.Decimal);
                        break;
                }
            }
        }

        ExpressionBase ICloneableExpression.Clone()
        {
            return Clone();
        }

        /// <summary>
        /// Creates a clone of the expression.
        /// </summary>
        public ModifiedMemoryAccessorExpression Clone()
        {
            return new ModifiedMemoryAccessorExpression(this);
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
            return ApplyMathematic(right, operation);
        }

        public ExpressionBase CombineInverse(ExpressionBase left, MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Add:
                case MathematicOperation.Subtract:
                    var clause = new MemoryValueExpression();
                    clause = clause.ApplyMathematic(left, MathematicOperation.Add) as MemoryValueExpression;
                    if (clause != null)
                        return clause.ApplyMathematic(this, operation);
                    break;

                case MathematicOperation.Multiply:
                case MathematicOperation.BitwiseAnd:
                case MathematicOperation.BitwiseXor:
                    return Combine(left, operation);

                case MathematicOperation.Divide:
                    return new ErrorExpression("Cannot divide by a complex runtime value");

                case MathematicOperation.Modulus:
                    return new ErrorExpression("Cannot modulus using a runtime value");
            }

            return null;
        }

        private ExpressionBase ApplyMask(uint mask)
        {
            // anything & 0 is 0
            if (mask == 0)
                return new IntegerConstantExpression(0);

            // if any bits are non-zero after the first non-zero bit, we can't simplify
            var shifted = mask >> 1;
            if ((mask & shifted) != shifted)
                return null;

            long min, max;
            MemoryAccessor.GetMinMax(out min, out max);

            // if the mask contains more bits than the value can possibly have, the mask can be ignored
            if (mask >= max)
            {
                ModifyingOperator = RequirementOperator.None;
                return this;
            }

            if (mask > 0x00FFFFFF)
            {
                // dword required
                return null;
            }

            FieldSize size;
            uint sizeMask;

            if (mask > 0x0000FFFF)
            {
                size = FieldSize.TByte;
                sizeMask = 0x00FFFFFF;
            }
            else if (mask > 0x000000FF)
            {
                size = FieldSize.Word;
                sizeMask = 0x0000FFFF;
            }
            else if (mask > 0x00000001)
            {
                // if masking off the lower nibble, convert to low4,
                // otherwise just mask off the byte itself to avoid double masking.
                if (mask == 0x0000000F)
                {
                    size = FieldSize.LowNibble;
                    sizeMask = 0x0000000F;
                }
                else
                {
                    size = FieldSize.Byte;
                    sizeMask = 0x000000FF;
                }
            }
            else
            {
                size = FieldSize.Bit0;
                sizeMask = 0x00000001;
            }

            if (max < sizeMask)
                return null;

            // shrink to size
            if (MemoryAccessor.Field.Size != size)
            {
                var newField = MemoryAccessor.Field.Clone();
                newField.Size = size;

                if (MemoryAccessor.Field.IsBigEndian)
                {
                    switch (size)
                    {
                        case FieldSize.TByte:
                            newField.Size = FieldSize.BigEndianTByte;
                            break;

                        case FieldSize.Word:
                            newField.Size = FieldSize.BigEndianWord;
                            break;
                    }

                    var bytesBefore = Field.GetByteSize(MemoryAccessor.Field.Size);
                    var bytesAfter = Field.GetByteSize(newField.Size);
                    if (bytesAfter < bytesBefore)
                        newField.Value += (bytesBefore - bytesAfter);
                }

                MemoryAccessor = new MemoryAccessorExpression(MemoryAccessor) { Field = newField };
            }

            if (mask < sizeMask)
            {
                ModifyingOperator = RequirementOperator.BitwiseAnd;
                Modifier = FieldFactory.CreateField(new IntegerConstantExpression((int)mask));
            }
            else
            {
                ModifyingOperator = RequirementOperator.None;
            }

            return this;
        }

        private static bool IsBitwiseOperator(RequirementOperator op)
        {
            switch (op)
            {
                case RequirementOperator.BitwiseAnd:
                case RequirementOperator.BitwiseXor:
                    return true;

                default:
                    return false;
            }
        }

        public ExpressionBase ApplyMathematic(ExpressionBase right, MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Add:
                case MathematicOperation.Subtract:
                    var memoryValue = new MemoryValueExpression();
                    memoryValue.ApplyMathematic(this, MathematicOperation.Add);
                    return memoryValue.ApplyMathematic(right, operation);
            }

            if (IsReadOnly)
                return Clone().ApplyMathematic(right, operation);

            Field field;

            var rightAccessor = right as MemoryAccessorExpression;
            if (rightAccessor != null)
            {
                // FieldFactory won't process a MemoryAccessor with a Pointer chain. We want
                // to allow it, but only if it matches the pointer chain of this MemoryAccessor.
                if (!MemoryAccessor.PointerChainMatches(rightAccessor))
                {
                    if (!MemoryAccessor.HasPointerChain)
                    {
                        if (operation == MathematicOperation.Multiply)
                        {
                            // right has a pointer chain. left does not. multiplication is communitive. invert
                            return rightAccessor.Combine(this, MathematicOperation.Multiply);
                        }

                        return new ErrorExpression(
                            string.Format("Cannot {0} pointer value and modified value",
                                            MathematicExpression.GetOperatorVerb(operation)), right);
                    }

                    return new ErrorExpression(
                        string.Format("Cannot {0} two values with differing pointers",
                                      MathematicExpression.GetOperatorVerb(operation)), right);
                }

                // pointer chain matched. extract the field
                field = rightAccessor.Field;
            }
            else
            {
                if (MemoryAccessor.HasPointerChain)
                {
                    var rightModifiedAccessor = right as ModifiedMemoryAccessorExpression;
                    if (rightModifiedAccessor != null && rightModifiedAccessor.ModifyingOperator != RequirementOperator.None)
                    {
                        return new ErrorExpression(
                            string.Format("Cannot {0} pointer value and modified value",
                                          MathematicExpression.GetOperatorVerb(operation)), right);
                    }
                }

                field = FieldFactory.CreateField(right);
                if (field.Type == FieldType.None)
                    return new ErrorExpression("Could not create condition from " + right.Type);
            }

            var newModifyingOperator = GetModifyingOperator(operation);
            if (newModifyingOperator == RequirementOperator.None)
            {
                // operator could not be turned into a conditional operator
                if (operation == MathematicOperation.Modulus)
                {
                    if ((field.Type == FieldType.Value && field.Value == 1) ||
                        (field.Type == FieldType.Float && field.Float == 1.0))
                    {
                        // a % 1  =>  0
                        return new IntegerConstantExpression(0);
                    }

                    if ((field.Type == FieldType.Value && field.Value == 0) ||
                        (field.Type == FieldType.Float && field.Value == 0.0))
                    {
                        return new ErrorExpression("Division by zero");
                    }

                    if (MemoryAccessor.Field.IsMemoryReference)
                        return new ErrorExpression("Cannot modulus using a runtime value");
                }

                return new ErrorExpression("Cannot combine " + Type + " and " + right.Type + " using " + operation);
            }

            if (IsBitwiseOperator(newModifyingOperator))
            {
                if (field.IsFloat || Modifier.IsFloat || MemoryAccessor.Field.IsFloat)
                    return new ErrorExpression("Cannot perform bitwise operations on floating point values");

                if (newModifyingOperator == RequirementOperator.BitwiseAnd && field.Type == FieldType.Value)
                {
                    long min, max;
                    MemoryAccessor.GetMinMax(out min, out max);
                    if ((max & 0x01) != 0)
                        field.Value &= (uint)max;
                }

                if (ModifyingOperator != RequirementOperator.None && !IsBitwiseOperator(ModifyingOperator))
                    return new ErrorExpression("Cannot combine bitwise and arithmetic operations");
            }

            switch (ModifyingOperator)
            {
                case RequirementOperator.None:
                    ModifyingOperator = newModifyingOperator;
                    break;

                case RequirementOperator.Multiply:
                case RequirementOperator.Divide:
                    if (field.Type == FieldType.Float || Modifier.Type == FieldType.Float)
                    {
                        Modifier = FieldFactory.ConvertToFloat(Modifier);
                        field = FieldFactory.ConvertToFloat(field);

                        if (ModifyingOperator == RequirementOperator.Divide)
                        {
                            if (newModifyingOperator == RequirementOperator.Multiply)
                            {
                                // a / 6.0 * 2.0 => a / (6.0 / 2.0)
                                newModifyingOperator = RequirementOperator.Divide;
                            }
                            else if (newModifyingOperator == RequirementOperator.Divide)
                            {
                                // a / 2.0 / 4.0 => a / (2.0 * 4.0)
                                newModifyingOperator = RequirementOperator.Multiply;
                            }
                        }
                    }
                    else if (ModifyingOperator != newModifyingOperator)
                    {
                        if (Modifier.Type == FieldType.Value && field.Type == FieldType.Value)
                        {
                            if (ModifyingOperator == RequirementOperator.Multiply && newModifyingOperator == RequirementOperator.Divide)
                            {
                                // a * 4 / 2 => a * 2
                                if (Modifier.Value % field.Value == 0)
                                {
                                    field = FieldFactory.ApplyMathematic(Modifier, RequirementOperator.Divide, field);
                                    break;
                                }

                                // a * 2 / 4 => a / 2
                                if (field.Value % Modifier.Value == 0)
                                {
                                    ModifyingOperator = RequirementOperator.Divide;
                                    field = FieldFactory.ApplyMathematic(field, RequirementOperator.Divide, Modifier);
                                    break;
                                }

                                // remainder after dividing, don't do it
                                goto default;
                            }
                        }

                        goto default;
                    }
                    else
                    {
                        // a / 2 / 4 => a / (2 * 4)
                        if (newModifyingOperator == RequirementOperator.Divide)
                            newModifyingOperator = RequirementOperator.Multiply;
                    }

                    field = FieldFactory.ApplyMathematic(Modifier, newModifyingOperator, field);
                    break;

                case RequirementOperator.BitwiseAnd:
                case RequirementOperator.BitwiseXor:
                    if (field.Type == FieldType.Float || Modifier.Type == FieldType.Float)
                        return new ErrorExpression("Cannot perform bitwise operations on floating point values");
                    if (!IsBitwiseOperator(newModifyingOperator))
                        return new ErrorExpression("Cannot combine bitwise and arithmetic operations");

                    if (MathematicExpression.GetPriority(GetMathematicOperation(newModifyingOperator)) >
                        MathematicExpression.GetPriority(GetMathematicOperation(ModifyingOperator)))
                    {
                        // new operator has priority, don't merge
                        goto default;
                    }

                    field = FieldFactory.ApplyMathematic(Modifier, newModifyingOperator, field);
                    break;

                default:
                    // return a MathematicExpression for now, it may get reduced in a comparison normalization
                    return new MathematicExpression(this, operation, right);
            }

            if (field.Type == FieldType.None)
                return new MathematicExpression(this, operation, right);

            if (ModifyingOperator == RequirementOperator.BitwiseAnd && field.Type == FieldType.Value)
            {
                var masked = ApplyMask(field.Value);
                if (masked != null)
                    return masked;
            }

            if ((field.Type == FieldType.Value && field.Value == 0) ||
                (field.Type == FieldType.Float && field.Float == 0.0))
            {
                switch (ModifyingOperator)
                {
                    case RequirementOperator.Divide:
                        return new ErrorExpression("Division by zero");

                    case RequirementOperator.Multiply:   // a * 0  =>  0
                    case RequirementOperator.BitwiseAnd: // a & 0  =>  0
                        return new IntegerConstantExpression(0);

                    case RequirementOperator.BitwiseXor: // a ^ 0  =>  a
                        ModifyingOperator = RequirementOperator.None;
                        break;
                }
            }
            else if ((field.Type == FieldType.Value && field.Value == 1) ||
                     (field.Type == FieldType.Float && field.Float == 1.0))
            {
                switch (ModifyingOperator)
                {
                    case RequirementOperator.Multiply: // a * 1  =>  a
                    case RequirementOperator.Divide:   // a / 1  =>  a
                        ModifyingOperator = RequirementOperator.None;
                        break;
                }
            }

            Modifier = field;
            return this;
        }

        /// <summary>
        /// Gets the lowest and highest values that can be represented by this expression.
        /// </summary>
        public override void GetMinMax(out long min, out long max)
        {
            long accessorMin = 0;
            long accessorMax = Field.GetMaxValue(MemoryAccessor.Field.Size);
            if (MemoryAccessor.Field.Type == FieldType.Value)
                accessorMin = accessorMax = MemoryAccessor.Field.Value;

            uint modifierMin = 0;
            uint modifierMax = Field.GetMaxValue(Modifier.Size);
            if (Modifier.Type == FieldType.Value)
                modifierMin = modifierMax = Modifier.Value;
                
            switch (ModifyingOperator)
            {
                case RequirementOperator.Multiply:
                    min = accessorMin * modifierMin;
                    max = accessorMax * modifierMax;
                    break;

                case RequirementOperator.Divide:
                    if (MemoryAccessor.Field == Modifier)
                    {
                        // X / X will always be 0 or 1
                        min = 0;
                        max = 1;
                    }
                    else if (modifierMin == 0)
                    {
                        // division by zero in the runtime will output a 0
                        min = 0;
                        max = (modifierMax == 0) ? 0 : accessorMax;
                    }
                    else
                    {
                        // smallest value will be the smallest left divided by the largest right
                        // largest value will be the largest left divided by the smallest right
                        // don't need to worry about division by zero because a non-zero modifierMin
                        // also implies a non-zero modifiedMax
                        min = accessorMin / modifierMax;
                        max = accessorMax / modifierMin;
                    }
                    break;

                case RequirementOperator.BitwiseAnd:
                case RequirementOperator.BitwiseXor:
                    min = 0;
                    max = modifierMax;
                    break;

                default:
                    min = accessorMin;
                    max = accessorMax;
                    break;
            }
        }

        private static RequirementOperator GetModifyingOperator(MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Multiply: return RequirementOperator.Multiply;
                case MathematicOperation.Divide: return RequirementOperator.Divide;
                case MathematicOperation.BitwiseAnd: return RequirementOperator.BitwiseAnd;
                case MathematicOperation.BitwiseXor: return RequirementOperator.BitwiseXor;
                default: return RequirementOperator.None;
            }
        }

        private static MathematicOperation GetMathematicOperation(RequirementOperator operation)
        {
            switch (operation)
            {
                case RequirementOperator.Multiply: return MathematicOperation.Multiply;
                case RequirementOperator.Divide: return MathematicOperation.Divide;
                case RequirementOperator.BitwiseAnd: return MathematicOperation.BitwiseAnd;
                case RequirementOperator.BitwiseXor: return MathematicOperation.BitwiseXor;
                default: return MathematicOperation.None;
            }
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
            right = ReduceToSimpleExpression(right);

            if (ModifyingOperator == RequirementOperator.None && CombiningOperator == RequirementType.None)
            {
                var normalized = MemoryAccessor.NormalizeComparison(right, operation, canModifyRight);
                if (normalized != null)
                    return normalized;
            }

            var modifiedMemoryAccessor = right as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null)
            {
                if (modifiedMemoryAccessor.ModifyingOperator == ModifyingOperator &&
                    modifiedMemoryAccessor.Modifier == Modifier)
                {
                    // same modifier applied to both sides, eliminate it
                    return new ComparisonExpression(MemoryAccessor, operation, modifiedMemoryAccessor.MemoryAccessor);
                }

                var opposingOperator = MathematicExpression.GetOppositeOperation(GetMathematicOperation(modifiedMemoryAccessor.ModifyingOperator));
                if (opposingOperator != MathematicOperation.Multiply && opposingOperator != MathematicOperation.Divide)
                    return null;

                // prefer modifiers on left - try to merge left
                var modifier = modifiedMemoryAccessor.CreateModifierExpression();
                var newLeft = Clone();
                var newRight = modifiedMemoryAccessor.Clone();
                var result = newLeft.ApplyMathematic(modifier, opposingOperator);

                var mergeSuccessful = true;
                if (result is MathematicExpression)
                {
                    // could not merge 
                    mergeSuccessful = false;
                }
                else
                {
                    var modifiedMemoryAccessorResult = result as ModifiedMemoryAccessorExpression;
                    if (modifiedMemoryAccessorResult != null && 
                        modifiedMemoryAccessorResult.ModifyingOperator == RequirementOperator.Divide &&
                        ModifyingOperator == RequirementOperator.Multiply)
                    {
                        // multiplication changed to division may result in false positives due to integer
                        // division truncation. i.e. 17*10 is not equal to 8*20, but 17/2 is equal to 8 
                        mergeSuccessful = false;
                    }
                }

                if (!mergeSuccessful)
                {
                    // could not merge left. try merging right
                    opposingOperator = MathematicExpression.GetOppositeOperation(GetMathematicOperation(ModifyingOperator));
                    modifier = CreateModifierExpression();
                    result = newRight.ApplyMathematic(modifier, opposingOperator);

                    if (result is MathematicExpression)
                    {
                        var zero = new UnsignedIntegerConstantExpression(0U);
                        var negative = new UnsignedIntegerConstantExpression(0x80000000U);

                        // left and right are both modified memory expressions. use subsource and check the negative bit
                        var memoryValue = new MemoryValueExpression(this);
                        memoryValue.ApplyMathematic(modifiedMemoryAccessor.Clone(), MathematicOperation.Subtract);
                        switch (operation)
                        {
                            case ComparisonOperation.Equal:
                            case ComparisonOperation.NotEqual:
                                // A * 3 == B * 4  =>  A * 3 - B * 4 == 0  =>  A * 3 - B * 4 == 0
                                // A * 3 != B * 4  =>  A * 3 - B * 4 != 0  =>  A * 3 - B * 4 != 0
                                return new ComparisonExpression(memoryValue, operation, zero);

                            case ComparisonOperation.LessThan:
                            case ComparisonOperation.GreaterThanOrEqual:
                                // A * 3 <  B * 4  =>  A * 3 - B * 4 <  0  =>  A * 3 - B * 4 >= 0x80000000
                                // A * 3 >= B * 4  =>  A * 3 - B * 4 >= 0  =>  A * 3 - B * 4 <  0x80000000
                                var newOperation = ComparisonExpression.GetOppositeComparisonOperation(operation);
                                return new ComparisonExpression(memoryValue, newOperation, negative);

                            case ComparisonOperation.LessThanOrEqual:
                                // A * 3 <= B * 4  =>  A * 3 - B * 4 <= 0  =>  A * 3 - B * 4 >= 0x80000000 || == 0
                                var negativeComparison = new RequirementConditionExpression() { Left = memoryValue, Comparison = ComparisonOperation.GreaterThanOrEqual, Right = negative };
                                var zeroComparison = new RequirementConditionExpression() { Left = memoryValue, Comparison = ComparisonOperation.Equal, Right = zero };
                                var lessThanOrEqualClause = new RequirementClauseExpression() { Operation = ConditionalOperation.Or };
                                lessThanOrEqualClause.AddCondition(negativeComparison);
                                lessThanOrEqualClause.AddCondition(zeroComparison);
                                return lessThanOrEqualClause;

                            case ComparisonOperation.GreaterThan:
                                // A * 3 >  B * 4  =>  A * 3 - B * 4 >  0  =>  A * 3 - B * 4 <  0x80000000 && != 0
                                var positiveComparison = new RequirementConditionExpression() { Left = memoryValue, Comparison = ComparisonOperation.LessThan, Right = negative };
                                var nonZeroComparison = new RequirementConditionExpression() { Left = memoryValue, Comparison = ComparisonOperation.NotEqual, Right = zero };
                                var greaterThanClause = new RequirementClauseExpression() { Operation = ConditionalOperation.And };
                                greaterThanClause.AddCondition(positiveComparison);
                                greaterThanClause.AddCondition(nonZeroComparison);
                                return greaterThanClause;

                            default:
                                return new ErrorExpression("Result can never be true using integer math");
                        }
                    }

                    // swap so modifier is on left
                    newRight = newLeft;
                    operation = ComparisonExpression.ReverseComparisonOperation(operation);
                }

                if (result is ErrorExpression)
                    return result;

                newRight.ModifyingOperator = RequirementOperator.None;
                return new ComparisonExpression(result, operation, newRight);
            }

            var mathematicOperation = GetMathematicOperation(ModifyingOperator);
            if (mathematicOperation != MathematicOperation.None)
            {
                var modifier = CreateModifierExpression();
                if (modifier != null)
                {
                    var newLeft = Clone();
                    newLeft.ModifyingOperator = RequirementOperator.None;

                    var mathematic = new MathematicExpression(newLeft, mathematicOperation, modifier);
                    return mathematic.NormalizeComparison(right, operation, canModifyRight);
                }
            }

            if (right is FloatConstantExpression && !MemoryAccessor.Field.IsFloat)
                return ComparisonExpression.NormalizeFloatComparisonForInteger(this, operation, right);

            return null;
        }

        private ExpressionBase CreateModifierExpression()
        {
            switch (Modifier.Type)
            {
                case FieldType.Value:
                    return new IntegerConstantExpression((int)Modifier.Value);

                case FieldType.Float:
                    return new FloatConstantExpression(Modifier.Float);

                default:
                    return new MemoryAccessorExpression(Modifier);
            }
        }

        public ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            MemoryAccessor.BuildTrigger(context);
            if (context.LastRequirement.Type != RequirementType.None)
                return new ErrorExpression("Cannot combine modified requirement", MemoryAccessor);
            context.LastRequirement.Type = CombiningOperator;

            if (ModifyingOperator != RequirementOperator.None)
            {
                if (context.LastRequirement.Operator != RequirementOperator.None)
                    return new ErrorExpression("Cannot combine modified requirement", MemoryAccessor);
                context.LastRequirement.Operator = ModifyingOperator;

                if (context.LastRequirement.Right.Type != FieldType.None)
                    return new ErrorExpression("Cannot combine modified requirement", MemoryAccessor);
                context.LastRequirement.Right = Modifier;
            }

            return null;
        }

        public ErrorExpression Execute(InterpreterScope scope)
        {
            // report error if this occurs outside a trigger clause
            return MemoryAccessor.Execute(scope);
        }
    }
}
