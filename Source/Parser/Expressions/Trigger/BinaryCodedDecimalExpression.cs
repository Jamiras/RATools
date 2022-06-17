using RATools.Data;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class BinaryCodedDecimalExpression : MemoryAccessorExpression
    {
        public BinaryCodedDecimalExpression(MemoryAccessorExpression accessor)
            : base(accessor)
        {
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as BinaryCodedDecimalExpression;
            return (that != null && base.Equals(obj));
        }

        public override MemoryAccessorExpression Clone()
        {
            return new BinaryCodedDecimalExpression(this);
        }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("bcd(");
            base.AppendString(builder);
            builder.Append(')');
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            var result = base.BuildTrigger(context);
            if (result == null)
            {
                if (context.LastRequirement.Left.Type != FieldType.MemoryAddress)
                    return new ErrorExpression("cannot apply multiple modifiers to memory accessor", this);

                context.LastRequirement.Left = context.LastRequirement.Left.ChangeType(FieldType.BinaryCodedDecimal);
            }

            return result;
        }
    }
}
