using RATools.Data;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    /// <summary>
    /// Represents a memory read (with optional pointer chain).
    /// </summary>
    internal class MemoryAccessorExpression : MemoryAccessorExpressionBase,
        ITriggerExpression, IExecutableExpression,
        IMathematicCombineExpression, IMathematicCombineInverseExpression,
        IComparisonNormalizeExpression, ICloneableExpression
    {
        public MemoryAccessorExpression(FieldType type, FieldSize size, uint value)
            : this(new Field { Type = type, Size = size, Value = value })
        {
        }

        public MemoryAccessorExpression(Field field)
            : this()
        {
            Field = field;
        }

        public MemoryAccessorExpression()
            : base()
        {
        }

        public MemoryAccessorExpression(MemoryAccessorExpression source)
            : this()
        {
            Field = source.Field;
            Location = source.Location;

            _rememberPointer = source._rememberPointer;
            if (source._pointerChain != null)
                _pointerChain = new List<Requirement>(source._pointerChain);
        }

        public static MemoryAccessorExpression Extract(ExpressionBase expression)
        {
            var memoryAccessor = expression as MemoryAccessorExpression;
            if (memoryAccessor != null)
                return memoryAccessor;

            var modifiedMemoryAccessor = expression as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null)
                return modifiedMemoryAccessor.MemoryAccessor;

            return null;
        }

        /// <summary>
        /// Gets or sets the memory being read.
        /// </summary>
        public Field Field
        {
            get { return _field; }
            set
            {
                Debug.Assert(!IsReadOnly);
                _field = value;
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Field _field;

        /// <summary>
        /// Gets the chain of pointers leading to the element represented by <see cref="Field"/>.
        /// </summary>
        public IEnumerable<Requirement> PointerChain
        {
            get { return _pointerChain ?? Enumerable.Empty<Requirement>(); }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected List<Requirement> _pointerChain;

        /// <summary>
        /// Gets the remembered value to use as a pointer.
        /// </summary>
        public RememberRecallExpression RememberPointer
        {
            get { return _rememberPointer; }
            set
            {
                Debug.Assert(!IsReadOnly);
                _rememberPointer = value;
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected RememberRecallExpression _rememberPointer;

        /// <summary>
        /// Returns <c>true</c> if <see cref="PointerChain"/> is not empty.
        /// </summary>
        public bool HasPointerChain
        {
            get { return (_pointerChain != null && _pointerChain.Count > 0) || (RememberPointer != null); }
        }

        /// <summary>
        /// Adds a pointer to <see cref="PointerChain"/>.
        /// </summary>
        public void AddPointer(Requirement pointer)
        {
            Debug.Assert(!IsReadOnly);
            Debug.Assert(_rememberPointer == null);

            if (_pointerChain == null)
                _pointerChain = new List<Requirement>();

            Debug.Assert(pointer.Type == RequirementType.AddAddress || pointer.Type == RequirementType.Remember);
            _pointerChain.Add(pointer);
        }

        /// <summary>
        /// Clears <see cref="PointerChain"/>.
        /// </summary>
        public void ClearPointerChain()
        {
            Debug.Assert(!IsReadOnly);
            _pointerChain = null;
            _rememberPointer = null;
        }

        /// <summary>
        /// Determines if <see cref="PointerChain"/> matches <paramref name="that"/>.<see cref="PointerChain"/>.
        /// </summary>
        public bool PointerChainMatches(MemoryAccessorExpression that)
        {
            if (_rememberPointer != null || that._rememberPointer != null)
                return (_rememberPointer == that._rememberPointer);

            if (_pointerChain == null || that._pointerChain == null)
                return (_pointerChain == null && that._pointerChain == null);

            if (_pointerChain.Count != that._pointerChain.Count)
                return false;

            for (int i = 0; i < _pointerChain.Count; i++)
            {
                if (_pointerChain[i] != that._pointerChain[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines if <see cref="PointerChain"/> matches <paramref name="that"/>.<see cref="PointerChain"/>.
        /// </summary>
        public bool PointerChainMatches(MemoryAccessorExpressionBase that)
        {
            var memoryAccessor = that as MemoryAccessorExpression;
            if (memoryAccessor != null)
                return PointerChainMatches(memoryAccessor);

            var modifiedMemoryAccessor = that as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null)
                return PointerChainMatches(modifiedMemoryAccessor.MemoryAccessor);

            var memoryValue = that as MemoryValueExpression;
            if (memoryValue != null)
            {
                var memoryAcessorExpression = memoryValue.MemoryAccessors.Last();
                if (memoryAcessorExpression.ModifyingOperator != RequirementOperator.None)
                    return false;

                return PointerChainMatches(memoryAcessorExpression.MemoryAccessor);
            }

            return (_pointerChain == null || _pointerChain.Count == 0);
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as MemoryAccessorExpression;
            return (that != null && Field == that.Field && PointerChainMatches(that));
        }

        ExpressionBase ICloneableExpression.Clone()
        {
            return Clone();
        }

        /// <summary>
        /// Creates a clone of the expression.
        /// </summary>
        public virtual MemoryAccessorExpression Clone()
        {
            return new MemoryAccessorExpression(this);
        }

        /// <summary>
        /// Creates a clone of the expression with a different field size.
        /// </summary>
        /// <param name="newSize">The size for the clone.</param>
        /// <returns>The cloned expression with the new size.</returns>
        public MemoryAccessorExpression ChangeFieldSize(FieldSize newSize)
        {
            return new MemoryAccessorExpression(this)
            {
                Field = new Field
                {
                    Size = newSize,
                    Type = this.Field.Type,
                    Value = this.Field.Value,
                    Float = this.Field.Float,
                }
            };
        }

        internal override void AppendString(StringBuilder builder)
        {
            switch (Field.Type)
            {
                case FieldType.PreviousValue:
                    builder.Append("prev(");
                    break;

                case FieldType.PriorValue:
                    builder.Append("prior(");
                    break;

                case FieldType.Value:
                case FieldType.Float:
                    Field.AppendString(builder, NumberFormat.Decimal);
                    return;
            }

            builder.Append(Field.GetSizeFunction(Field.Size));
            builder.Append('(');
            if (_rememberPointer != null)
            {
                _rememberPointer.AppendString(builder);
            }
            else if (_pointerChain != null)
            {
                for (int i = _pointerChain.Count - 1; i >= 0; i--)
                {
                    if (_pointerChain[i].Operator == RequirementOperator.BitwiseAnd)
                        builder.Append('(');
                    builder.Append(Field.GetSizeFunction(_pointerChain[i].Left.Size));
                    builder.Append('(');
                }

                for (int i = 0; i < _pointerChain.Count; i++)
                {
                    if (i == 0)
                        builder.AppendFormat("0x{0:X6}", _pointerChain[i].Left.Value);
                    else if (_pointerChain[i].Left.Value != 0)
                        builder.Append(_pointerChain[i].Left.Value);
                    else
                        builder.Length -= 3;

                    builder.Append(')');

                    switch (_pointerChain[i].Operator)
                    {
                        case RequirementOperator.Multiply:
                            builder.Append(" * ");
                            _pointerChain[i].Right.AppendString(builder, NumberFormat.Decimal);
                            break;
                        case RequirementOperator.Divide:
                            builder.Append(" / ");
                            _pointerChain[i].Right.AppendString(builder, NumberFormat.Decimal);
                            break;
                        case RequirementOperator.Add:
                            builder.Append(" + ");
                            _pointerChain[i].Right.AppendString(builder, NumberFormat.Decimal);
                            break;
                        case RequirementOperator.Subtract:
                            builder.Append(" - ");
                            _pointerChain[i].Right.AppendString(builder, NumberFormat.Decimal);
                            break;
                        case RequirementOperator.Modulus:
                            builder.Append(" % ");
                            _pointerChain[i].Right.AppendString(builder, NumberFormat.Decimal);
                            break;
                        case RequirementOperator.BitwiseAnd:
                            builder.Append(" & ");
                            builder.AppendFormat("0x{0:X6}", _pointerChain[i].Right.Value);
                            builder.Append(')');
                            break;
                    }

                    builder.Append(" + ");
                }

                if (Field.Value != 0)
                    builder.Append(Field.Value);
                else
                    builder.Length -= 3;
            }
            else
            {
                builder.AppendFormat("0x{0:X6}", Field.Value);
            }

            builder.Append(')');

            switch (Field.Type)
            {
                case FieldType.PreviousValue:
                case FieldType.PriorValue:
                    builder.Append(')');
                    break;
            }
        }

        public virtual ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            if (_rememberPointer != null)
            {
                _rememberPointer.BuildTrigger(context);
                context.LastRequirement.Type = RequirementType.AddAddress;
            }
            else if (_pointerChain != null)
            {
                foreach (var pointer in _pointerChain)
                    context.Trigger.Add(pointer);
            }

            var requirement = new Requirement();
            requirement.Left = Field;
            context.Trigger.Add(requirement);
            return null;
        }

        public ErrorExpression Execute(InterpreterScope scope)
        {
            return new ErrorExpression(Field.GetSizeFunction(Field.Size) + " has no meaning outside of a trigger clause", this);
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
            var modifiedMemoryAccessorExpression = new ModifiedMemoryAccessorExpression(this);
            return modifiedMemoryAccessorExpression.ApplyMathematic(right, operation);
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
                    {
                        var modifiedMemoryAccessor = left as ModifiedMemoryAccessorExpression;
                        if (modifiedMemoryAccessor == null)
                        {
                            var memoryAccessor = left as MemoryAccessorExpression;
                            if (memoryAccessor != null)
                            {
                                modifiedMemoryAccessor = new ModifiedMemoryAccessorExpression(memoryAccessor);
                            }
                            else
                            {
                                modifiedMemoryAccessor = new ModifiedMemoryAccessorExpression(this);
                                return modifiedMemoryAccessor.ApplyMathematic(left, operation);
                            }
                        }
                        return modifiedMemoryAccessor.ApplyMathematic(this, operation);
                    }

                case MathematicOperation.Divide:
                    {
                        var modifiedMemoryAccessor = new ModifiedMemoryAccessorExpression()
                        {
                            MemoryAccessor = new MemoryAccessorExpression(FieldFactory.CreateField(left))
                        };
                        return modifiedMemoryAccessor.ApplyMathematic(this, operation);
                    }

                case MathematicOperation.Modulus:
                    return new ErrorExpression("Cannot modulus using a runtime value");
            }

            return null;
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
            if (!canModifyRight)
                return null;

            right = ReduceToSimpleExpression(right);

            bool swap = false;

            var memoryValue = right as MemoryValueExpression;
            if (memoryValue != null)
            {
                if (memoryValue.IntegerConstant >= 0 && memoryValue.FloatConstant >= 0)
                {
                    if (memoryValue.MemoryAccessors.All(a => a.CombiningOperator == RequirementType.AddSource))
                    {
                        // right side is all positive stuff, just invert everything
                        return new ComparisonExpression(right, ComparisonExpression.ReverseComparisonOperation(operation), this);
                    }
                }

                var newRight = memoryValue.ClearConstant();
                if (newRight is not MemoryValueExpression)
                {
                    // just move the constant
                    var newLeft = new MemoryValueExpression();
                    newLeft.ApplyMathematic(this, MathematicOperation.Add);
                    newLeft.ApplyMathematic(memoryValue.ExtractConstant(), MathematicOperation.Subtract);
                    return new ComparisonExpression(newLeft, operation, newRight);
                }

                swap = true;
            }

            var modifiedMemoryAccessor = right as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null)
            {
                if (modifiedMemoryAccessor.ModifyingOperator == RequirementOperator.None)
                {
                    // right side can be simplified to a MemoryAccessor, treat it as such.
                    right = modifiedMemoryAccessor.MemoryAccessor;
                }
                else
                {
                    swap = true;
                }
            }

            var memoryAccessor = right as MemoryAccessorExpression;
            if (memoryAccessor != null)
            {
                if (memoryAccessor.HasPointerChain)
                {
                    if (!HasPointerChain)
                    {
                        swap = true;
                    }
                    else if (!PointerChainMatches(memoryAccessor))
                    {
                        // both sides have pointers (that are not the same).
                        // move them both to the same side and compare to 0
                        memoryValue = new MemoryValueExpression();
                        memoryValue.ApplyMathematic(this, MathematicOperation.Add);
                        memoryValue.ApplyMathematic(memoryAccessor, MathematicOperation.Subtract);
                        return new ComparisonExpression(memoryValue, operation, new IntegerConstantExpression(0));
                    }
                }
            }

            var floatConstant = right as FloatConstantExpression;
            if (floatConstant != null && !Field.IsFloat)
                return ComparisonExpression.NormalizeFloatComparisonForInteger(this, operation, right);

            if (swap)
                return new ComparisonExpression(right, ComparisonExpression.ReverseComparisonOperation(operation), this);

            return null;
        }

        /// <summary>
        /// Gets the lowest and highest values that can be represented by this expression.
        /// </summary>
        public override void GetMinMax(out long min, out long max)
        {
            min = 0;
            max = Field.GetMaxValue(Field.Size);
        }
    }
}
