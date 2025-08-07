using Jamiras.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RATools.Data
{
    [DebuggerDisplay("{Address,h}: {Summary}")]
    public class CodeNote
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
        private string _summary;

        private class PointerData
        {
            public PointerData()
            {
                OffsetNotes = new List<CodeNote>();
            }
            public enum PointerOffsetType
            {
                None = 0,
                Converted,
                Overflow,
            };

            public Token Header { get; set; }
            public PointerOffsetType OffsetType { get; set; }
            public List<CodeNote> OffsetNotes { get; private set; }
            public bool HasPointers { get; set; }
        };
        private PointerData _pointerData;

        public uint Length
        {
            get
            {
                if (_length == 0)
                    Process();

                return _length;
            }
        }

        public FieldSize FieldSize
        {
            get
            {
                if (_length == 0)
                    Process();

                return _fieldSize;
            }
        }

        public string Summary
        {
            get
            {
                if (_length == 0)
                    Process();

                return _summary.ToString();
            }
        }

        private void Process()
        {
            _length = 1;
            _fieldSize = FieldSize.None;
            _summary = String.Empty;

            var tokenizer = Tokenizer.CreateTokenizer(Note);
            do
            {
                var line = tokenizer.ReadTo('\n').Trim();

                if (_summary.Length == 0 && !line.IsEmpty)
                    _summary = TrimSize(line.Trim().ToString(), false);

                if (line.Length >= 4) // nBit is smallest parsable note
                {
                    var lower = line.ToString().ToLower();
                    if (!lower.Contains("pointer"))
                    {
                        // non-pointer
                        ExtractSize(lower, false);
                    }
                    else if (lower.Contains("pointers"))
                    {
                        // assume actual pointer would be singular
                        ExtractSize(lower, false);
                    }
                    else
                    {
                        // pointer
                        ProcessPointer(tokenizer, lower);
                    }

                    // if we found a size, we're done.
                    if (_fieldSize != FieldSize.None)
                        break;
                }
            } while (tokenizer.NextChar != '\0');
        }

        private void ProcessPointer(Tokenizer tokenizer, string line)
        {
            _pointerData = new PointerData();

            ExtractSize(line, true);

            if (_fieldSize == FieldSize.None)
            {
                // pointer size not specified. assume 32-bit
                _fieldSize = FieldSize.DWord;
                _length = 4;
            }

            _pointerData.Header = tokenizer.ReadTo("\n+");
            if (tokenizer.NextChar != '\0')
            {
                // process indirect notes
            }
        }

        private void ExtractSize(string line, bool isPointer)
        {
            bool isBytesFromBits = false;
            bool wasSizeFound = false;
            bool isLastWordASize = false;
            TokenType lastTokenType = TokenType.None;
            Token lastToken = new Token();

            var tokenizer = Tokenizer.CreateTokenizer(line);
            do
            {
                Token token;
                var tokenType = NextToken(tokenizer, out token);
                if (tokenType == TokenType.None)
                    break;

                // process the word
                bool bWordIsSize = false;
                if (tokenType == TokenType.Number)
                {
                    if (lastTokenType == TokenType.MBF)
                    {
                        int bits;
                        if (Int32.TryParse(token.ToString(), out bits))
                        {
                            if (bits == 32)
                            {
                                _length = 4;
                                _fieldSize = FieldSize.MBF32;
                                bWordIsSize = true;
                                wasSizeFound = true;
                            }
                            else if (bits == 40)
                            {
                                _length = 5;
                                _fieldSize = FieldSize.MBF32;
                                bWordIsSize = true;
                                wasSizeFound = true;
                            }
                        }
                    }
                    else if (lastTokenType == TokenType.Double && token == "32")
                    {
                        _length = 4;
                        _fieldSize = FieldSize.Double32;
                        bWordIsSize = true;
                        wasSizeFound = true;
                    }
                }
                else if (isLastWordASize)
                {
                    if (tokenType == TokenType.Float)
                    {
                        if (_fieldSize == FieldSize.DWord)
                        {
                            _fieldSize = FieldSize.Float;
                            bWordIsSize = true; // allow trailing be/bigendian
                        }
                    }
                    else if (tokenType == TokenType.Double)
                    {
                        if (_fieldSize == FieldSize.DWord || _length == 8)
                        {
                            _fieldSize = FieldSize.Double32;
                            bWordIsSize = true; // allow trailing be/bigendian
                        }
                    }
                    else if (tokenType == TokenType.BigEndian)
                    {
                        switch (_fieldSize)
                        {
                            case FieldSize.Word: _fieldSize = FieldSize.BigEndianWord; break;
                            case FieldSize.TByte: _fieldSize = FieldSize.BigEndianTByte; break;
                            case FieldSize.DWord: _fieldSize = FieldSize.BigEndianDWord; break;
                            case FieldSize.Float: _fieldSize = FieldSize.BigEndianFloat; break;
                            case FieldSize.Double32: _fieldSize = FieldSize.BigEndianDouble32; break;
                            default: break;
                        }
                    }
                    else if (tokenType == TokenType.LittleEndian)
                    {
                        if (_fieldSize == FieldSize.MBF32)
                            _fieldSize = FieldSize.LittleEndianMBF32;
                    }
                    else if (tokenType == TokenType.MBF)
                    {
                        if (_length == 4 || _length == 5)
                            _fieldSize = FieldSize.MBF32;
                    }
                }
                else if (lastTokenType == TokenType.Number)
                {
                    if (tokenType == TokenType.Bits)
                    {
                        if (!wasSizeFound)
                        {
                            uint bits;
                            if (UInt32.TryParse(lastToken.ToString(), out bits))
                            {
                                _length = (bits + 7) / 8;
                                _fieldSize = FieldSize.None;
                                isBytesFromBits = true;
                                bWordIsSize = true;
                                wasSizeFound = true;
                            }
                        }
                    }
                    else if (tokenType == TokenType.Bytes)
                    {
                        if (!wasSizeFound || (isBytesFromBits && !isPointer))
                        {
                            uint bits;
                            if (UInt32.TryParse(lastToken.ToString(), out bits))
                            {
                                _length = bits;
                                _fieldSize = FieldSize.None;
                                isBytesFromBits = false;
                                bWordIsSize = true;
                                wasSizeFound = true;
                            }
                        }
                    }

                    if (bWordIsSize)
                    {
                        switch (_length)
                        {
                            case 0: _length = 1; break; // Unexpected size, reset to defaults (1 byte, Unknown)
                            case 1: _fieldSize = FieldSize.Byte; break;
                            case 2: _fieldSize = FieldSize.Word; break;
                            case 3: _fieldSize = FieldSize.TByte; break;
                            case 4: _fieldSize = FieldSize.DWord; break;
                            default: _fieldSize = FieldSize.Array; break;
                        }
                    }
                }
                else if (tokenType == TokenType.Float)
                {
                    if (!wasSizeFound)
                    {
                        _length = 4;
                        _fieldSize = FieldSize.Float;
                        bWordIsSize = true; // allow trailing be/bigendian

                        if (lastTokenType == TokenType.BigEndian)
                            _fieldSize = FieldSize.BigEndianFloat;
                    }
                }
                else if (tokenType == TokenType.Double)
                {
                    if (!wasSizeFound)
                    {
                        _length = 8;
                        _fieldSize = FieldSize.Double32;
                        bWordIsSize = true; // allow trailing be/bigendian

                        if (lastTokenType == TokenType.BigEndian)
                            _fieldSize = FieldSize.BigEndianDouble32;
                    }
                }

                // store information about the word for later
                isLastWordASize = bWordIsSize;
                lastTokenType = tokenType;

                if (Char.IsLetterOrDigit(tokenizer.NextChar))
                {
                    // number next to word [32bit]
                    lastToken = token;
                }
                else if (tokenizer.NextChar == ' ' || tokenizer.NextChar == '-')
                {
                    // spaces or hyphen could be a joined word [32-bit] [32 bit].
                    lastToken = token;
                }
                else
                {
                    // everything else starts a new phrase
                    lastToken = new Token();
                    lastTokenType = TokenType.None;
                }
            } while (true);
        }

        private enum TokenType
        {
            None = 0,
            Number,
            Bits,
            Bytes,
            Float,
            Double,
            MBF,
            BigEndian,
            LittleEndian,
            Other,
        }

        private static TokenType NextToken(Tokenizer tokenizer, out Token token)
        {
            while (tokenizer.NextChar != '\0' && !Char.IsLetterOrDigit(tokenizer.NextChar))
                tokenizer.Advance();

            if (tokenizer.NextChar == '\0')
            {
                token = new Token();
                return TokenType.None;
            }

            if (Char.IsDigit(tokenizer.NextChar))
            {
                token = tokenizer.ReadNumber();
                return TokenType.Number;
            }

            token = tokenizer.ReadWord();
            switch (token[0])
            {
                case 'b':
                    if (token == "bit" || token == "bits")
                        return TokenType.Bits;
                    if (token == "byte" || token == "bytes")
                        return TokenType.Bytes;
                    if (token == "be" || token == "bigendian")
                        return TokenType.BigEndian;
                    break;

                case 'd':
                    if (token == "double")
                        return TokenType.Double;
                    break;

                case 'f':
                    if (token == "float")
                        return TokenType.Float;
                    break;

                case 'l':
                    if (token == "le" || token == "littleendian")
                        return TokenType.LittleEndian;
                    break;

                case 'm':
                    if (token == "mbf")
                        return TokenType.MBF;
                    break;

                default:
                    break;
            }

            return TokenType.Other;
        }

        private static string TrimSize(string line, bool keepPointer)
        {
            int endIndex = -1;
            var startIndex = line.IndexOf('[');
            if (startIndex != -1)
            {
                endIndex = line.IndexOf(']', startIndex);
            }
            else
            {
                startIndex = line.IndexOf('(');
                if (startIndex != -1)
                    endIndex = line.IndexOf(')');
            }

            if (endIndex == -1)
                return line;

            var tokenizer = Tokenizer.CreateTokenizer(line, startIndex, endIndex - startIndex);
            Token token;
            bool isPointer = false;
            while (tokenizer.NextChar != '\0')
            {
                var tokenType = NextToken(tokenizer, out token);
                if (tokenType == TokenType.Other)
                {
                    if (token.CompareTo("pointer", StringComparison.InvariantCultureIgnoreCase) == 0)
                        isPointer = true;
                    else
                        return line;
                }
            };

            while (startIndex > 0 && Char.IsWhiteSpace(line[startIndex - 1]))
                --startIndex;
            while (endIndex < line.Length - 1 && Char.IsWhiteSpace(line[endIndex + 1]))
                ++endIndex;

            line = line.Remove(startIndex, endIndex - startIndex);
            if (isPointer && keepPointer)
                line = "[pointer] " + line;

            return line;
        }
    }
}
