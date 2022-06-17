using RATools.Data;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class MemoryAccessorExpression : ExpressionBase, ITriggerExpression, IExecutableExpression
    {
        public MemoryAccessorExpression(FieldType type, FieldSize size, uint value)
            : this()
        {
            Field = new Field { Type = type, Size = size, Value = value };
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

        public void AddPointer(Requirement pointer)
        {
            if (_pointerChain == null)
                _pointerChain = new List<Requirement>();
            _pointerChain.Add(pointer);
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

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as MemoryAccessorExpression;
            return (that != null && Field == that.Field && PointerChainMatches(that));
        }

        public virtual MemoryAccessorExpression Clone()
        {
            return new MemoryAccessorExpression(this);
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
            }

            builder.Append(Field.GetSizeFunction(Field.Size));
            builder.Append('(');
            if (_pointerChain != null)
            {
                for (int i = _pointerChain.Count - 1; i >= 0; i--)
                {
                    builder.Append(Field.GetSizeFunction(_pointerChain[i].Left.Size));
                    builder.Append('(');
                }

                for (int i = 0; i < _pointerChain.Count; i++)
                {
                    builder.AppendFormat("0x{0:X6}", _pointerChain[i].Left.Value);

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
                            _pointerChain[i].Right.AppendString(builder, NumberFormat.Hexadecimal);
                            break;
                    }

                    builder.Append(") + ");
                }
                builder.Append(Field.Value);
            }
            else
            {
                // TODO: update unit tests to allow for hex addresses in validations
                // builder.AppendFormat("0x{0:X6}", Field.Value);
                builder.Append(Field.Value);
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
    }
}
