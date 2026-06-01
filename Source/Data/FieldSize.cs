namespace RATools.Data
{
    /// <summary>
    /// The amount of data to pull from the related field.
    /// </summary>
    public enum FieldSize
    {
        /// <summary>
        /// Unspecified
        /// </summary>
        None = 0,

        /// <summary>
        /// Bit 0 of a byte.
        /// </summary>
        Bit0,

        /// <summary>
        /// Bit 1 of a byte.
        /// </summary>
        Bit1,

        /// <summary>
        /// Bit 2 of a byte.
        /// </summary>
        Bit2,

        /// <summary>
        /// Bit 3 of a byte.
        /// </summary>
        Bit3,

        /// <summary>
        /// Bit 4 of a byte.
        /// </summary>
        Bit4,

        /// <summary>
        /// Bit 5 of a byte.
        /// </summary>
        Bit5,

        /// <summary>
        /// Bit 6 of a byte.
        /// </summary>
        Bit6,

        /// <summary>
        /// Bit 7 of a byte.
        /// </summary>
        Bit7,

        /// <summary>
        /// Bits 0-3 of a byte.
        /// </summary>
        LowNibble,

        /// <summary>
        /// Bits 4-7 of a byte.
        /// </summary>
        HighNibble,

        /// <summary>
        /// A byte (8-bits).
        /// </summary>
        Byte,

        /// <summary>
        /// Two bytes (16-bit). Read from memory in little-endian mode.
        /// </summary>
        Word,

        /// <summary>
        /// Three bytes (24-bit). Read from memory in little-endian mode.
        /// </summary>
        TByte,

        /// <summary>
        /// Four bytes (32-bit). Read from memory in little-endian mode.
        /// </summary>
        DWord,

        /// <summary>
        /// The number of bits set in a byte.
        /// </summary>
        BitCount,

        /// <summary>
        /// Two bytes (16-bit). Read from memory in big-endian mode.
        /// </summary>
        BigEndianWord,

        /// <summary>
        /// Three bytes (24-bit). Read from memory in big-endian mode.
        /// </summary>
        BigEndianTByte,

        /// <summary>
        /// Four bytes (32-bit). Read from memory in big-endian mode.
        /// </summary>
        BigEndianDWord,

        /// <summary>
        /// 32-bit IEE-754 floating point number.
        /// </summary>
        Float,

        /// <summary>
        /// 32-bit Microsoft Binary Format floating point number.
        /// </summary>
        MBF32,

        /// <summary>
        /// 32-bit Microsoft Binary Format floating point number in little-endian mode.
        /// </summary>
        LittleEndianMBF32,

        /// <summary>
        /// 32-bit IEE-754 floating point number in big-endian mode
        /// </summary>
        BigEndianFloat,

        /// <summary>
        /// Most significant 32-bits of an IEE-754 double number (64-bit float).
        /// </summary>
        Double32,

        /// <summary>
        /// Most significant 32-bits of an IEE-754 double number (64-bit float) in big endian mode.
        /// </summary>
        BigEndianDouble32,

        /// <summary>
        /// Virtual size indicating a value takes an arbitrary number of bytes
        /// </summary>
        Array,
    }

    public static class FieldSizeExtension
    {
        /// <summary>
        /// Gets the RATools function used to read memory of the specified size.
        /// </summary>
        public static string GetSizeFunction(this FieldSize size)
        {
            switch (size)
            {
                case FieldSize.Bit0: return "bit0";
                case FieldSize.Bit1: return "bit1";
                case FieldSize.Bit2: return "bit2";
                case FieldSize.Bit3: return "bit3";
                case FieldSize.Bit4: return "bit4";
                case FieldSize.Bit5: return "bit5";
                case FieldSize.Bit6: return "bit6";
                case FieldSize.Bit7: return "bit7";
                case FieldSize.LowNibble: return "low4";
                case FieldSize.HighNibble: return "high4";
                case FieldSize.Byte: return "byte";
                case FieldSize.Word: return "word";
                case FieldSize.TByte: return "tbyte";
                case FieldSize.DWord: return "dword";
                case FieldSize.BitCount: return "bitcount";
                case FieldSize.BigEndianWord: return "word_be";
                case FieldSize.BigEndianTByte: return "tbyte_be";
                case FieldSize.BigEndianDWord: return "dword_be";
                case FieldSize.Float: return "float";
                case FieldSize.MBF32: return "mbf32";
                case FieldSize.LittleEndianMBF32: return "mbf32_le";
                case FieldSize.BigEndianFloat: return "float_be";
                case FieldSize.Double32: return "double32";
                case FieldSize.BigEndianDouble32: return "double32_be";
                default: return size.ToString();
            }
        }


        /// <summary>
        /// Gets the maximum value representable by the specified size.
        /// </summary>
        public static uint GetMaxValue(this FieldSize size)
        {
            switch (size)
            {
                case FieldSize.Bit0:
                case FieldSize.Bit1:
                case FieldSize.Bit2:
                case FieldSize.Bit3:
                case FieldSize.Bit4:
                case FieldSize.Bit5:
                case FieldSize.Bit6:
                case FieldSize.Bit7:
                    return 1;

                case FieldSize.BitCount:
                    return 8;

                case FieldSize.LowNibble:
                case FieldSize.HighNibble:
                    return 0x0F;

                case FieldSize.Byte:
                    return 0xFF;

                case FieldSize.Word:
                case FieldSize.BigEndianWord:
                    return 0xFFFF;

                case FieldSize.TByte:
                case FieldSize.BigEndianTByte:
                    return 0xFFFFFF;

                default:
                    return 0xFFFFFFFF;
            }
        }


        /// <summary>
        /// Gets the number of bytes needed to hold the specified size.
        /// </summary>
        public static uint GetByteSize(this FieldSize size)
        {
            switch (size)
            {
                case FieldSize.Bit0:
                case FieldSize.Bit1:
                case FieldSize.Bit2:
                case FieldSize.Bit3:
                case FieldSize.Bit4:
                case FieldSize.Bit5:
                case FieldSize.Bit6:
                case FieldSize.Bit7:
                case FieldSize.BitCount:
                case FieldSize.LowNibble:
                case FieldSize.HighNibble:
                case FieldSize.Byte:
                    return 1;

                case FieldSize.Word:
                case FieldSize.BigEndianWord:
                    return 2;

                case FieldSize.TByte:
                case FieldSize.BigEndianTByte:
                    return 3;

                default:
                    return 4;
            }
        }

        /// <summary>
        /// Gets whether or not the field size represents a floating point number.
        /// </summary>
        public static bool IsFloat(this FieldSize size)
        {
            switch (size)
            {
                case FieldSize.Float:
                case FieldSize.BigEndianFloat:
                case FieldSize.MBF32:
                case FieldSize.LittleEndianMBF32:
                case FieldSize.Double32:
                case FieldSize.BigEndianDouble32:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets whether or not the data of the specified size is stored in big endian order.
        /// </summary>
        public static bool IsBigEndian(this FieldSize size)
        {
            switch (size)
            {
                case FieldSize.BigEndianDWord:
                case FieldSize.BigEndianWord:
                case FieldSize.BigEndianTByte:
                case FieldSize.BigEndianFloat:
                case FieldSize.BigEndianDouble32:
                case FieldSize.MBF32:
                    return true;

                default:
                    return false;
            }
        }
    }
}