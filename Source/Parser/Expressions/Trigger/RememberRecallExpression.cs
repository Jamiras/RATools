using RATools.Data;
using RATools.Parser.Internal;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    /// <summary>
    /// Captures a <see cref="MemoryAccessor"/> to use for further modifications.
    /// </summary>
    internal class RememberRecallExpression : MemoryAccessorExpression,
        ITriggerExpression, ICloneableExpression
    {
        public RememberRecallExpression()
            : base(FieldType.Recall, FieldSize.DWord, 0)
        {
        }

        public RememberRecallExpression(ModifiedMemoryAccessorExpression source)
            : this(new MemoryValueExpression(source))
        {
        }

        public RememberRecallExpression(MemoryValueExpression source)
            : this()
        {
            _rememberedValue = source;
            Location = source.Location;
        }

        public RememberRecallExpression(RememberRecallExpression source)
            : this()
        {
            _rememberedValue = source._rememberedValue;
            Location = source.Location;
        }

        /// <summary>
        /// Gets or sets the <see cref="MemoryAccessor"/> that is to be further modified.
        /// </summary>
        public MemoryValueExpression RememberedValue
        {
            get { return _rememberedValue; }
            set
            {
                Debug.Assert(!IsReadOnly);
                _rememberedValue = value;
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MemoryValueExpression _rememberedValue;

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as RememberRecallExpression;
            return (that != null && RememberedValue == that.RememberedValue);
        }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("remembered(");
            RememberedValue.AppendString(builder);
            builder.Append(')');
        }

        ExpressionBase ICloneableExpression.Clone()
        {
            return Clone();
        }

        /// <summary>
        /// Creates a clone of the expression.
        /// </summary>
        public new RememberRecallExpression Clone()
        {
            return new RememberRecallExpression(this);
        }

        /// <summary>
        /// Gets the lowest and highest values that can be represented by this expression.
        /// </summary>
        public override void GetMinMax(out long min, out long max)
        {
            RememberedValue.GetMinMax(out min, out max);
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            RememberedValue.BuildTrigger(context);

            // if the RememberedValue only contains ModifiedMemoryReferences, BuildTrigger
            // will append an extra condition so the result can be compared to.
            var lastRequirement = context.LastRequirement;
            if (context.Trigger.Count >= 2)
            {
                if (lastRequirement.Left.Type == FieldType.Value && lastRequirement.Left.Value == 0)
                {
                    var secondToLastRequirement = context.Trigger.ElementAt(context.Trigger.Count - 2);
                    if (secondToLastRequirement.Type == RequirementType.AddSource)
                    {
                        secondToLastRequirement.Type = RequirementType.None;
                        context.Trigger.Remove(lastRequirement);
                        lastRequirement = secondToLastRequirement;
                    }
                }
            }

            if (lastRequirement.Type != RequirementType.None)
                return new ErrorExpression("Cannot remember requirement", RememberedValue);
            lastRequirement.Type = RequirementType.Remember;

            return base.BuildTrigger(context);
        }
    }
}
