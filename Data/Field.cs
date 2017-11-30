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
            if (Type == FieldType.None)
                return "none";

            if (Type == FieldType.Value)
                return Value.ToString();

            var builder = new StringBuilder();
            if (Type == FieldType.PreviousValue)
                builder.Append("prev(");

            switch (Size)
            {
                case FieldSize.Bit0: builder.Append("bit0"); break;
                case FieldSize.Bit1: builder.Append("bit1"); break;
                case FieldSize.Bit2: builder.Append("bit2"); break;
                case FieldSize.Bit3: builder.Append("bit3"); break;
                case FieldSize.Bit4: builder.Append("bit4"); break;
                case FieldSize.Bit5: builder.Append("bit5"); break;
                case FieldSize.Bit6: builder.Append("bit6"); break;
                case FieldSize.Bit7: builder.Append("bit7"); break;
                case FieldSize.LowNibble: builder.Append("low4"); break;
                case FieldSize.HighNibble: builder.Append("high4"); break;
                case FieldSize.Byte: builder.Append("byte"); break;
                case FieldSize.Word: builder.Append("word"); break;
                case FieldSize.DWord: builder.Append("dword"); break;
            }

            builder.Append("(0x");
            builder.AppendFormat("{0:X6}", Value);
            builder.Append(')');

            if (Type == FieldType.PreviousValue)
                builder.Append(')');

            return builder.ToString();
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
                case FieldSize.DWord: builder.Append('X'); break;
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

            if (!tokenizer.Match("0x"))
                return new Field { Type = FieldType.Value, Value = ReadNumber(tokenizer) };

            FieldSize size = FieldSize.None;
            switch (tokenizer.NextChar)
            {
                case 'M': size = FieldSize.Bit0; tokenizer.Advance(); break;
                case 'N': size = FieldSize.Bit1; tokenizer.Advance(); break;
                case 'O': size = FieldSize.Bit2; tokenizer.Advance(); break;
                case 'P': size = FieldSize.Bit3; tokenizer.Advance(); break;
                case 'Q': size = FieldSize.Bit4; tokenizer.Advance(); break;
                case 'R': size = FieldSize.Bit5; tokenizer.Advance(); break;
                case 'S': size = FieldSize.Bit6; tokenizer.Advance(); break;
                case 'T': size = FieldSize.Bit7; tokenizer.Advance(); break;
                case 'L': size = FieldSize.LowNibble; tokenizer.Advance(); break;
                case 'U': size = FieldSize.HighNibble; tokenizer.Advance(); break;
                case 'H': size = FieldSize.Byte; tokenizer.Advance(); break;
                case 'X': size = FieldSize.DWord; tokenizer.Advance(); break;
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9': size = FieldSize.Word; break;
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
            return that.Type == Type && that.Size == Size && that.Value == Value;
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
        /// Four bytes (32-bit). Read from memory in little-endian mode.
        /// </summary>
        DWord,
    }
}
