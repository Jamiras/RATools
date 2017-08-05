using System.Text;

namespace RATools.Data
{
    public struct Field
    {
        public FieldType Type { get; set; }
        public FieldSize Size { get; set; }
        public uint Value { get; set; }

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

        public override bool Equals(object obj)
        {
            if (!(obj is Field))
                return false;

            var that = (Field)obj;
            return that.Type == Type && that.Size == Size && that.Value == Value;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(Field left, Field right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Field left, Field right)
        {
            return !left.Equals(right);
        }
    }

    public enum FieldType
    {
        None = 0,
        MemoryAddress = 1,
        Value = 3,
        PreviousValue = 2, // Delta
    }

    public enum FieldSize
    {
        None = 0,
        Bit0,
        Bit1,
        Bit2,
        Bit3,
        Bit4,
        Bit5,
        Bit6,
        Bit7,
        LowNibble, // b0-3
        HighNibble, // b4-7
        Byte,
        Word,
        DWord,
    }
}
