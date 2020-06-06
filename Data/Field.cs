using Jamiras.Components;
using System.Text;

namespace RATools.Data
{
    /// <summary>
    /// Defines a <see cref="Requirement"/> field.
    /// </summary>
    public struct Field
    {
        /// <summary>
        /// Gets or sets the field type.
        /// </summary>
        public FieldType Type { get; set; }

        /// <summary>
        /// Gets or sets the field size.
        /// </summary>
        public FieldSize Size { get; set; }

        /// <summary>
        /// Gets or sets the field value.
        /// </summary>
        /// <remarks>
        /// For <see cref="FieldType.MemoryAddress"/> or <see cref="FieldType.PreviousValue"/> fields, this is the memory address.
        /// For <see cref="FieldType.Value"/> fields, this is a raw value.
        /// </remarks>
        public uint Value { get; set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            AppendString(builder, NumberFormat.Decimal);
            return builder.ToString();
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder"/>.
        /// </summary>
        internal void AppendString(StringBuilder builder, NumberFormat numberFormat, string addAddress = null)
        {
            if (Type == FieldType.None)
            {
                builder.Append("none");
                return;
            }

            if (Type == FieldType.Value)
            {
                if (numberFormat == NumberFormat.Decimal)
                {
                    builder.Append(Value);
                }
                else
                {
                    builder.Append("0x");
                    switch (Size)
                    {
                        case FieldSize.Bit0:
                        case FieldSize.Bit1:
                        case FieldSize.Bit2:
                        case FieldSize.Bit3:
                        case FieldSize.Bit4:
                        case FieldSize.Bit5:
                        case FieldSize.Bit6:
                        case FieldSize.Bit7:
                            builder.Append(Value);
                            break;

                        case FieldSize.LowNibble:
                        case FieldSize.HighNibble:
                            builder.AppendFormat("{0:X1}", Value);
                            break;

                        default:
                        case FieldSize.Byte:
                        case FieldSize.BitCount:
                            builder.AppendFormat("{0:X2}", Value);
                            break;

                        case FieldSize.Word:
                            builder.AppendFormat("{0:X4}", Value);
                            break;

                        case FieldSize.TByte:
                            builder.AppendFormat("{0:X6}", Value);
                            break;

                        case FieldSize.DWord:
                            builder.AppendFormat("{0:X8}", Value);
                            break;
                    }
                }

                return;
            }

            if (Type == FieldType.PreviousValue)
                builder.Append("prev(");
            else if (Type == FieldType.PriorValue)
                builder.Append("prior(");

            AppendMemoryReference(builder, Value, Size, addAddress);

            if (Type == FieldType.PreviousValue || Type == FieldType.PriorValue)
                builder.Append(')');
        }

        /// <summary>
        /// Gets a string representing the function call to reference the specified memory.
        /// </summary>
        public static string GetMemoryReference(uint address, FieldSize size)
        {
            var builder = new StringBuilder();
            AppendMemoryReference(builder, address, size);
            return builder.ToString();
        }

        private static void AppendMemoryReference(StringBuilder builder, uint address, FieldSize size, string addAddress = null)
        {
            builder.Append(GetSizeFunction(size));
            builder.Append('(');

            if (!string.IsNullOrEmpty(addAddress))
                builder.Append(addAddress);

            builder.Append("0x");
            builder.AppendFormat("{0:X6}", address);
            builder.Append(')');
        }

        /// <summary>
        /// Gets a string representing the function call for retrieving the specified amount of memory.
        /// </summary>
        public static string GetSizeFunction(FieldSize size)
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
                default: return size.ToString();
            }
        }

        /// <summary>
        /// Gets the maximum value representable by the specified size.
        /// </summary>
        public static uint GetMaxValue(FieldSize size)
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
                    return 15;

                case FieldSize.Byte:
                    return 255;

                case FieldSize.Word:
                    return 65535;

                default:
                    return uint.MaxValue;
            }
        }

        /// <summary>
        /// Gets whether or not the field references memory.
        /// </summary>
        public bool IsMemoryReference
        {
            get
            {
                switch (Type)
                {
                    case FieldType.MemoryAddress:
                    case FieldType.PreviousValue:
                    case FieldType.PriorValue:
                        return true;

                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Appends the serialized field to the <paramref name="builder"/>
        /// </summary>
        /// <remarks>
        /// This is a custom serialization format.
        /// </remarks>
        internal void Serialize(StringBuilder builder)
        {
            if (Type == FieldType.Value)
            {
                builder.Append(Value);
                return;
            }

            if (Type == FieldType.PreviousValue)
                builder.Append('d');
            else if (Type == FieldType.PriorValue)
                builder.Append('p');

            builder.Append("0x");

            switch (Size)
            {
                case FieldSize.Bit0: builder.Append('M'); break;
                case FieldSize.Bit1: builder.Append('N'); break;
                case FieldSize.Bit2: builder.Append('O'); break;
                case FieldSize.Bit3: builder.Append('P'); break;
                case FieldSize.Bit4: builder.Append('Q'); break;
                case FieldSize.Bit5: builder.Append('R'); break;
                case FieldSize.Bit6: builder.Append('S'); break;
                case FieldSize.Bit7: builder.Append('T'); break;
                case FieldSize.LowNibble: builder.Append('L'); break;
                case FieldSize.HighNibble: builder.Append('U'); break;
                case FieldSize.Byte: builder.Append('H'); break;
                case FieldSize.Word: builder.Append(' ');  break;
                case FieldSize.TByte: builder.Append('W'); break;
                case FieldSize.DWord: builder.Append('X'); break;
                case FieldSize.BitCount: builder.Append('K'); break;
            }

            builder.AppendFormat("{0:x6}", Value);
        }


        /// <summary>
        /// Creates a <see cref="Field"/> from a serialized value.
        /// </summary>
        /// <param name="tokenizer">The tokenizer.</param>
        internal static Field Deserialize(Tokenizer tokenizer)
        {
            var fieldType = FieldType.MemoryAddress;
            if (tokenizer.NextChar == 'd')
            {
                fieldType = FieldType.PreviousValue;
                tokenizer.Advance();
            }
            else if (tokenizer.NextChar == 'p')
            {
                fieldType = FieldType.PriorValue;
                tokenizer.Advance();
            }

            if (!tokenizer.Match("0x"))
                return new Field { Type = FieldType.Value, Value = ReadNumber(tokenizer) };

            FieldSize size = FieldSize.None;
            switch (tokenizer.NextChar)
            {
                case 'm':
                case 'M': size = FieldSize.Bit0; tokenizer.Advance(); break;
                case 'n':
                case 'N': size = FieldSize.Bit1; tokenizer.Advance(); break;
                case 'o':
                case 'O': size = FieldSize.Bit2; tokenizer.Advance(); break;
                case 'p':
                case 'P': size = FieldSize.Bit3; tokenizer.Advance(); break;
                case 'q':
                case 'Q': size = FieldSize.Bit4; tokenizer.Advance(); break;
                case 'r':
                case 'R': size = FieldSize.Bit5; tokenizer.Advance(); break;
                case 's':
                case 'S': size = FieldSize.Bit6; tokenizer.Advance(); break;
                case 't':
                case 'T': size = FieldSize.Bit7; tokenizer.Advance(); break;
                case 'l':
                case 'L': size = FieldSize.LowNibble; tokenizer.Advance(); break;
                case 'u':
                case 'U': size = FieldSize.HighNibble; tokenizer.Advance(); break;
                case 'h':
                case 'H': size = FieldSize.Byte; tokenizer.Advance(); break;
                case 'w':
                case 'W': size = FieldSize.TByte; tokenizer.Advance(); break;
                case 'x':
                case 'X': size = FieldSize.DWord; tokenizer.Advance(); break;
                case 'k':
                case 'K': size = FieldSize.BitCount; tokenizer.Advance(); break;
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case 'A':
                case 'a':
                case 'B':
                case 'b':
                case 'C':
                case 'c':
                case 'D':
                case 'd':
                case 'E':
                case 'e':
                case 'F':
                case 'f': size = FieldSize.Word; break;
                case ' ': size = FieldSize.Word; tokenizer.Advance(); break;
            }

            uint address = 0;
            do
            {
                uint charValue = 255;
                switch (tokenizer.NextChar)
                {
                    case '0': charValue = 0; break;
                    case '1': charValue = 1; break;
                    case '2': charValue = 2; break;
                    case '3': charValue = 3; break;
                    case '4': charValue = 4; break;
                    case '5': charValue = 5; break;
                    case '6': charValue = 6; break;
                    case '7': charValue = 7; break;
                    case '8': charValue = 8; break;
                    case '9': charValue = 9; break;
                    case 'a':
                    case 'A': charValue = 10; break;
                    case 'b':
                    case 'B': charValue = 11; break;
                    case 'c':
                    case 'C': charValue = 12; break;
                    case 'd':
                    case 'D': charValue = 13; break;
                    case 'e':
                    case 'E': charValue = 14; break;
                    case 'f':
                    case 'F': charValue = 15; break;
                }

                if (charValue == 255)
                    break;

                tokenizer.Advance();
                address <<= 4;
                address += charValue;
            } while (true);

            return new Field { Size = size, Type = fieldType, Value = address };
        }

        private static uint ReadNumber(Tokenizer tokenizer)
        {
            uint value = 0;
            while (tokenizer.NextChar >= '0' && tokenizer.NextChar <= '9')
            {
                value *= 10;
                value += (uint)(tokenizer.NextChar - '0');
                tokenizer.Advance();
            }

            return value;
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (!(obj is Field))
                return false;

            var that = (Field)obj;
            if (that.Type != Type || that.Value != Value)
                return false;

            if (Type == FieldType.Value)
                return true;

            return that.Size == Size;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Determines if two <see cref="Field"/>s are equivalent.
        /// </summary>
        public static bool operator ==(Field left, Field right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines if two <see cref="Field"/>s are not equivalent.
        /// </summary>
        public static bool operator !=(Field left, Field right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Supported number formats
    /// </summary>
    public enum NumberFormat
    {
        /// <summary>
        /// Display values as decimal numbers.
        /// </summary>
        Decimal,

        /// <summary>
        /// Display values as hexadecimal numbers.
        /// </summary>
        Hexadecimal,
    }

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
        MemoryAddress = 1,
        
        /// <summary>
        /// A raw value.
        /// </summary>
        Value = 3,

        /// <summary>
        /// The previous value at a memory address.
        /// </summary>
        PreviousValue = 2, // Delta

        /// <summary>
        /// The last differing value at a memory address.
        /// </summary>
        PriorValue = 4, // Prior
    }

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
    }
}
