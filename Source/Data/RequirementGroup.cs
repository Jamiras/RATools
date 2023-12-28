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
            return Serialize(0.0, 4);
        }

        /// <summary>
        /// Creates a serialized string from the requirement group.
        /// </summary>
        /// <param name="minimumVersion">DLL version to target.</param>
        /// <param name="addressWidth">Number of hex characters to use for addresses.</param>
        /// <returns>Serialized requirement group string.</returns>
        public string Serialize(double minimumVersion = 0.0, int addressWidth = 6)
        {
            var builder = new StringBuilder();
            Serialize(builder, minimumVersion, addressWidth);
            return builder.ToString();
        }

        /// <summary>
        /// Creates a serialized string from the requirement group.
        /// </summary>
        /// <param name="minimumVersion">DLL version to target.</param>
        /// <param name="addressWidth">Number of hex characters to use for addresses.</param>
        /// <returns>Serialized requirement group string.</returns>
        public void Serialize(StringBuilder builder, double minimumVersion = 0.0, int addressWidth = 6)
        {
            if (minimumVersion == 0.0)
                minimumVersion = MinimumVersion();

            var enumerator = Requirements.GetEnumerator();
            if (enumerator.MoveNext())
            {
                do
                {
                    enumerator.Current.Serialize(builder, minimumVersion, addressWidth);

                    if (!enumerator.MoveNext())
                        break;

                    builder.Append('_');
                } while (true);
            }
        }

        public double MinimumVersion()
        {
            double minimumVersion = 0.0;
            foreach (var requirement in Requirements)
                minimumVersion = Math.Max(minimumVersion, requirement.MinimumVersion());

            return minimumVersion;
        }

        public bool IsMinimumVersionRequiredAtLeast(double minimumVersion)
        {
            foreach (var requirement in Requirements)
            {
                if (requirement.MinimumVersion() >= minimumVersion)
                    return true;
            }

            return false;
        }
    }
}
