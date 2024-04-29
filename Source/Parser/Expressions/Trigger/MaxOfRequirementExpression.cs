using RATools.Data;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class MaxOfRequirementExpression : RequirementExpressionBase
    {
        public MaxOfRequirementExpression()
        {
        }

        public IEnumerable<RequirementExpressionBase> Values
        {
            get { return _values ?? Enumerable.Empty<RequirementExpressionBase>(); }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected List<RequirementExpressionBase> _values;

        public void AddValue(RequirementExpressionBase condition)
        {
            if (_values == null)
                _values = new List<RequirementExpressionBase>();

            _values.Add(condition);
        }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("max_of(");

            if (_values != null)
            {
                foreach (var value in _values)
                {
                    value.AppendString(builder);
                    builder.Append(", ");
                }

                builder.Length -= 2; // remove last ", "
            }

            builder.Append(')');
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as MaxOfRequirementExpression;
            return (that != null && CompareRequirements(_values, that._values));
        }

        public Value ToValue(out ErrorExpression error)
        {
            var values = new List<IEnumerable<Requirement>>();

            if (_values != null)
            {
                foreach (var expression in _values)
                {
                    var value = ValueBuilder.BuildValue(expression, out error);
                    if (value == null)
                        return null;

                    values.Add(value.Values.First().Requirements);
                }
            }

            error = null;
            return new Value(values);
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context)
        {
            return new ErrorExpression("max_of cannot be used in logic");
        }
    }
}
