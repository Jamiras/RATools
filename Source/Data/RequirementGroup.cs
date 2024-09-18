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

        public override bool Equals(object obj)
        {
            var that = obj as RequirementGroup;
            if (that == null)
                return false;

            var thisEnumerator = Requirements.GetEnumerator();
            var thatEnumerator = that.Requirements.GetEnumerator();
            while (thisEnumerator.MoveNext())
            {
                if (!thatEnumerator.MoveNext())
                    return false;

                if (thisEnumerator.Current != thatEnumerator.Current)
                    return false;
            }

            if (thatEnumerator.MoveNext())
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Determines if two <see cref="RequirementGroup"/>s are equivalent.
        /// </summary>
        public static bool operator ==(RequirementGroup left, RequirementGroup right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left is null || right is null)
                return false;

            return left.Equals(right);
        }

        /// <summary>
        /// Determines if two <see cref="RequirementGroup"/>s are not equivalent.
        /// </summary>
        public static bool operator !=(RequirementGroup left, RequirementGroup right)
        {
            if (ReferenceEquals(left, right))
                return false;
            if (left is null || right is null)
                return true;

            return !left.Equals(right);
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

        public uint MaximumAddress()
        {
            uint maximumAddress = 0;
            foreach (var requirement in Requirements)
            {
                if (requirement.Left.IsMemoryReference)
                    maximumAddress = Math.Max(maximumAddress, requirement.Left.Value);
                if (requirement.Right.IsMemoryReference)
                    maximumAddress = Math.Max(maximumAddress, requirement.Right.Value);
            }

            return maximumAddress;
        }
    }
}
