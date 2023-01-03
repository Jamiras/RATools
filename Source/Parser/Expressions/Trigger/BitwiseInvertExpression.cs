using RATools.Data;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class BitwiseInvertExpression : MemoryAccessorExpression
    {
        public BitwiseInvertExpression(MemoryAccessorExpression accessor)
            : base(accessor)
        {
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as BitwiseInvertExpression;
            return (that != null && base.Equals(obj));
        }

        public override MemoryAccessorExpression Clone()
        {
            return new BitwiseInvertExpression(this);
        }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append('~');
            base.AppendString(builder);
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            var result = base.BuildTrigger(context);
            if (result == null)
            {
                if (context.LastRequirement.Left.Type != FieldType.MemoryAddress)
                    return new ErrorExpression("cannot apply multiple modifiers to memory accessor", this);

                context.LastRequirement.Left = context.LastRequirement.Left.ChangeType(FieldType.Invert);
            }

            return result;
        }
    }
}
