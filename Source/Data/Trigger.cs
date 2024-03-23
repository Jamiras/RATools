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
    public class Trigger
    {
        public Trigger()
            : this(Enumerable.Empty<Requirement>(), Enumerable.Empty<IEnumerable<Requirement>>())
        {
        }

        public Trigger(IEnumerable<Requirement> core)
            : this(core, Enumerable.Empty<IEnumerable<Requirement>>())
        {
        }

        public Trigger(IEnumerable<Requirement> core, IEnumerable<IEnumerable<Requirement>> alts)
        {
            Core = new RequirementGroup(core);
            
            if (alts.Any())
            {
                var altGroups = new List<RequirementGroup>();
                foreach (var alt in alts)
                    altGroups.Add(new RequirementGroup(alt));
                Alts = altGroups.ToArray();
            }
            else
            {
                Alts = Enumerable.Empty<RequirementGroup>();
            }
        }

        /// <summary>
        /// Gets the core requirements for the trigger.
        /// </summary>
        public RequirementGroup Core { get; private set; }

        /// <summary>
        /// Gets the alternate requirements for the trigger.
        /// </summary>
        public IEnumerable<RequirementGroup> Alts { get; private set; }

        /// <summary>
        /// Constructs a Trigger from a serialized trigger string.
        /// </summary>
        /// <param name="serialized">The serialized trigger string</param>
        /// <returns>Deserialized trigger or <c>null</c> on error</returns>
        public static Trigger Deserialize(string serialized)
        {
            return Deserialize(Tokenizer.CreateTokenizer(serialized));
        }

        /// <summary>
        /// Constructs a Trigger from a serialized trigger string.
        /// </summary>
        /// <param name="tokenizer">Tokenizer containing the serialized trigger string</param>
        /// <returns>Deserialized trigger or <c>null</c> on error</returns>
        public static Trigger Deserialize(Tokenizer tokenizer)
        {
            var trigger = new Trigger();

            var isCore = true;
            var current = new List<Requirement>();
            var alts = new List<RequirementGroup>();
            do
            {
                // immediate 'S' is an empty group
                if (tokenizer.NextChar != 'S')
                    current.Add(Requirement.Deserialize(tokenizer));

                // '_' indicates another requirement follows
                if (tokenizer.NextChar != '_')
                {
                    // end of group. append current to trigger
                    if (isCore)
                    {
                        trigger.Core = new RequirementGroup(current.ToArray());
                        current.Clear();
                        isCore = false;
                    }
                    else if (current.Count != 0)
                    {
                        alts.Add(new RequirementGroup(current.ToArray()));
                        current.Clear();
                    }

                    // 'S' starts a new group (alt)
                    if (tokenizer.NextChar != 'S')
                        break;
                }

                tokenizer.Advance();

            } while (true);

            // end of achievement, finalize
            if (alts.Count != 0)
                trigger.Alts = alts.ToArray();

            return trigger;
        }

        public override string ToString()
        {
            return Serialize(new SerializationContext { AddressWidth = 4 });
        }

        /// <summary>
        /// Creates a serialized string from the trigger.
        /// </summary>
        /// <returns>Serialized trigger string.</returns>
        public string Serialize(SerializationContext serializationContext)
        {
            if (serializationContext.MinimumVersion == Version.Uninitialized)
                serializationContext = serializationContext.WithVersion(MinimumVersion());

            var builder = new StringBuilder();

            if (Core.Requirements.Any())
                Core.Serialize(builder, serializationContext);
            else
                builder.Append("1=1"); // provide always_true core group for legacy parsers

            foreach (var alt in Alts)
            {
                builder.Append('S');
                alt.Serialize(builder, serializationContext);
            }

            return builder.ToString();
        }

        public SoftwareVersion MinimumVersion()
        {
            var minimumVersion = Core.MinimumVersion();
            foreach (var alt in Alts)
                minimumVersion = minimumVersion.OrNewer(alt.MinimumVersion());

            return minimumVersion;
        }

        public uint MaximumAddress()
        {
            var maximumAddress = Core.MaximumAddress();
            foreach (var alt in Alts)
                maximumAddress = Math.Max(maximumAddress, alt.MaximumAddress());

            return maximumAddress;
        }
    }
}
