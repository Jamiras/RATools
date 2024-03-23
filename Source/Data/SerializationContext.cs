using Jamiras.Components;
using System;

namespace RATools.Data
{
    public class SerializationContext
    {
        public SerializationContext()
        {
            MinimumVersion = Version.Uninitialized;
            AddressWidth = 6;
        }

        public SerializationContext WithVersion(SoftwareVersion newVersion)
        {
            return new SerializationContext
            {
                MinimumVersion = newVersion,
                AddressWidth = AddressWidth
            };
        }

        /// <summary>
        /// Gets or sets the minimum DLL version to target.
        /// </summary>
        public SoftwareVersion MinimumVersion { get; set; }

        /// <summary>
        /// Gets or sets the number of characters to use for addresses.
        /// </summary>
        public int AddressWidth { get; set; }

        /// <summary>
        /// Appends a padded address to the <paramref="builder"/>
        /// </summary>
        public string FormatAddress(uint address)
        {
            switch (AddressWidth)
            {
                case 2:
                    return String.Format("{0:x2}", address);
                case 4:
                    return String.Format("{0:x4}", address);
                default:
                    return String.Format("{0:x6}", address);
            }
        }

        public override string ToString()
        {
            return FormatAddress(0) + " (" + MinimumVersion + ")";
        }
    }
}
