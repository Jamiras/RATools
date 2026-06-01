namespace RATools.Data
{
    /// <summary>
    /// Supported field types
    /// </summary>
    public enum FieldType
    {
        /// <summary>
        /// Unspecified.
        /// </summary>
        None = 0,

        /// <summary>
        /// The value at a memory address.
        /// </summary>
        MemoryAddress,

        /// <summary>
        /// An unsigned integer constant.
        /// </summary>
        Value,

        /// <summary>
        /// The previous value at a memory address.
        /// </summary>
        PreviousValue, // Delta

        /// <summary>
        /// The last differing value at a memory address.
        /// </summary>
        PriorValue, // Prior

        /// <summary>
        /// The current value at a memory address decoded from BCD.
        /// </summary>
        BinaryCodedDecimal,

        /// <summary>
        /// A floating point constant.
        /// </summary>
        Float,

        /// <summary>
        /// The bitwise inversion of the value at a memory address.
        /// </summary>
        Invert,

        /// <summary>
        /// The accumulator captured by a Remember condition.
        /// </summary>
        Recall,
    }

    public static class FieldTypeExtension
    {
        /// <summary>
        /// Gets whether or not the field references memory.
        /// </summary>
        public static bool IsMemoryReference(this FieldType type)
        {
            switch (type)
            {
                case FieldType.MemoryAddress:
                case FieldType.PreviousValue:
                case FieldType.PriorValue:
                case FieldType.BinaryCodedDecimal:
                case FieldType.Invert:
                case FieldType.Recall:
                    return true;

                default:
                    return false;
            }
        }
    }
}