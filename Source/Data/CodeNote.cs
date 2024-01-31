using Jamiras.Components;
using System;
using System.Diagnostics;

namespace RATools.Data
{
    [DebuggerDisplay("{Address,h}: {Note}")]
    public struct CodeNote
    {
        public CodeNote(uint address, string note)
        {
            Address = address;
            Note = note;
            _length = 0;
            _fieldSize = FieldSize.None;
        }

        public string Note { get; set; }

        public uint Address { get; private set; }

        private uint _length;
        private FieldSize _fieldSize;

        public uint Length
        {
            get
            {
                if (_length == 0)
                    CalculateLength();

                return _length;
            }
        }

        public FieldSize FieldSize
        {
            get
            {
                if (_length == 0)
                    CalculateLength();

                return _fieldSize;
            }
        }

        private void CalculateLength()
        {
            _length = 1;
            _fieldSize = FieldSize.None;

            Token token = new Token(Note, 0, Note.Length);
            int index;

            var bitIndex = token.IndexOf("bit", StringComparison.OrdinalIgnoreCase);
            var byteIndex = token.IndexOf("byte", StringComparison.OrdinalIgnoreCase);
            while (bitIndex != -1 || byteIndex != -1)
            {
                Token prefix;

                if (bitIndex == -1 || (byteIndex != -1 && byteIndex < bitIndex))
                {
                    index = byteIndex;
                    prefix = token.SubToken(0, index);
                    token = token.SubToken(index + 4);
                }
                else
                {
                    index = bitIndex;
                    prefix = token.SubToken(0, index);
                    token = token.SubToken(index + 3);

                    // match "bits", but not "bite" even if there is a numeric prefix
                    if (token.Length > 0)
                    {
                        var c = token[0];
                        if (Char.IsLetter(c) && c != 's' && c != 'S')
                        {
                            bitIndex = token.IndexOf("bit", StringComparison.OrdinalIgnoreCase);
                            continue;
                        }
                    }
                }

                // ignore single space or hyphen preceding "bit" or "byte"
                if (prefix.EndsWith("-") || prefix.EndsWith(" "))
                    prefix = prefix.SubToken(0, index - 1);

                // extract the number
                var scan = prefix.Length;
                while (scan > 0 && Char.IsDigit(prefix[scan - 1]))
                    scan--;

                // if a number was found, process it
                prefix = prefix.SubToken(scan);
                if (prefix.Length > 0)
                {
                    var count = UInt32.Parse(prefix.ToString());
                    if (index == byteIndex)
                        count *= 8;

                    _length = (count == 0) ? 1 : (count + 7) / 8;

                    // find the next word after "bits" or "bytes"
                    scan = 0;
                    while (scan < token.Length)
                    {
                        var c = token[scan];
                        if (c != ' ' && c != '-' && c != '(' && c != '[' && c != '<')
                            break;
                        ++scan;
                    }
                    token = token.SubToken(scan);

                    if (token.StartsWith("BE", StringComparison.OrdinalIgnoreCase) ||
                        token.StartsWith("BigEndian", StringComparison.OrdinalIgnoreCase))
                    {
                        if (count == 16)
                            _fieldSize = FieldSize.BigEndianWord;
                        else if (count == 24)
                            _fieldSize = FieldSize.BigEndianTByte;
                        else if (count == 32)
                            _fieldSize = FieldSize.BigEndianDWord;
                        else if (count == 8)
                            _fieldSize = FieldSize.Byte;
                    }
                    else if (token.StartsWith("float", StringComparison.OrdinalIgnoreCase))
                    {
                        if (count == 32)
                            _fieldSize = FieldSize.Float;
                    }
                    else if (token.StartsWith("MBF", StringComparison.OrdinalIgnoreCase))
                    {
                        if (count == 32 || count == 40)
                            _fieldSize = FieldSize.MBF32;
                    }
                    else
                    {
                        if (count == 16)
                            _fieldSize = FieldSize.Word;
                        else if (count == 24)
                            _fieldSize = FieldSize.TByte;
                        else if (count == 32)
                            _fieldSize = FieldSize.DWord;
                        else if (count == 8)
                            _fieldSize = FieldSize.Byte;
                    }

                    // if "bytes" were found, we're done. if "bits" were found, it might be indicating
                    // the size of individual elements. keep searching
                    if (index == byteIndex)
                        return;
                }

                bitIndex = token.IndexOf("bit", StringComparison.OrdinalIgnoreCase);
                byteIndex = token.IndexOf("byte", StringComparison.OrdinalIgnoreCase);
            }

            if (_fieldSize != FieldSize.None)
                return;

            token = new Token(Note, 0, Note.Length);

            index = token.IndexOf("MBF32", StringComparison.OrdinalIgnoreCase);
            if (index != -1)
            {
                _length = 4;
                _fieldSize = FieldSize.MBF32;
            }
            else
            {
                index = token.IndexOf("MBF40", StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    // MBF-40 values are 100% compatible with MBF-32. The last 8 bits are
                    // too insignificant to be handled by the runtime, so can be ignored.
                    _length = 5;
                    _fieldSize = FieldSize.MBF32;
                }
            }
            if (index != -1)
            {
                var subtoken = token.SubToken(index + 5);
                if (subtoken.StartsWith("LE", StringComparison.OrdinalIgnoreCase) ||
                    subtoken.StartsWith("-LE", StringComparison.OrdinalIgnoreCase) ||
                    subtoken.StartsWith(" LE", StringComparison.OrdinalIgnoreCase) ||
                    subtoken.Contains("LittleEndian", StringComparison.OrdinalIgnoreCase))
                {
                    _fieldSize = FieldSize.LittleEndianMBF32;
                }
                return;
            }

            do
            {
                index = token.IndexOf("float", StringComparison.OrdinalIgnoreCase);
                if (index == -1)
                    break;

                if (index > 0 && Char.IsLetter(token[index - 1]))
                    break;

                var subtoken = token.SubToken(index + 5);
                if (subtoken.StartsWith("BE", StringComparison.OrdinalIgnoreCase) ||
                    subtoken.StartsWith("-BE", StringComparison.OrdinalIgnoreCase) ||
                    subtoken.StartsWith(" BE", StringComparison.OrdinalIgnoreCase) ||
                    subtoken.Contains("BigEndian", StringComparison.OrdinalIgnoreCase))
                {
                    _length = 4;
                    _fieldSize = FieldSize.BigEndianFloat;
                    return;
                }

                if (subtoken.Length == 0 || subtoken[0] == ']' || subtoken[0] == ')' || subtoken.StartsWith("32"))
                {
                    _length = 4;
                    _fieldSize = FieldSize.Float;
                    return;
                }

                token = subtoken;
            } while (true);
        }
    }
}
