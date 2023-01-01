﻿using RATools.Data;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class MemoryAccessorExpression : ExpressionBase, ITriggerExpression, IExecutableExpression, 
        IMathematicCombineExpression, IMathematicCombineInverseExpression,
        IComparisonNormalizeExpression, IUpconvertibleExpression, ICloneableExpression
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
            : base(ExpressionType.MemoryAccessor)
        {
        }

        public MemoryAccessorExpression(MemoryAccessorExpression source)
            : this()
        {
            Field = source.Field.Clone();
            Location = source.Location;

            if (source._pointerChain != null)
            {
                _pointerChain = new List<Requirement>(source._pointerChain.Count);
                foreach (var pointer in source._pointerChain)
                    _pointerChain.Add(pointer.Clone());
            }
        }

        public Field Field { get; set; }

        public IEnumerable<Requirement> PointerChain
        {
            get { return _pointerChain ?? Enumerable.Empty<Requirement>(); }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected List<Requirement> _pointerChain;

        public bool HasPointerChain
        {
            get { return _pointerChain != null && _pointerChain.Count > 0; }
        }

        public void AddPointer(Requirement pointer)
        {
            if (_pointerChain == null)
                _pointerChain = new List<Requirement>();

            Debug.Assert(pointer.Type == RequirementType.AddAddress);
            _pointerChain.Add(pointer);
        }

        public void ClearPointerChain()
        {
            _pointerChain = null;
        }

        public bool PointerChainMatches(MemoryAccessorExpression that)
        {
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

        public bool PointerChainMatches(ExpressionBase that)
        {
            var memoryAccessor = that as MemoryAccessorExpression;
            if (memoryAccessor != null)
                return PointerChainMatches(memoryAccessor);

            var modifiedMemoryAccessor = that as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null)
                return PointerChainMatches(modifiedMemoryAccessor.MemoryAccessor);

            var memoryValue = that as MemoryValueExpression;
            if (memoryValue != null)
                return PointerChainMatches(memoryValue.MemoryAccessors.Last().MemoryAccessor);

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
            if (_pointerChain != null)
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
            if (_pointerChain != null)
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

            var simplified = MemoryValueExpression.ReduceToSimpleExpression(right);
            if (simplified != null)
                right = simplified;

            bool swap = false;

            switch (right.Type)
            {
                case ExpressionType.MemoryValue:
                {
                    var memoryValue = (MemoryValueExpression)right;
                    if (memoryValue.IntegerConstant >= 0 && memoryValue.FloatConstant >= 0)
                    { 
                        if (memoryValue.MemoryAccessors.All(a => a.CombiningOperator == RequirementType.AddSource))
                        {
                            // right side is all positive stuff, just invert everything
                            return new ComparisonExpression(right, ComparisonExpression.ReverseComparisonOperation(operation), this);
                        }
                    }

                    var modifiedMemoryAccessor = memoryValue.ConvertToModifiedMemoryAccessor();
                    if (modifiedMemoryAccessor != null)
                    {
                        // right side can be simplified to a ModifierMemoryAccessor, treat it as such.
                        right = modifiedMemoryAccessor;
                        goto case ExpressionType.ModifiedMemoryAccessor;
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
                    break;
                }

                case ExpressionType.ModifiedMemoryAccessor:
                {
                    var modifiedMemoryAccessor = (ModifiedMemoryAccessorExpression)right;
                    if (modifiedMemoryAccessor.ModifyingOperator == RequirementOperator.None)
                    {
                        right = modifiedMemoryAccessor.MemoryAccessor;
                        goto case ExpressionType.MemoryAccessor;
                    }

                    swap = true;
                    break;
                }

                case ExpressionType.MemoryAccessor:
                {
                    var memoryAccessor = (MemoryAccessorExpression)right;
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
                            var memoryValue = new MemoryValueExpression();
                            memoryValue.ApplyMathematic(this, MathematicOperation.Add);
                            memoryValue.ApplyMathematic(memoryAccessor, MathematicOperation.Subtract);
                            return new ComparisonExpression(memoryValue, operation, new IntegerConstantExpression(0));
                        }
                    }
                    break;
                }

                case ExpressionType.FloatConstant:
                    if (!Field.IsFloat)
                        return ComparisonExpression.NormalizeFloatComparisonForInteger(this, operation, right);
                    break;
            }

            if (swap)
                return new ComparisonExpression(right, ComparisonExpression.ReverseComparisonOperation(operation), this);

            return null;
        }

        public void GetMinMax(out long min, out long max)
        {
            min = 0;
            max = Field.GetMaxValue(Field.Size);
        }

        /// <summary>
        /// Attempts to create a new expression from the current expression without loss of data.
        /// </summary>
        /// <param name="newType">The type of express to try to convert to.</param>
        /// <returns>
        /// A new expression of the requested type, or <c>null</c> if the conversion could not be performed.
        /// </returns>
        public ExpressionBase UpconvertTo(ExpressionType newType)
        {
            switch (newType)
            {
                case ExpressionType.ModifiedMemoryAccessor:
                    return new ModifiedMemoryAccessorExpression(this);

                case ExpressionType.MemoryValue:
                    var clause = new MemoryValueExpression();
                    return clause.ApplyMathematic(this, MathematicOperation.Add);

                default:
                    return null;
            }
        }
    }
}
