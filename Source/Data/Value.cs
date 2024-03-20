using Jamiras.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Data
{
    /// <summary>
    /// Defines a trigger
    /// </summary>
    public class Value
    {
        public Value()
        {
            Values = Enumerable.Empty<RequirementGroup>();
        }

        public Value(IEnumerable<IEnumerable<Requirement>> values)
        {
            var list = new List<RequirementGroup>();
            foreach (var value in values)
                list.Add(new RequirementGroup(value));

            Values = list;
        }

        /// <summary>
        /// Gets the possible value definitions.
        /// </summary>
        public IEnumerable<RequirementGroup> Values { get; private set; }

        /// <summary>
        /// Constructs a Value from a serialized value string.
        /// </summary>
        /// <param name="serialized">The serialized value string</param>
        /// <returns>Deserialized value or <c>null</c> on error</returns>
        public static Value Deserialize(string serialized)
        {
            return Deserialize(Tokenizer.CreateTokenizer(serialized));
        }

        /// <summary>
        /// Constructs a Value from a serialized value string.
        /// </summary>
        /// <param name="tokenizer">Tokenizer containing the serialized value string</param>
        /// <returns>Deserialized value or <c>null</c> on error</returns>
        public static Value Deserialize(Tokenizer tokenizer)
        {
            tokenizer.PushState();
            tokenizer.Advance();
            var secondCharacter = tokenizer.NextChar;
            tokenizer.PopState();

            if (secondCharacter != ':')
                return DeserializeLegacy(tokenizer);

            var value = new Value();

            var current = new List<Requirement>();
            var values = new List<RequirementGroup>();
            do
            {
                // immediate '$' is an empty value
                if (tokenizer.NextChar != '$')
                    current.Add(Requirement.Deserialize(tokenizer));

                // '_' indicates another requirement follows
                if (tokenizer.NextChar != '_')
                {
                    if (current.Count != 0)
                    {
                        values.Add(new RequirementGroup(current.ToArray()));
                        current.Clear();
                    }

                    // '$' starts a new value
                    if (tokenizer.NextChar != '$')
                        break;
                }

                tokenizer.Advance();

            } while (true);

            // end of achievement, finalize
            if (values.Count != 0)
                value.Values = values.ToArray();

            return value;
        }

        public static Value DeserializeLegacy(Tokenizer tokenizer)
        {
            var value = new Value();

            var current = new List<Requirement>();
            var values = new List<RequirementGroup>();
            do
            {
                current.Add(DeserializeLegacyRequirement(tokenizer));

                if (tokenizer.NextChar != '_')
                {
                    var lastAddSource = current.Last();
                    if (lastAddSource.Type != RequirementType.AddSource)
                    {
                        lastAddSource = current.Last(r => r.Type == RequirementType.AddSource);
                        current.Remove(lastAddSource);
                        current.Add(lastAddSource);
                    }
                    lastAddSource.Type = RequirementType.None;

                    values.Add(new RequirementGroup(current.ToArray()));

                    if (tokenizer.NextChar != '$')
                        break;

                    current.Clear();
                }

                tokenizer.Advance();

            } while (true);

            // end of achievement, finalize
            if (values.Count != 0)
                value.Values = values.ToArray();

            return value;
        }

        private static Requirement DeserializeLegacyRequirement(Tokenizer tokenizer)
        {
            var requirement = new Requirement();
            requirement.Type = RequirementType.AddSource;

            requirement.Left = Field.Deserialize(tokenizer);

            requirement.Operator = Requirement.ReadOperator(tokenizer);
            if (requirement.Operator == RequirementOperator.None)
            {
                if (requirement.Left.Type == FieldType.Value &&
                    (requirement.Left.Value & 0x80000000) != 0)
                {
                    requirement.Type = RequirementType.SubSource;
                    requirement.Left = new Field
                    {
                        Type = FieldType.Value,
                        Value = (uint)(-((int)requirement.Left.Value))
                    };
                }
            }
            else
            {
                requirement.Right = Field.Deserialize(tokenizer);

                if (requirement.IsComparison)
                {
                    requirement.Operator = RequirementOperator.None;
                }
                else if (requirement.Right.Type == FieldType.Value &&
                    (requirement.Right.Value & 0x80000000) != 0)
                {
                    requirement.Type = RequirementType.SubSource;
                    if (requirement.Right.Value == 0xFFFFFFFF)
                    {
                        requirement.Operator = RequirementOperator.None;
                        requirement.Right = new Field { Type = FieldType.Value, Value = 1 };
                    }
                    else
                    {
                        requirement.Right = new Field
                        {
                            Type = FieldType.Value,
                            Value = (uint)(-(int)requirement.Right.Value)
                        };
                    }
                }
            }

            return requirement;
        }

        public override string ToString()
        {
            return Serialize(new SerializationContext { AddressWidth = 4 });
        }

        /// <summary>
        /// Creates a serialized string from the value.
        /// </summary>
        /// <returns>Serialized value string.</returns>
        public string Serialize(SerializationContext serializationContext)
        {
            var builder = new StringBuilder();

            var enumerator = Values.GetEnumerator();
            if (enumerator.MoveNext())
            {
                do
                {
                    if (serializationContext.MinimumVersion < Version._0_77) // Measured leaderboard format
                        SerializeLegacyRequirements(enumerator.Current.Requirements, builder, serializationContext);
                    else if (enumerator.Current.Requirements.Any(r => r.Type == RequirementType.None)) // raw value
                        SerializeLegacyRequirements(enumerator.Current.Requirements, builder, serializationContext);
                    else
                        enumerator.Current.Serialize(builder, serializationContext);

                    if (!enumerator.MoveNext())
                        break;

                    builder.Append('$');
                } while (true);
            }

            return builder.ToString();
        }

        private static void SerializeLegacyRequirements(IEnumerable<Requirement> requirements, StringBuilder builder, SerializationContext serializationContext)
        {
            var enumerator = requirements.GetEnumerator();
            if (enumerator.MoveNext())
            {
                bool first = true;
                int constant = 0;
                do
                {
                    if (enumerator.Current.Left.IsMemoryReference)
                    {
                        if (first)
                            first = false;
                        else
                            builder.Append('_');

                        enumerator.Current.Left.Serialize(builder, serializationContext);

                        double multiplier = 1.0;
                        if (enumerator.Current.Type == RequirementType.SubSource)
                            multiplier = -1.0;

                        if (enumerator.Current.Operator == RequirementOperator.Multiply)
                        {
                            if (enumerator.Current.Right.IsFloat)
                                multiplier *= enumerator.Current.Right.Float;
                            else
                                multiplier *= enumerator.Current.Right.Value;
                        }
                        else if (enumerator.Current.Operator == RequirementOperator.Divide)
                        {
                            if (enumerator.Current.Right.IsFloat)
                                multiplier /= enumerator.Current.Right.Float;
                            else
                                multiplier /= enumerator.Current.Right.Value;
                        }

                        if (multiplier != 1.0)
                        {
                            if (enumerator.Current.Operator == RequirementOperator.Divide)
                            {
                                builder.Append('/');
                                builder.Append(1.0 / multiplier);
                            }
                            else
                            {
                                builder.Append('*');
                                builder.Append(multiplier);
                            }
                        }
                    }
                    else
                    {
                        if (enumerator.Current.Type == RequirementType.SubSource)
                            constant -= (int)enumerator.Current.Left.Value;
                        else
                            constant += (int)enumerator.Current.Left.Value;
                    }
                } while (enumerator.MoveNext());

                if (constant != 0)
                {
                    if (!first)
                        builder.Append('_');
                    builder.Append('v');
                    if (constant < 0)
                    {
                        constant = -constant;
                        builder.Append('-');
                    }
                    builder.Append(constant);
                }
            }
        }

        public SoftwareVersion MinimumVersion()
        {
            SoftwareVersion minimumVersion = Version.MinimumVersion;

            foreach (var value in Values)
            {
                foreach (var requirement in value.Requirements)
                {
                    Requirement clone = null;

                    if (requirement.Operator == RequirementOperator.Multiply ||
                        requirement.Operator == RequirementOperator.Divide)
                    {
                        // Multiply/Divide in trigger logic requires 0.78, but can be used with constants
                        // in legacy value expressions long before that.
                        if (!requirement.Right.IsMemoryReference)
                        {
                            clone = requirement.Clone();
                            clone.Operator = RequirementOperator.None;

                            // float support in trigger logic requires 1.0
                            if (clone.Right.Type == FieldType.Float)
                                clone.Right = new Field { Type = FieldType.Value, Value = 1 };
                        }
                    }

                    if (requirement.Type == RequirementType.Measured)
                    {
                        // Measured in trigger logic requires 0.77, but we can construct a legacy expression
                        // without it.
                        if (clone == null)
                            clone = requirement.Clone();
                        clone.Type = RequirementType.None;
                    }
                    else if (requirement.Type == RequirementType.AddHits)
                    {
                        // AddHits is supported pre-0.77, but cannot be used in value logic without a Measured flag.
                        minimumVersion = minimumVersion.OrNewer(Version._0_77);
                    }

                    if (clone != null)
                        minimumVersion = minimumVersion.OrNewer(clone.MinimumVersion());
                    else
                        minimumVersion = minimumVersion.OrNewer(requirement.MinimumVersion());
                }
            }

            return minimumVersion;
        }

        public uint MaximumAddress()
        {
            uint maximumAddress = 0;
            foreach (var value in Values)
                maximumAddress = Math.Max(maximumAddress, value.MaximumAddress());

            return maximumAddress;
        }
    }
}
