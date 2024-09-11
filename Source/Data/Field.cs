using Jamiras.Components;
using System;
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
        /// Gets or sets the field value for floating-point fields.
        /// </summary>
        /// <remarks>
        /// Only used for <see cref="FieldType.Value"/> fields.
        /// </remarks>
        public float Float { get; set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            AppendString(builder, NumberFormat.Decimal);
            return builder.ToString();
        }

        private static void AppendHexValue(StringBuilder builder, uint value, FieldSize size)
        {
            builder.Append("0x");
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
                    builder.Append(value);
                    break;

                case FieldSize.LowNibble:
                case FieldSize.HighNibble:
                    builder.AppendFormat("{0:X1}", value);
                    break;

                default:
                case FieldSize.Byte:
                case FieldSize.BitCount:
                    builder.AppendFormat("{0:X2}", value);
                    break;

                case FieldSize.Word:
                case FieldSize.BigEndianWord:
                    builder.AppendFormat("{0:X4}", value);
                    break;

                case FieldSize.TByte:
                case FieldSize.BigEndianTByte:
                    builder.AppendFormat("{0:X6}", value);
                    break;

                case FieldSize.DWord:
                case FieldSize.BigEndianDWord:
                    builder.AppendFormat("{0:X8}", value);
                    break;
            }
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder"/>.
        /// </summary>
        internal void AppendString(StringBuilder builder, NumberFormat numberFormat, string addAddress = null)
        {
            switch (Type)
            {
                case FieldType.MemoryAddress:
                    AppendMemoryReference(builder, Value, Size, addAddress);
                    break;

                case FieldType.Value:
                    if (numberFormat == NumberFormat.Decimal)
                        builder.Append(Value);
                    else
                        AppendHexValue(builder, Value, Size);
                    break;

                case FieldType.Float:
                    builder.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:0.0#####}", Float);
                    break;

                case FieldType.PreviousValue:
                    builder.Append("prev(");
                    AppendMemoryReference(builder, Value, Size, addAddress);
                    builder.Append(')');
                    break;

                case FieldType.PriorValue:
                    builder.Append("prior(");
                    AppendMemoryReference(builder, Value, Size, addAddress);
                    builder.Append(')');
                    break;

                case FieldType.BinaryCodedDecimal:
                    builder.Append("bcd(");
                    AppendMemoryReference(builder, Value, Size, addAddress);
                    builder.Append(')');
                    break;

                case FieldType.Invert:
                    builder.Append('~');
                    AppendMemoryReference(builder, Value, Size, addAddress);
                    break;

                case FieldType.Recall:
                    builder.Append("{recall}");
                    break;

                case FieldType.None:
                    builder.Append("none");
                    break;

                default:
                    throw new NotImplementedException("Unknown FieldType:" + Type);
            }
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
            {
                if (address == 0)
                {
                    if (addAddress[0] == '(' && addAddress.EndsWith(") + "))
                        builder.Append(addAddress, 1, addAddress.Length - 5);
                    else
                        builder.Append(addAddress, 0, addAddress.Length - 3);

                    builder.Append(')');
                    return;
                }

                if (addAddress.Contains(" & 0x"))
                {
                    builder.Append('(');
                    builder.Append(addAddress);
                    builder.Length -= 3;
                    builder.Append(") + ");
                }
                else
                {
                    builder.Append(addAddress);
                }
            }

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
        /// Gets the number of bytes needed to hold the specified size
        /// </summary>
        public static uint GetByteSize(FieldSize size)
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
                    case FieldType.BinaryCodedDecimal:
                    case FieldType.Invert:
                    case FieldType.Recall:
                        return true;

                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Gets whether or not the value of this field is a floating point number.
        /// </summary>
        public bool IsFloat
        {
            get
            {
                switch (Size)
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
        }

        /// <summary>
        /// Gets whether or not the value of this field is a big endian number.
        /// </summary>
        public bool IsBigEndian
        {
            get
            {
                switch (Size)
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

        /// <summary>
        /// Appends the serialized field to the <paramref name="builder"/>
        /// </summary>
        /// <remarks>
        /// This is a custom serialization format.
        /// </remarks>
        public void Serialize(StringBuilder builder, SerializationContext serializationContext)
        {
            switch (Type)
            {
                case FieldType.Value:
                    builder.Append(Value);
                    return;

                case FieldType.PreviousValue:
                    builder.Append('d');
                    break;

                case FieldType.PriorValue:
                    builder.Append('p');
                    break;

                case FieldType.BinaryCodedDecimal:
                    builder.Append('b');
                    break;

                case FieldType.Invert:
                    builder.Append('~');
                    break;

                case FieldType.Float:
                    builder.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "f{0:0.0#####}", Float);
                    return;

                case FieldType.Recall:
                    builder.Append("{recall}");
                    return;

                default:
                    break;
            }

            switch (Size)
            {
                case FieldSize.Bit0: builder.Append("0xM"); break;
                case FieldSize.Bit1: builder.Append("0xN"); break;
                case FieldSize.Bit2: builder.Append("0xO"); break;
                case FieldSize.Bit3: builder.Append("0xP"); break;
                case FieldSize.Bit4: builder.Append("0xQ"); break;
                case FieldSize.Bit5: builder.Append("0xR"); break;
                case FieldSize.Bit6: builder.Append("0xS"); break;
                case FieldSize.Bit7: builder.Append("0xT"); break;
                case FieldSize.LowNibble: builder.Append("0xL"); break;
                case FieldSize.HighNibble: builder.Append("0xU"); break;
                case FieldSize.Byte: builder.Append("0xH"); break;
                case FieldSize.Word: builder.Append("0x ");  break;
                case FieldSize.TByte: builder.Append("0xW"); break;
                case FieldSize.DWord: builder.Append("0xX"); break;
                case FieldSize.BitCount: builder.Append("0xK"); break;
                case FieldSize.BigEndianWord: builder.Append("0xI"); break;
                case FieldSize.BigEndianTByte: builder.Append("0xJ"); break;
                case FieldSize.BigEndianDWord: builder.Append("0xG"); break;
                case FieldSize.Float: builder.Append("fF"); break;
                case FieldSize.MBF32: builder.Append("fM"); break;
                case FieldSize.LittleEndianMBF32: builder.Append("fL"); break;
                case FieldSize.BigEndianFloat: builder.Append("fB"); break;
                case FieldSize.Double32: builder.Append("fH"); break;
                case FieldSize.BigEndianDouble32: builder.Append("fI"); break;
            }

            builder.Append(serializationContext.FormatAddress(Value));
        }

        /// <summary>
        /// Creates a <see cref="Field"/> from a serialized value.
        /// </summary>
        /// <param name="tokenizer">The tokenizer.</param>
        public static Field Deserialize(Tokenizer tokenizer)
        {
            var fieldType = FieldType.MemoryAddress;
            switch (tokenizer.NextChar)
            {
                case 'd':
                    fieldType = FieldType.PreviousValue;
                    tokenizer.Advance();
                    break;

                case 'p':
                    fieldType = FieldType.PriorValue;
                    tokenizer.Advance();
                    break;

                case 'b':
                    fieldType = FieldType.BinaryCodedDecimal;
                    tokenizer.Advance();
                    break;

                case '~':
                    fieldType = FieldType.Invert;
                    tokenizer.Advance();
                    break;

                case 'h': // explicit hex value
                    tokenizer.Advance();
                    return new Field { Type = FieldType.Value, Value = ReadHexNumber(tokenizer) };

                case 'v': // explicit decimal value
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '-')
                        goto case '-';
                    return new Field { Type = FieldType.Value, Value = ReadNumber(tokenizer) };

                case '-': // explicit negative decimal value
                    tokenizer.Advance();
                    return new Field { Type = FieldType.Value, Value = (uint)(-(int)ReadNumber(tokenizer)) };

                case '{': // variable
                    tokenizer.Advance();
                    var variable = tokenizer.ReadTo('}');
                    tokenizer.Advance();

                    if (variable == "recall")
                        return new Field { Type = FieldType.Recall, Size = FieldSize.DWord };

                    return new Field();
            }

            if (tokenizer.NextChar == 'f')
            {
                tokenizer.Advance();
                switch (tokenizer.NextChar)
                {
                    case 'F':
                        tokenizer.Advance();
                        return new Field { Size = FieldSize.Float, Type = fieldType, Value = ReadHexNumber(tokenizer) };
                    case 'B':
                        tokenizer.Advance();
                        return new Field { Size = FieldSize.BigEndianFloat, Type = fieldType, Value = ReadHexNumber(tokenizer) };
                    case 'M':
                        tokenizer.Advance();
                        return new Field { Size = FieldSize.MBF32, Type = fieldType, Value = ReadHexNumber(tokenizer) };
                    case 'L':
                        tokenizer.Advance();
                        return new Field { Size = FieldSize.LittleEndianMBF32, Type = fieldType, Value = ReadHexNumber(tokenizer) };
                    case 'H':
                        tokenizer.Advance();
                        return new Field { Size = FieldSize.Double32, Type = fieldType, Value = ReadHexNumber(tokenizer) };
                    case 'I':
                        tokenizer.Advance();
                        return new Field { Size = FieldSize.BigEndianDouble32, Type = fieldType, Value = ReadHexNumber(tokenizer) };
                    default: 
                        return new Field { Type = FieldType.Float, Float = ReadFloat(tokenizer) };
                }
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
                case 'i':
                case 'I': size = FieldSize.BigEndianWord; tokenizer.Advance(); break;
                case 'j':
                case 'J': size = FieldSize.BigEndianTByte; tokenizer.Advance(); break;
                case 'g':
                case 'G': size = FieldSize.BigEndianDWord; tokenizer.Advance(); break;
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

            return new Field { Size = size, Type = fieldType, Value = ReadHexNumber(tokenizer) };
        }

        private static uint ReadHexNumber(Tokenizer tokenizer)
        {
            uint value = 0;
            do
            {
                uint charValue;
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
                    default:
                        return value;
                }

                tokenizer.Advance();

                value <<= 4;
                value += charValue;
            } while (true);
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

        private static float ReadFloat(Tokenizer tokenizer)
        {
            bool isNegative = false;
            if (tokenizer.NextChar == '-')
            {
                tokenizer.Advance();
                isNegative = true;
            }

            var token = tokenizer.ReadNumber();
            var value = float.Parse(token.ToString(), System.Globalization.CultureInfo.InvariantCulture);
            if (isNegative)
                value = -value;

            return value;
        }

        /// <summary>
        /// Creates a copy of the <see cref="Field"/>.
        /// </summary>
        public Field Clone()
        {
            return (Field)MemberwiseClone();
        }

        public Field ChangeType(FieldType newType)
        {
            var newField = Clone();
            newField.Type = newType;
            return newField;
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
            return Equals(that);
        }

        private bool Equals(Field that)
        {
            if (that.Type != Type)
                return false;

            switch (Type)
            {
                case FieldType.Value:
                    return (that.Value == Value);

                case FieldType.Float:
                    return (that.Float == Float);

                default:
                    return (that.Value == Value && that.Size == Size);
            }
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
    }
}
