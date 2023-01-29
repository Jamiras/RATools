using RATools.Data;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    /// <summary>
    /// A sequence of <see cref="ModifiedMemoryAccessorExpression"/> that are summed together.
    /// </summary>
    internal class MemoryValueExpression : MemoryAccessorExpressionBase,
        ITriggerExpression, IExecutableExpression, IMathematicCombineExpression,
        IMathematicCombineInverseExpression, IComparisonNormalizeExpression,
        ICloneableExpression, INestedExpressions
    {
        public MemoryValueExpression()
            : base()
        {
        }

        public MemoryValueExpression(ModifiedMemoryAccessorExpression modifiedMemoryAccessor)
            : this()
        {
            ApplyAddition(modifiedMemoryAccessor, RequirementType.AddSource);
        }

        /// <summary>
        /// Gets the numerical offset to apply to the value read from memory.
        /// </summary>
        public int IntegerConstant { get; private set; }

        /// <summary>
        /// Gets the numerical offset to apply to the value read from memory.
        /// </summary>
        public double FloatConstant { get; private set; }

        /// <summary>
        /// Gets the list of <see cref="ModifiedMemoryAccesorExpression"/>s that are summed together.
        /// </summary>
        public IEnumerable<ModifiedMemoryAccessorExpression> MemoryAccessors
        {
            get { return _memoryAccessors ?? Enumerable.Empty<ModifiedMemoryAccessorExpression>(); }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected List<ModifiedMemoryAccessorExpression> _memoryAccessors;

        /// <summary>
        /// Returns <c>true</c> if <see cref="IntegerConstant"/> or <see cref="FloatConstant"/> are not zero.
        /// </summary>
        public bool HasConstant
        {
            get
            {
                return IntegerConstant != 0 || FloatConstant != 0.0;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="MemoryAccessors"/> is not empty.
        /// </summary>
        public bool HasMemoryAccessor
        {
            get
            {
                return _memoryAccessors != null && _memoryAccessors.Count > 0;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="MemoryAccessors"/> contains any subtracted accessors.
        /// </summary>
        private bool HasSubtractedMemoryAccessor
        {
            get
            {
                return _memoryAccessors != null && _memoryAccessors.Any(a => a.CombiningOperator == RequirementType.SubSource);
            }
        }

        /// <summary>
        /// Returns <c>true</c> if <see cref="MemoryAccessors"/> contains any added accessors.
        /// </summary>
        private bool HasAddedMemoryAccessor
        {
            get
            {
                return _memoryAccessors != null && _memoryAccessors.Any(a => a.CombiningOperator == RequirementType.AddSource);
            }
        }

        /// <summary>
        /// Combines the current expression with the <paramref name="right"/> expression using the <paramref name="operation"/> operator.
        /// </summary>
        /// <param name="right">The expression to combine with the current expression.</param>
        /// <param name="operation">How to combine the expressions.</param>
        /// <returns>
        /// An expression representing the combined values on success, or <c>null</c> if the expressions could not be combined.
        /// </returns>
        public ExpressionBase CombineInverse(ExpressionBase left, MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Multiply:
                    // multiplication is commutative
                    return Combine(left, operation);

                case MathematicOperation.Divide:
                    var reduced = ConvertToModifiedMemoryAccessor();
                    if (reduced == null)
                        return new ErrorExpression("Cannot divide by complex memory reference");
                    return reduced.CombineInverse(left, operation);

                default:
                    // attempt to convert left to MemoryValue, then apply right
                    var value = new MemoryValueExpression();

                    var error = value.ApplyMathematic(left, MathematicOperation.Add);
                    if (error.Type == ExpressionType.Error)
                        return error;

                    return value.ApplyMathematic(this, operation);
            }
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
            switch (operation)
            {
                case MathematicOperation.Multiply:
                case MathematicOperation.Divide:
                    var clone = new MemoryValueExpression() { Location = this.Location };
                    if (_memoryAccessors != null)
                    {
                        clone._memoryAccessors = new List<ModifiedMemoryAccessorExpression>();
                        foreach (var accessor in _memoryAccessors)
                        {
                            var combined = accessor.Clone().Combine(right, operation);
                            if (combined is ErrorExpression)
                                return combined;
                            var newAccessor = combined as ModifiedMemoryAccessorExpression;
                            if (newAccessor == null)
                                return null;

                            clone._memoryAccessors.Add(newAccessor);
                        }
                    }

                    if (FloatConstant != 0)
                    {
                        var combined = new FloatConstantExpression((float)(FloatConstant + IntegerConstant)).Combine(right, operation);
                        var result = clone.ApplyMathematic(combined, MathematicOperation.Add);
                        if (result != null)
                            return result;
                    }
                    else if (IntegerConstant != 0)
                    {
                        var integerConstant = new IntegerConstantExpression(IntegerConstant);
                        if (operation == MathematicOperation.Divide)
                        {
                            var remainder = integerConstant.Combine(right, MathematicOperation.Modulus);
                            if (remainder is IntegerConstantExpression && ((IntegerConstantExpression)remainder).Value != 0)
                            {
                                // remainder after dividing, don't do it
                                // return a MathematicExpression for now, it may get reduced in a comparison normalization
                                return new MathematicExpression(this, operation, right);
                            }
                        }

                        var combined = integerConstant.Combine(right, operation);
                        var result = clone.ApplyMathematic(combined, MathematicOperation.Add);
                        if (result != null)
                            return result;
                    }

                    return clone;

                default:
                    return Clone().ApplyMathematic(right, operation);
            }
        }

        public ExpressionBase ApplyMathematic(ExpressionBase right, MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Add:
                    return ApplyAddition(right, RequirementType.AddSource);

                case MathematicOperation.Subtract:
                    return ApplyAddition(right, RequirementType.SubSource);

                default:
                    return new MathematicExpression(this, operation, right);
            }
        }

        private ExpressionBase ApplyAddition(ExpressionBase right, RequirementType combiningOperator)
        {
            if (IsReadOnly)
                return Clone().ApplyAddition(right, combiningOperator);

            switch (right.Type)
            {
                case ExpressionType.IntegerConstant:
                    if (combiningOperator == RequirementType.AddSource)
                        IntegerConstant += ((IntegerConstantExpression)right).Value;
                    else
                        IntegerConstant -= ((IntegerConstantExpression)right).Value;
                    break;

                case ExpressionType.FloatConstant:
                    if (combiningOperator == RequirementType.AddSource)
                        FloatConstant += ((FloatConstantExpression)right).Value;
                    else
                        FloatConstant -= ((FloatConstantExpression)right).Value;
                    break;

                case ExpressionType.MemoryAccessor:
                    var memoryValue = right as MemoryValueExpression;
                    if (memoryValue != null)
                    {
                        if (combiningOperator == RequirementType.AddSource)
                        {
                            if (memoryValue._memoryAccessors != null)
                            {
                                if (_memoryAccessors == null)
                                    _memoryAccessors = new List<ModifiedMemoryAccessorExpression>();
                                foreach (var accessor in memoryValue.MemoryAccessors)
                                    _memoryAccessors.Add(accessor);
                            }

                            IntegerConstant += memoryValue.IntegerConstant;
                            FloatConstant += memoryValue.FloatConstant;
                        }
                        else
                        {
                            if (memoryValue._memoryAccessors != null)
                            {
                                if (_memoryAccessors == null)
                                    _memoryAccessors = new List<ModifiedMemoryAccessorExpression>();
                                foreach (var accessor in memoryValue.MemoryAccessors)
                                    _memoryAccessors.Add(accessor.InvertCombiningOperator());
                            }

                            IntegerConstant -= memoryValue.IntegerConstant;
                            FloatConstant -= memoryValue.FloatConstant;
                        }
                        break;
                    }

                    var modifiedMemoryAccessor = right as ModifiedMemoryAccessorExpression;
                    if (modifiedMemoryAccessor != null)
                    {
                        if (modifiedMemoryAccessor.CombiningOperator != combiningOperator)
                        {
                            modifiedMemoryAccessor = modifiedMemoryAccessor.Clone();
                            modifiedMemoryAccessor.CombiningOperator = combiningOperator;
                            modifiedMemoryAccessor.MakeReadOnly();
                        }
                    }
                    else
                    {
                        var memoryAccessor = right as MemoryAccessorExpression;
                        if (memoryAccessor == null)
                            goto default;

                        modifiedMemoryAccessor = new ModifiedMemoryAccessorExpression(memoryAccessor);
                        modifiedMemoryAccessor.CombiningOperator = combiningOperator;
                        modifiedMemoryAccessor.MakeReadOnly();
                    }

                    if (_memoryAccessors == null)
                        _memoryAccessors = new List<ModifiedMemoryAccessorExpression>();
                    _memoryAccessors.Add(modifiedMemoryAccessor);
                    break;

                default:
                    return new ErrorExpression(String.Format("Cannot {0} {1} to requirement clause", (combiningOperator == RequirementType.AddSource) ? "add" : "subtract", right.Type));
            }

            return this;
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
            var simplified = ReduceToSimpleExpression(this) as IComparisonNormalizeExpression;
            if (simplified != null && !ReferenceEquals(simplified, this))
                return simplified.NormalizeComparison(right, operation, canModifyRight);

            right = ReduceToSimpleExpression(right);

            var normalized = canModifyRight ? MoveConstantsToRightHandSide(right, operation) : null;
            var comparison = normalized ?? new ComparisonExpression(this, operation, right);

            var bitcountNormalized = MergeBitCount(comparison, canModifyRight);
            if (!ReferenceEquals(bitcountNormalized, comparison))
                normalized = comparison = bitcountNormalized;

            if (canModifyRight)
            {
                var underflowNormalized = CheckForUnderflow(comparison);
                if (!ReferenceEquals(underflowNormalized, comparison))
                    normalized = underflowNormalized;
            }

            return CheckForImpossibleValues(normalized);
        }

        private ExpressionBase MoveConstantsToRightHandSide(ExpressionBase right, ComparisonOperation operation)
        {
            var memoryValue = right as MemoryValueExpression;
            if (memoryValue == null)
            {
                memoryValue = new MemoryValueExpression();
                memoryValue.ApplyMathematic(right, MathematicOperation.Add);
            }

            return MoveConstantsToRightHandSide(memoryValue, operation);
        }

        private ExpressionBase MoveConstantsToRightHandSide(MemoryValueExpression memoryValue, ComparisonOperation operation)
        {
            // special handling for pointers
            // try to find a single AddAdress that can be applied to both sides of the equation
            if (_memoryAccessors != null && _memoryAccessors.Count >= 2)
            {
                var result = RebalanceForPointerChain(memoryValue, operation);
                if (result != null)
                    return result;
            }

            // if the left side doesn't have a constant to move, just normalize the right side
            if (!HasConstant)
                return EnsureSingleExpressionOnRightHandSide(memoryValue, operation);

            // constant on left. check for a constant on the right too
            if (memoryValue.HasConstant)
            {
                if (operation == ComparisonOperation.Equal || operation == ComparisonOperation.NotEqual)
                {
                    // direct comparisons aren't affected by underflow.
                    return EnsureSingleExpressionOnRightHandSide(memoryValue, operation);
                }

                // if there are constants on both sides of the comparison and both added and
                // subtracted accessors on the left, then assume the user is trying to account
                // for the underflow themselves and don't merge the constants.
                if (HasSubtractedMemoryAccessor && HasAddedMemoryAccessor)
                    return null;
            }

            var integerConstant = memoryValue.IntegerConstant - IntegerConstant;
            var floatConstant = memoryValue.FloatConstant - FloatConstant;
            var constantSum = floatConstant + integerConstant;

            if (constantSum < 0.0)
            {
                // if there's no constant on the right, moving the left constant will create a negative constant on
                // the right and it'll just get moved back to the left. do nothing
                if (!memoryValue.HasConstant)
                {
                    // cannot leave pointer chain on right side unless it matches the left.
                    if (memoryValue._memoryAccessors.Any(
                        a => a.MemoryAccessor.HasPointerChain &&
                             !_memoryAccessors.Any(b => b.MemoryAccessor.PointerChainMatches(a))))
                    {
                        // move non-constants to right and reverse
                        var constant = ExtractConstant();
                        var newComparison = memoryValue.Clone();
                        newComparison = Clone().InvertAndMigrateAccessorsTo(newComparison);
                        return new ComparisonExpression(newComparison, ComparisonExpression.ReverseComparisonOperation(operation), constant);
                    }

                    return null;
                }

                // left constant is greater than right constant, reverse comparison
                operation = ComparisonExpression.ReverseComparisonOperation(operation);
                return memoryValue.MoveConstantsToRightHandSide(this, operation);
            }

            var newLeft = ClearConstant();
            var newRight = memoryValue.ClearConstant();

            // move all accessors to left side
            var rightValue = newRight as MemoryValueExpression;
            if (rightValue != null)
                newLeft = rightValue.InvertAndMigrateAccessorsTo(newLeft);

            // construct a constant for the right side
            ExpressionBase constantExpression;
            if (Math.Floor(constantSum) == constantSum)
                constantExpression = new IntegerConstantExpression((int)(uint)constantSum);
            else
                constantExpression = new FloatConstantExpression((float)constantSum);

            if (IsZero(constantExpression))
            {
                // constants canceled out
            }
            else if (newLeft is MemoryAccessorExpression ||
                    newLeft.IsLiteralConstant)
            {
                // new left is non-complex, reverse the equation to eliminate the subtraction

                var reverseOperation = ComparisonExpression.ReverseComparisonOperation(operation);
                if (reverseOperation == operation)
                {
                    // if both sides are non-complex, move the constant to the left side
                    if (!memoryValue.HasConstant)
                        return null;

                    newLeft = ((MemoryAccessorExpression)newLeft).Combine(constantExpression, MathematicOperation.Subtract);
                }
                else
                {
                    operation = reverseOperation;

                    // A - N < B  ~>  B + N > A
                    var toMove = newRight;
                    newRight = newLeft;
                    Debug.Assert(toMove is IMathematicCombineExpression);
                    newLeft = ((IMathematicCombineExpression)toMove).Combine(constantExpression, MathematicOperation.Add);
                    Debug.Assert(newLeft != null);
                }
            }
            else
            {
                // A + B - N < C  ~>  A + B - C < N
                Debug.Assert(newLeft is IMathematicCombineExpression);
                newLeft = ((IMathematicCombineExpression)newLeft).Combine(newRight, MathematicOperation.Subtract);
                Debug.Assert(newLeft != null);

                newRight = constantExpression;
            }

            var leftValue = newLeft as MemoryValueExpression;
            if (leftValue != null)
            {
                if (IsZero(newRight))
                    return leftValue.SwapSubtractionWithConstant(newRight, operation);

                // if a single AddAdress can be applied to both sides of the equation, prefer that
                // over having a constant on the right side as the AddAddress will be shared,
                // resulting in one less condition required.
                if (leftValue._memoryAccessors != null && leftValue._memoryAccessors.Count >= 2)
                {
                    var result = leftValue.RebalanceForPointerChain(newRight, operation);
                    if (result != null)
                    {
                        var comparison = result as ComparisonExpression;
                        if (comparison != null && comparison.Operation == operation && comparison.Left == this)
                        {
                            var simpleNewRight = ReduceToSimpleExpression(comparison.Right);
                            var simpleRight = ReduceToSimpleExpression(memoryValue);

                            if (simpleNewRight == simpleRight)
                            {
                                // rebalanced expression matched original expression, return "no change"
                                return null;
                            }
                        }

                        return result;
                    }
                }
            }

            return new ComparisonExpression(newLeft, operation, newRight);
        }

        private ExpressionBase EnsureSingleExpressionOnRightHandSide(ExpressionBase right, ComparisonOperation operation)
        {
            var memoryValue = right as MemoryValueExpression;
            if (memoryValue == null)
            {
                memoryValue = new MemoryValueExpression();
                memoryValue.ApplyMathematic(right, MathematicOperation.Add);
            }

            if (!memoryValue.HasMemoryAccessor && !HasConstant)
            {
                // right side is alredy just a constant, don't move it
                if (memoryValue.IntegerConstant != 0 || memoryValue.FloatConstant != 0.0)
                    return null;

                // right side is zero. attempt to move a subtraction over there
                return SwapSubtractionWithConstant(new IntegerConstantExpression(0), operation);
            }

            if (memoryValue.IntegerConstant == IntegerConstant &&
                memoryValue.FloatConstant == FloatConstant && HasConstant)
            {
                // constant is the same on both sidees, eliminate it
                var newLeft = ClearConstant();
                var newRight = memoryValue.ClearConstant();

                memoryValue = newLeft as MemoryValueExpression;
                if (memoryValue != null)
                    return memoryValue.EnsureSingleExpressionOnRightHandSide(newRight, operation);

                return new ComparisonExpression(newLeft, operation, newRight);
            }

            // right side is just a single accessor, don't move it
            if (!memoryValue.HasConstant && memoryValue._memoryAccessors != null && memoryValue._memoryAccessors.Count == 1)
                return null;

            var cloneLeft = Clone();
            cloneLeft.IntegerConstant = 0;
            cloneLeft.FloatConstant = 0.0;
            var cloneRight = memoryValue.Clone();
            cloneLeft = cloneRight.InvertAndMigrateAccessorsTo(cloneLeft);

            cloneRight.ApplyMathematic(ExtractConstant(), MathematicOperation.Subtract);
            var constant = cloneRight.ExtractConstant();
            if (IsZero(constant) || IsNegative(constant))
            {
                var result = cloneLeft.SwapSubtractionWithConstant(constant, operation);
                if (result != null)
                    return result;
            }

            return new ComparisonExpression(cloneLeft, operation, constant);
        }

        private MemoryValueExpression InvertAndMigrateAccessorsTo(ExpressionBase target)
        {
            var value = target as MemoryValueExpression;
            if (value == null)
            {
                value = new MemoryValueExpression();
                value.ApplyMathematic(target, MathematicOperation.Add);
            }
            if (value._memoryAccessors == null)
                value._memoryAccessors = new List<ModifiedMemoryAccessorExpression>();

            foreach (var memoryAccessor in MemoryAccessors)
                value._memoryAccessors.Add(memoryAccessor.InvertCombiningOperator());

            _memoryAccessors = null;
            return value;
        }

        private ExpressionBase RebalanceForPointerChain(ExpressionBase right, ComparisonOperation operation)
        {
            for (int i = _memoryAccessors.Count - 1; i >= 0; i--)
            {
                if (_memoryAccessors[i].CombiningOperator != RequirementType.SubSource)
                    continue;

                if (!_memoryAccessors[i].MemoryAccessor.HasPointerChain)
                    continue;

                // found a SubSource with a pointer. see if there's an AddSource with the same pointer
                // try to match a prev with it's non-prev first
                var searchMemoryAccessor = _memoryAccessors[i].MemoryAccessor;
                var paired = _memoryAccessors.FirstOrDefault(a =>
                    a.CombiningOperator == RequirementType.AddSource &&
                    a.MemoryAccessor.Field.Value == searchMemoryAccessor.Field.Value &&
                    a.MemoryAccessor.Field.Size == searchMemoryAccessor.Field.Size &&
                    a.MemoryAccessor.PointerChainMatches(_memoryAccessors[i].MemoryAccessor));

                if (paired == null)
                {
                    // could not find a prev/non-prev match. try again for any shared pointer chain
                    paired = _memoryAccessors.FirstOrDefault(a =>
                        a.CombiningOperator == RequirementType.AddSource &&
                        a.MemoryAccessor.PointerChainMatches(_memoryAccessors[i].MemoryAccessor));
                }

                if (paired != null)
                {
                    // found a pair, move the SubSource to the right side and the constant to the left
                    var newLeft = (MemoryValueExpression)Combine(right, MathematicOperation.Subtract);
                    var newRight = newLeft._memoryAccessors[i].MemoryAccessor;
                    newLeft._memoryAccessors.RemoveAt(i);
                    return new ComparisonExpression(newLeft, operation, newRight);
                }
            }

            return null;
        }

        private class BitReference
        {
            public uint Address;
            public FieldType Type;
            public RequirementType CombiningOperator;
            public byte Mask;
            public List<ModifiedMemoryAccessorExpression> MemoryAccessors;

            public static BitReference Find(List<BitReference> bitReferences, ModifiedMemoryAccessorExpression memoryAccessor)
            {
                foreach (var scan in bitReferences)
                {
                    if (scan.Address == memoryAccessor.MemoryAccessor.Field.Value &&
                        scan.Type == memoryAccessor.MemoryAccessor.Field.Type &&
                        scan.CombiningOperator == memoryAccessor.CombiningOperator)
                    {
                        if (scan.MemoryAccessors[0].MemoryAccessor.PointerChainMatches(memoryAccessor.MemoryAccessor))
                            return scan;
                    }
                }

                var bitReference = new BitReference
                {
                    Address = memoryAccessor.MemoryAccessor.Field.Value,
                    Type = memoryAccessor.MemoryAccessor.Field.Type,
                    CombiningOperator = memoryAccessor.CombiningOperator,
                    MemoryAccessors = new List<ModifiedMemoryAccessorExpression>()
                };
                bitReferences.Add(bitReference);
                return bitReference;
            }

            public static byte GetMask(ModifiedMemoryAccessorExpression memoryAccessor)
            {
                switch (memoryAccessor.MemoryAccessor.Field.Size)
                {
                    case FieldSize.Bit0: return 0x01;
                    case FieldSize.Bit1: return 0x02;
                    case FieldSize.Bit2: return 0x04;
                    case FieldSize.Bit3: return 0x08;
                    case FieldSize.Bit4: return 0x10;
                    case FieldSize.Bit5: return 0x20;
                    case FieldSize.Bit6: return 0x40;
                    case FieldSize.Bit7: return 0x80;
                    default: return 0x00;
                }
            }
        };

        private static ExpressionBase MergeBitCount(ExpressionBase expression, bool canModifyRight)
        {
            var comparison = expression as ComparisonExpression;
            if (comparison == null)
                return expression;

            var valueExpression = comparison.Left as MemoryValueExpression;
            if (valueExpression == null || valueExpression._memoryAccessors.Count < 4)
                return expression;

            // identify bit accessors in the MemoryValueExpression
            var newMemoryAccessors = new List<ModifiedMemoryAccessorExpression>(valueExpression._memoryAccessors);
            var bitReferences = new List<BitReference>();

            foreach (var memoryAccessor in newMemoryAccessors)
            {
                if (memoryAccessor.ModifyingOperator == RequirementOperator.None &&
                    memoryAccessor.MemoryAccessor.Field.IsMemoryReference)
                {
                    byte mask = BitReference.GetMask(memoryAccessor);
                    if (mask == 0)
                        continue;

                    var bitReference = BitReference.Find(bitReferences, memoryAccessor);
                    bitReference.Mask |= mask;
                    bitReference.MemoryAccessors.Add(memoryAccessor);
                }
            }

            // if any address is fully represented, convert to a bitcount
            foreach (var bitReference in bitReferences)
            {
                while (bitReference.Mask == 0xFF)
                {
                    // reset the mask in case there's more than one set of matches
                    bitReference.Mask = 0;

                    // clone an accessor that's going to be removed so we have the correct
                    // pointer chain and turn it into a bitcount accessor
                    var bitCountAccessor = bitReference.MemoryAccessors[0].MemoryAccessor.ChangeFieldSize(FieldSize.BitCount);

                    // extract one accessor for each bit
                    byte matchedMask = 0;
                    int index = 0;
                    int replaceIndex = newMemoryAccessors.Count;
                    while (index < bitReference.MemoryAccessors.Count)
                    {
                        var memoryAccessor = bitReference.MemoryAccessors[index];
                        var mask = BitReference.GetMask(memoryAccessor);
                        if ((matchedMask & mask) != 0)
                        {
                            // this bit is already represented, save it for later
                            bitReference.Mask |= mask;
                            index++;
                            continue;
                        }

                        // found an unrepresented bit, remove it from the accessors list, keeping
                        // track of the lowest removed index. that's where we'll insert the bitcount
                        matchedMask |= mask;

                        var accesorIndex = newMemoryAccessors.IndexOf(memoryAccessor);
                        newMemoryAccessors.RemoveAt(accesorIndex);

                        replaceIndex = Math.Min(replaceIndex, accesorIndex);

                        // also remove from the unmatched accessors
                        bitReference.MemoryAccessors.RemoveAt(index);
                    }

                    // expecting to find a full bitcount
                    Debug.Assert(matchedMask == 0xFF);

                    // insert the bitcount at the lowest index of the bit references that were replaced
                    newMemoryAccessors.Insert(replaceIndex, new ModifiedMemoryAccessorExpression
                    {
                        MemoryAccessor = bitCountAccessor,
                        CombiningOperator = bitReference.CombiningOperator,
                    });
                }
            }

            // if nothing was collapsed, return the original expression
            if (newMemoryAccessors.Count == valueExpression._memoryAccessors.Count)
                return expression;

            if (canModifyRight && newMemoryAccessors.Count == 1)
            {
                var rightInteger = comparison.Right as IntegerConstantExpression;
                if (rightInteger != null)
                {
                    if (rightInteger.Value == 8)
                    {
                        // prefer byte(X) == 255 over bitcount(X) == 8
                        var byteAccessor = newMemoryAccessors[0].MemoryAccessor.ChangeFieldSize(FieldSize.Byte);
                        return new ComparisonExpression(byteAccessor, comparison.Operation, new IntegerConstantExpression(255));
                    }
                    else if (rightInteger.Value == 0)
                    {
                        // prefer byte(X) == 0 over bitcount(X) == 0
                        var byteAccessor = newMemoryAccessors[0].MemoryAccessor.ChangeFieldSize(FieldSize.Byte);
                        return new ComparisonExpression(byteAccessor, comparison.Operation, new IntegerConstantExpression(0));
                    }
                }
            }

            // create a new expression
            var newValueExpression = new MemoryValueExpression
            {
                Location = valueExpression.Location,
                IntegerConstant = valueExpression.IntegerConstant,
                FloatConstant = valueExpression.FloatConstant,
                _memoryAccessors = newMemoryAccessors
            };

            return new ComparisonExpression(newValueExpression, comparison.Operation, comparison.Right);
        }

        private ExpressionBase SwapSubtractionWithConstant(ExpressionBase constant, ComparisonOperation operation)
        {
            // must have at least one SubSource to proceed
            if (!HasSubtractedMemoryAccessor)
                return null;

            // drop the constant portion - it's already part of 'constant'
            var newLeft = ClearConstant();
            var newLeftValue = newLeft as MemoryValueExpression;
            if (newLeftValue == null || newLeftValue._memoryAccessors == null)
                return null;

            if (newLeftValue._memoryAccessors.All(a => a.CombiningOperator == RequirementType.SubSource))
            {
                // everything on both sides is negative, invert it all
                constant = new IntegerConstantExpression(0).Combine(constant, MathematicOperation.Subtract);

                // switch the SubSources to AddSources
                foreach (var memoryAccesor in newLeftValue._memoryAccessors)
                    memoryAccesor.CombiningOperator = RequirementType.AddSource;

                operation = ComparisonExpression.ReverseComparisonOperation(operation);
                return new ComparisonExpression(newLeft, operation, constant);
            }

            // find an unmodified SubSource to move to the right side
            var memoryAccessor = newLeftValue._memoryAccessors.LastOrDefault(IsSimpleSubtraction);
            if (memoryAccessor != null)
            {
                // found at least one subtraction on the left side, move it to the right and put the constant on the left
                newLeftValue._memoryAccessors.Remove(memoryAccessor);
                newLeftValue.ApplyMathematic(constant, MathematicOperation.Subtract);
                return new ComparisonExpression(newLeft, operation, memoryAccessor.MemoryAccessor);
            }

            // no unmodified SubSources, don't rearrange
            return null;
        }

        private static bool IsSimpleSubtraction(ModifiedMemoryAccessorExpression expr)
        {
            return expr.CombiningOperator == RequirementType.SubSource &&
                   expr.ModifyingOperator == RequirementOperator.None &&
                   !expr.MemoryAccessor.HasPointerChain;
        }

        private static bool IsSimpleAddition(ModifiedMemoryAccessorExpression expr)
        {
            return expr.CombiningOperator == RequirementType.AddSource &&
                   expr.ModifyingOperator == RequirementOperator.None &&
                   !expr.MemoryAccessor.HasPointerChain;
        }

        private static ExpressionBase CheckForUnderflow(ExpressionBase expression)
        {
            var comparison = expression as ComparisonExpression;
            if (comparison == null)
                return expression;

            var leftMemoryValue = comparison.Left as MemoryValueExpression;
            if (leftMemoryValue == null)
                return comparison;

            // direct comparisons aren't affected by underflow.
            if (comparison.Operation == ComparisonOperation.Equal ||
                comparison.Operation == ComparisonOperation.NotEqual)
            {
                return leftMemoryValue.EnsureSingleExpressionOnRightHandSide(comparison.Right, comparison.Operation) ?? comparison;
            }

            // if the right side is a positive constant, and the left side has one simple added and at least one subtracted
            // memory accessor, move all the subtracted memory accessors to the right side and reverse the comparison
            if (leftMemoryValue._memoryAccessors != null && leftMemoryValue._memoryAccessors.Count > 1 &&
                !leftMemoryValue.HasConstant && IsPositive(comparison.Right))
            {
                var subtractCount = leftMemoryValue._memoryAccessors.Count(a => a.CombiningOperator == RequirementType.SubSource);
                if (subtractCount == leftMemoryValue._memoryAccessors.Count - 1)
                {
                    var simpleAdd = leftMemoryValue._memoryAccessors.FirstOrDefault(IsSimpleAddition);
                    if (simpleAdd != null)
                    {
                        var newRight = simpleAdd.Clone();
                        newRight.CombiningOperator = RequirementType.None;

                        var newLeft = new MemoryValueExpression();
                        newLeft.ApplyMathematic(comparison.Right, MathematicOperation.Add);

                        foreach (var accessor in leftMemoryValue.MemoryAccessors)
                        {
                            if (accessor.CombiningOperator == RequirementType.SubSource)
                            {
                                // NOTE: ApplyMathematic ignores the existing CombiningOperator
                                newLeft.ApplyMathematic(accessor, MathematicOperation.Add);
                            }
                        } 

                        comparison = new ComparisonExpression(newLeft, ComparisonExpression.ReverseComparisonOperation(comparison.Operation), newRight);
                        leftMemoryValue = newLeft;
                    }
                }
            }

            // underflow will not occur if left side does not have at least one subtracted memory accessor
            if (!leftMemoryValue.HasSubtractedMemoryAccessor)
                return comparison;

            // if the result of subtracting two bytes is negative, it becomes a very large positive number.
            // so a check for less than some byte value may fail. Attempt to eliminate subtractions.
            bool checkForUnderflow = true;

            if (leftMemoryValue.MemoryAccessors.Any(a =>
                    a.MemoryAccessor.Field.Size == FieldSize.DWord && a.MemoryAccessor.Field.IsMemoryReference))
            {
                // if there's a 32-bit read, the value might not be signed. don't adjust it
                checkForUnderflow = false;
            }
            else if (leftMemoryValue.IntegerConstant > 0 && leftMemoryValue.HasSubtractedMemoryAccessor)
            {
                // if there's an explicit modification on the left hand side (of the original
                // equation) and it's less then the calcul-ated adjustment value, assume it's 
                // a user-provided adjustment and don't check for underflow
                //
                // NOTE: check the "this" object as the comparison obect may have been normalized with
                // a positive adjustment on the left.
                if (comparison.Operation == ComparisonOperation.GreaterThan ||
                    comparison.Operation == ComparisonOperation.GreaterThanOrEqual)
                {
                    // a negative result in signed math is a very large number.
                    // check for underflow even if the user provided their own adjustment.
                }
                else if (IsNegative(comparison.Right))
                {
                    // a negative result in signed math is a very large number.
                    // check for underflow even if the user provided their own adjustment.
                }
                else
                {
                    checkForUnderflow = false;

                    var underflowAdjustment = leftMemoryValue.GetUnderflowAdjustment(comparison.Right);
                    if (underflowAdjustment > 0)
                    {
                        if (leftMemoryValue.IntegerConstant > underflowAdjustment)
                        {
                            // adjustment is too much, clamp it down to the minimum required
                            checkForUnderflow = true;
                        }
                        else
                        {
                            // assume adjustwment was intentional
                            underflowAdjustment = leftMemoryValue.IntegerConstant - leftMemoryValue.IntegerConstant;
                            if (underflowAdjustment > 0)
                                return ApplyUnderflowAdjustment(comparison, underflowAdjustment);
                        }
                    }
                    else if (underflowAdjustment < 0)
                    {
                        // adjustment is too much, clamp it down to the minimum required
                        var result = ApplyUnderflowAdjustment(comparison, underflowAdjustment);
                        comparison = result as ComparisonExpression;
                        if (comparison != null)
                        {
                            if (IsZero(comparison.Right))
                            {
                                // adjusted comparison resulted with a zero on the right side, try to move a SubSource over
                                leftMemoryValue = (MemoryValueExpression)comparison.Left;
                                return leftMemoryValue.SwapSubtractionWithConstant(comparison.Right, comparison.Operation);
                            }
                        }

                        return result;
                    }
                }
            }
            else
            {
                // if there's only one non-complex subtraction on the left side and a constant on the right, swap them
                if (comparison.Right.Type == ExpressionType.IntegerConstant && 
                    leftMemoryValue._memoryAccessors.Count(IsSimpleSubtraction) == 1)
                {
                    var newLeft = leftMemoryValue.Clone();
                    var memoryAccessor = newLeft._memoryAccessors.FirstOrDefault(IsSimpleSubtraction);
                    newLeft._memoryAccessors.Remove(memoryAccessor);
                    memoryAccessor.CombiningOperator = RequirementType.None;

                    var collapsed = newLeft.ConvertToModifiedMemoryAccessor();
                    if (collapsed != null && collapsed.ModifyingOperator == RequirementOperator.None &&
                        !collapsed.MemoryAccessor.HasPointerChain)
                    { 
                        if (((IntegerConstantExpression)comparison.Right).Value > 0)
                        {
                            // A - B > N  ~>  B + N < A
                            // all that remains on the left is a memory accessor, move everything else and reverse the condition
                            var newRight = memoryAccessor.ApplyMathematic(comparison.Right, MathematicOperation.Add);
                            var operation = ComparisonExpression.ReverseComparisonOperation(comparison.Operation);
                            return new ComparisonExpression(newRight, operation, collapsed);
                        }
                        else
                        {
                            // A - B < -N  ~>  A + N < B
                            newLeft.ApplyMathematic(comparison.Right, MathematicOperation.Subtract);
                            return new ComparisonExpression(newLeft, comparison.Operation, memoryAccessor.MemoryAccessor);
                        }
                    }
                }

                // if there are any subtractions on the left side, we have to check for underflow
                checkForUnderflow = true;
            }

            if (checkForUnderflow)
            {
                var underflowAdjustment = leftMemoryValue.GetUnderflowAdjustment(comparison.Right);
                if (underflowAdjustment != 0)
                    return ApplyUnderflowAdjustment(comparison, underflowAdjustment);
            }
            else if (IsZero(comparison.Right))
            {
                return leftMemoryValue.SwapSubtractionWithConstant(comparison.Right, comparison.Operation);
            }

            return comparison;
        }

        private static ExpressionBase ApplyUnderflowAdjustment(ComparisonExpression comparison, int underflowAdjustment)
        {
            Debug.Assert(comparison.Left is MemoryValueExpression);
            var leftMemoryValue = (MemoryValueExpression)comparison.Left;

            var newLeft = leftMemoryValue.Clone();
            newLeft.IntegerConstant += underflowAdjustment;

            var operation = comparison.Operation;

            var mathematic = new MathematicExpression(comparison.Right, MathematicOperation.Add, new IntegerConstantExpression(underflowAdjustment));
            var newRight = mathematic.MergeOperands();

            var rightMemoryValue = newRight as MemoryValueExpression;
            if (rightMemoryValue != null)
            {
                if (rightMemoryValue.MemoryAccessors.Contains(leftMemoryValue._memoryAccessors[0]))
                {
                    // prefer rebalancing towards first expression in original statement
                    rightMemoryValue = newLeft;
                    newLeft = (MemoryValueExpression)newRight;
                    operation = ComparisonExpression.ReverseComparisonOperation(operation);
                }

                newRight = rightMemoryValue.ExtractConstant();
                rightMemoryValue.IntegerConstant = 0;
                rightMemoryValue.FloatConstant = 0.0;
                newLeft.ApplyMathematic(rightMemoryValue, MathematicOperation.Subtract);

                underflowAdjustment = newLeft.GetUnderflowAdjustment(newRight);
                if (underflowAdjustment > 0)
                    return ApplyUnderflowAdjustment(new ComparisonExpression(newLeft, operation, newRight), underflowAdjustment);
            }
            else if (underflowAdjustment < 0)
            {
                // attempting to clamp adjustment, see if we can clamp it even more
                var integerConstant = newRight as IntegerConstantExpression;
                if (integerConstant != null)
                {
                    var newConstant = integerConstant.Value - newLeft.IntegerConstant;
                    if (newConstant < 0)
                    {
                        // adjustment is greater than compare target.
                        // decrease to 0 then swap a subsource to the right side
                        return newLeft.SwapSubtractionWithConstant(new IntegerConstantExpression(newConstant), operation);
                    }
                }
            }

            return new ComparisonExpression(newLeft, operation, newRight);
        }

        private int GetUnderflowAdjustment(ExpressionBase right)
        {
            long min, max;
            GetMinMax(out min, out max);
            var underflowAdjustment = -(int)min;

            // adjust a comparison against a negative value up to 0.
            var integerOffset = 0;
            switch (right.Type)
            {
                case ExpressionType.IntegerConstant:
                    integerOffset = ((IntegerConstantExpression)right).Value;
                    break;

                case ExpressionType.MemoryAccessor:
                    var memoryValue = right as MemoryValueExpression;
                    if (memoryValue != null)
                        integerOffset = memoryValue.IntegerConstant;
                    break;
            }

            if (integerOffset < 0)
            {
                var negativeAdjustment = -integerOffset;
                if (underflowAdjustment < negativeAdjustment)
                    underflowAdjustment = negativeAdjustment;
            }

            return underflowAdjustment;
        }

        /// <summary>
        /// Gets the lowest and highest values that can be represented by this expression.
        /// </summary>
        public override void GetMinMax(out long min, out long max)
        {
            long totalMin = IntegerConstant;
            long totalMax = IntegerConstant;

            foreach (var memoryAccessor in MemoryAccessors)
            {
                long accessorMin, accessorMax;
                memoryAccessor.GetMinMax(out accessorMin, out accessorMax);

                if (memoryAccessor.CombiningOperator == RequirementType.AddSource)
                {
                    totalMin += accessorMin;
                    totalMax += accessorMax;
                }
                else
                {
                    totalMax -= accessorMin;
                    totalMin -= accessorMax;
                }
            }

            min = totalMin;
            max = totalMax;
        }

        private static ExpressionBase CheckForImpossibleValues(ExpressionBase expression)
        {
            var comparison = expression as ComparisonExpression;
            if (comparison == null)
                return expression;

            // a comparison against a negative value without any subtractions can never be true
            if (!IsNegative(comparison.Right))
                return expression;

            bool canBeTrue;

            var memoryValue = comparison.Left as MemoryValueExpression;
            if (memoryValue != null)
            {
                canBeTrue = memoryValue.HasSubtractedMemoryAccessor;
            }
            else
            {
                var modifiedMemoryAccessor = comparison.Left as ModifiedMemoryAccessorExpression;
                if (modifiedMemoryAccessor != null)
                {
                    canBeTrue = modifiedMemoryAccessor.ModifyingOperator == RequirementOperator.Multiply &&
                        (int)modifiedMemoryAccessor.Modifier.Value < 0;
                }
                else
                {
                    canBeTrue = false;
                }
            }

            if (!canBeTrue)
            {
                switch (comparison.Operation)
                {
                    case ComparisonOperation.Equal:
                    case ComparisonOperation.LessThan:
                    case ComparisonOperation.LessThanOrEqual:
                        return new ErrorExpression("Expression can never be true");

                    case ComparisonOperation.NotEqual:
                    case ComparisonOperation.GreaterThan:
                    case ComparisonOperation.GreaterThanOrEqual:
                        return new ErrorExpression("Expression is always true");
                }
            }

            return comparison;
        }

        public ModifiedMemoryAccessorExpression ConvertToModifiedMemoryAccessor()
        {
            if (IntegerConstant == 0 && FloatConstant == 0.0)
            {
                if (_memoryAccessors != null && _memoryAccessors.Count == 1 &&
                    _memoryAccessors[0].CombiningOperator == RequirementType.AddSource)
                {
                    var accessor = _memoryAccessors[0].Clone();
                    accessor.CombiningOperator = RequirementType.None;
                    return accessor;
                }
            }

            return null;
        }

        public ExpressionBase ClearConstant()
        {
            Debug.Assert(!IsReadOnly);

            if (_memoryAccessors == null)
                return new IntegerConstantExpression(0);

            if (_memoryAccessors.Count == 1 &&
                _memoryAccessors[0].CombiningOperator == RequirementType.AddSource)
            {
                if (_memoryAccessors[0].ModifyingOperator == RequirementOperator.None)
                    return _memoryAccessors[0].MemoryAccessor.Clone();

                var newLeft = _memoryAccessors[0].Clone();
                newLeft.CombiningOperator = RequirementType.None;
                return newLeft;
            }

            var clone = Clone();
            clone.IntegerConstant = 0;
            clone.FloatConstant = 0.0;
            return clone;
        }

        public ExpressionBase ExtractConstant()
        {
            if (FloatConstant != 0.0)
                return new FloatConstantExpression((float)(FloatConstant + IntegerConstant));

            return new IntegerConstantExpression(IntegerConstant);
        }

        private static bool IsZero(ExpressionBase expression)
        {
            var numericConstant = expression as INumericConstantExpression;
            return (numericConstant != null) ? numericConstant.IsZero : false;
        }

        private static bool IsNegative(ExpressionBase expression)
        {
            var numericConstant = expression as INumericConstantExpression;
            return (numericConstant != null) ? numericConstant.IsNegative : false;
        }

        private static bool IsPositive(ExpressionBase expression)
        {
            var numericConstant = expression as INumericConstantExpression;
            return (numericConstant != null) ? numericConstant.IsPositive : false;
        }

        private bool MemoryAccessorsMatch(MemoryValueExpression that)
        {
            if (_memoryAccessors == null || that._memoryAccessors == null)
                return (_memoryAccessors == null && that._memoryAccessors == null);

            if (_memoryAccessors.Count != that._memoryAccessors.Count)
                return false;

            for (int i = 0; i < _memoryAccessors.Count; i++)
            {
                if (_memoryAccessors[i] != that._memoryAccessors[i])
                    return false;
            }

            return true;
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as MemoryValueExpression;
            return (that != null && IntegerConstant == that.IntegerConstant && MemoryAccessorsMatch(that));
        }

        ExpressionBase ICloneableExpression.Clone()
        {
            return Clone();
        }

        public MemoryValueExpression Clone()
        {
            var clone = new MemoryValueExpression() { Location = this.Location };
            clone.IntegerConstant = IntegerConstant;
            clone.FloatConstant = FloatConstant;
            
            if (_memoryAccessors != null)
            {
                clone._memoryAccessors = new List<ModifiedMemoryAccessorExpression>();
                foreach (var memoryAccessor in _memoryAccessors)
                    clone._memoryAccessors.Add(memoryAccessor.Clone());
            }

            return clone;
        }

        internal override void AppendString(StringBuilder builder)
        {
            if (_memoryAccessors == null)
            {
                if (FloatConstant == 0.0)
                {
                    builder.Append(IntegerConstant);
                }
                else
                {
                    var floatConstant = new FloatConstantExpression((float)(FloatConstant + IntegerConstant));
                    floatConstant.AppendString(builder);
                }
                return;
            }

            bool first = true;
            foreach (var memoryAccessor in _memoryAccessors)
            {
                var memoryAccessorBuilder = new StringBuilder();
                memoryAccessor.AppendString(memoryAccessorBuilder);

                if (first)
                {
                    if (memoryAccessor.CombiningOperator == RequirementType.AddSource)
                        memoryAccessorBuilder.Remove(0, 2);
                    first = false;
                }
                else
                {
                    builder.Append(' ');
                }

                builder.Append(memoryAccessorBuilder);
            }

            if (FloatConstant != 0.0)
            {
                var floatConstant = FloatConstant + (float)IntegerConstant;
                if (floatConstant < 0)
                {
                    builder.Append(" - ");
                    floatConstant = -floatConstant;
                }
                else
                {
                    builder.Append(" + ");
                }

                new FloatConstantExpression((float)floatConstant).AppendString(builder);
            }
            else if (IntegerConstant != 0)
            {
                if (IntegerConstant < 0)
                {
                    builder.Append(" - ");
                    builder.Append(-IntegerConstant);
                }
                else
                {
                    builder.Append(" + ");
                    builder.Append(IntegerConstant);
                }
            }
        }

        public static bool HasFloat(ExpressionBase expression)
        {
            var memoryValue = expression as MemoryValueExpression;
            if (memoryValue != null)
                return memoryValue.MemoryAccessors.Any(a => HasFloat(a));

            var modifiedMemoryAccessor = expression as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null)
            {
                return modifiedMemoryAccessor.MemoryAccessor.Field.IsFloat ||
                    (modifiedMemoryAccessor.ModifyingOperator != RequirementOperator.None && modifiedMemoryAccessor.Modifier.IsFloat);
            }

            var memoryAccessor = expression as MemoryAccessorExpression;
            if (memoryAccessor != null)
                return memoryAccessor.Field.IsFloat;

            return false;
        }

        public ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            return BuildTrigger(context, null);
        }

        public ErrorExpression BuildTrigger(TriggerBuilderContext context, ExpressionBase comparison)
        {
            var memoryAccessors = new List<ModifiedMemoryAccessorExpression>();
            ModifiedMemoryAccessorExpression constantAccessor = null;

            if (FloatConstant != 0.0)
            {
                var value = (float)(FloatConstant + IntegerConstant);
                var field = new Field { Type = FieldType.Float, Size = FieldSize.Float, Float = Math.Abs(value) };
                constantAccessor = new ModifiedMemoryAccessorExpression(new MemoryAccessorExpression(field));
                constantAccessor.CombiningOperator = (value < 0.0) ? RequirementType.SubSource : RequirementType.AddSource;
            }
            else if (IntegerConstant != 0)
            {
                var field = new Field { Type = FieldType.Value, Size = FieldSize.DWord, Value = (uint)Math.Abs(IntegerConstant) };
                constantAccessor = new ModifiedMemoryAccessorExpression(new MemoryAccessorExpression(field));
                constantAccessor.CombiningOperator = (IntegerConstant < 0) ? RequirementType.SubSource : RequirementType.AddSource;
            }

            bool appendConstantAccessor = false;
            if (constantAccessor != null)
            {
                if (context is ValueBuilderContext && constantAccessor.CombiningOperator == RequirementType.AddSource)
                    appendConstantAccessor = true;
                else
                    memoryAccessors.Add(constantAccessor);
            }

            if (_memoryAccessors != null)
                memoryAccessors.AddRange(_memoryAccessors);

            if (appendConstantAccessor)
                memoryAccessors.Add(constantAccessor);

            if (context is not ValueBuilderContext)
            {
                // last item has to be an unmodified AddSource so we can compare it.
                // if a comparison is provided then the AddAddress chain must also match.
                var comparisonAccessor = (comparison != null) ? ReduceToSimpleExpression(comparison) as MemoryAccessorExpression : null;

                var lastIndex = memoryAccessors.Count - 1;
                for (; lastIndex >= 0; --lastIndex)
                {
                    var last = memoryAccessors[lastIndex];
                    if (last.CombiningOperator == RequirementType.SubSource)
                        continue;

                    if (last.ModifyingOperator != RequirementOperator.None)
                        continue;

                    if (comparisonAccessor != null && !comparisonAccessor.PointerChainMatches(last))
                        continue;

                    break;
                }

                if (lastIndex == -1)
                {
                    // no unmodified AddSource items, append a item with value 0
                    if (context is not ValueBuilderContext)
                        memoryAccessors.Add(new ModifiedMemoryAccessorExpression(new MemoryAccessorExpression(FieldType.Value, FieldSize.DWord, 0)));
                }
                else if (lastIndex != memoryAccessors.Count - 1)
                {
                    // move the last unmodified AddSource item to the end of the list so we can
                    var last = memoryAccessors[lastIndex];
                    memoryAccessors.RemoveAt(lastIndex);
                    memoryAccessors.Add(last);
                }
            }

            // output the accessor chain
            foreach (var accessor in memoryAccessors)
                accessor.BuildTrigger(context);

            if (context is not ValueBuilderContext)
            {
                // the last item will be flagged as an AddSource (or None if 0 was appended)
                // make sure it's None before leaving
                context.LastRequirement.Type = RequirementType.None;
            }

            return null;
        }

        public ErrorExpression Execute(InterpreterScope scope)
        {
            if (_memoryAccessors == null || _memoryAccessors.Count == 0)
                return null;

            // report error if this occurs outside a trigger clause
            return _memoryAccessors.First().Execute(scope);
        }

        #region INestedExpressions

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                if (_memoryAccessors != null)
                {
                    foreach (var accessor in _memoryAccessors)
                        yield return accessor;
                }
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
        }

        #endregion
    }
}
