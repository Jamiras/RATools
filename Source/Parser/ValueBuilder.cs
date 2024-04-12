using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser
{
    [DebuggerDisplay("Values:{_values.Count}")]
    public class ValueBuilder
    {
        public ValueBuilder()
        {
            _values = new List<ICollection<Requirement>>();
        }

        public ValueBuilder(Value source)
            : this()
        {
            var values = new List<ICollection<Requirement>>();
            foreach (var value in source.Values)
                values.Add(value.Requirements.ToArray());

            _values = values;
        }

        internal static bool IsConvertible(ExpressionBase expression)
        {
            return (expression is ITriggerExpression || expression is IntegerConstantExpression);
        }

        internal static ErrorExpression InconvertibleError(ExpressionBase expression)
        {
            return new ErrorExpression("Cannot create value from " + expression.Type.ToLowerString(), expression);
        }

        public static Value BuildValue(ExpressionBase expression, out ErrorExpression error)
        {
            var integerConstant = expression as IntegerConstantExpression;
            if (integerConstant != null)
            {
                error = null;

                var requirement = new Requirement
                {
                    Left = FieldFactory.CreateField(integerConstant.Value)
                };
                return new Value(new[] { new[] { requirement } });
            }

            var trigger = expression as ITriggerExpression;
            if (trigger == null)
            {
                error = InconvertibleError(expression);
                return null;
            }

            var requirements = new List<Requirement>();
            var context = new ValueBuilderContext { Trigger = requirements };
            error = trigger.BuildTrigger(context);
            if (error != null)
                return null;

            // ensure there's a Measured condition
            if (!requirements.Any(r => r.Type == RequirementType.Measured))
            {
                var measured = requirements.LastOrDefault(r => r.Type == RequirementType.None);
                if (measured != null)
                    measured.Type = RequirementType.Measured;
            }

            return new Value(new[] { requirements });
        }

        /// <summary>
        /// Gets the value collections.
        /// </summary>
        public ICollection<ICollection<Requirement>> Values
        {
            get { return _values; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly List<ICollection<Requirement>> _values;

        /// <summary>
        /// Constructs a <see cref="Value"/> from the current state of the builder.
        /// </summary>
        public Value ToValue()
        {
            var values = new Requirement[_values.Count][];
            for (int i = 0; i < _values.Count; i++)
                values[i] = _values[i].ToArray();

            return new Value(values);
        }

        /// <summary>
        /// Populates the values collection from a serialized requirement string.
        /// </summary>
        public void ParseValue(Tokenizer tokenizer)
        {
            var value = Value.Deserialize(tokenizer);
            foreach (var singleValue in value.Values)
                _values.Add(new List<Requirement>(singleValue.Requirements));
        }

        /// <summary>
        /// Creates a serialized requirements string from the core and alt groups.
        /// </summary>
        public string SerializeRequirements(SerializationContext serializationContext)
        {
            var value = new Value(_values);
            return value.Serialize(serializationContext);
        }


        /// <summary>
        /// Gets the requirements formatted as a human-readable string.
        /// </summary>
        internal string RequirementsDebugString
        {
            get { return ToScript(new ScriptBuilderContext()); }
        }

        /// <summary>
        /// Gets the value formatted as a human-readable string.
        /// </summary>
        public string ToScript(ScriptBuilderContext context)
        {
            var builder = new StringBuilder();

            if (Values.Count == 1)
            {
                AppendValue(builder, Values.First(), context);
            }
            else if (Values.Count > 0)
            {
                builder.Append("max_of(");
                foreach (var value in Values)
                    AppendValue(builder, value, context);
                builder.Append(')');
            }

            return builder.ToString();
        }

        private static void AppendValue(StringBuilder builder, IEnumerable<Requirement> value, ScriptBuilderContext context)
        {
            var requirementEx = RequirementEx.Combine(value);

            var enumerator = requirementEx.GetEnumerator();
            if (enumerator.MoveNext())
            {
                context.AppendRequirements(builder, enumerator.Current.Requirements);
                while (enumerator.MoveNext())
                {
                    builder.Append(" && ");
                    context.AppendRequirements(builder, enumerator.Current.Requirements);
                }
            }
        }
    }
}
