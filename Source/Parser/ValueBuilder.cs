using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static System.Formats.Asn1.AsnWriter;

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
        }

        /// <summary>
        /// Gets the value collections.
        /// </summary>
        public ICollection<ICollection<Requirement>> Values
        {
            get { return _values; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<ICollection<Requirement>> _values;

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
