using Jamiras.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Data
{
    public class RequirementGroup
    {
        public RequirementGroup()
            : this(Enumerable.Empty<Requirement>())
        {
        }

        public RequirementGroup(IEnumerable<Requirement> requirements)
        {
            Requirements = requirements;
        }

        public IEnumerable<Requirement> Requirements { get; private set; }

        public override string ToString()
        {
            return Serialize(new SerializationContext { AddressWidth = 4 });
        }

        /// <summary>
        /// Creates a serialized string from the requirement group.
        /// </summary>
        /// <returns>Serialized requirement group string.</returns>
        public string Serialize(SerializationContext serializationContext)
        {
            var builder = new StringBuilder();
            Serialize(builder, serializationContext);
            return builder.ToString();
        }

        /// <summary>
        /// Creates a serialized string from the requirement group.
        /// </summary>
        /// <returns>Serialized requirement group string.</returns>
        public void Serialize(StringBuilder builder, SerializationContext serializationContext)
        {
            if (serializationContext.MinimumVersion == Version.Uninitialized)
                serializationContext = serializationContext.WithVersion(MinimumVersion());

            var enumerator = Requirements.GetEnumerator();
            if (enumerator.MoveNext())
            {
                do
                {
                    enumerator.Current.Serialize(builder, serializationContext);

                    if (!enumerator.MoveNext())
                        break;

                    builder.Append('_');
                } while (true);
            }
        }

        public SoftwareVersion MinimumVersion()
        {
            var minimumVersion = Version.MinimumVersion;
            foreach (var requirement in Requirements)
                minimumVersion = minimumVersion.OrNewer(requirement.MinimumVersion());

            return minimumVersion;
        }
    }
}
