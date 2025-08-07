using Jamiras.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RATools.Data
{
    [DebuggerDisplay("{Address,h}: {Summary}")]
    public class CodeNote
    {
        public CodeNote(uint address, string note)
        {
            Address = address;

            SetNote(note);
        }

        public string Note { get; private set; }

        public uint Address { get; private set; }

        public uint Length { get; private set; }

        public FieldSize Size { get; private set; }

        public string Summary { get; private set; }

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

            public List<CodeNote> OffsetNotes { get; private set; }
            public PointerOffsetType OffsetType { get; set; }
            public bool HasPointers { get; set; }
        };
        private PointerData _pointerData;

        public bool IsPointer
        {
            get
            {
                return (_pointerData != null);
            }
        }

        public IEnumerable<CodeNote> OffsetNotes
        {
            get
            {
                if (_pointerData != null)
                    return _pointerData.OffsetNotes;

                return Enumerable.Empty<CodeNote>();
            }
        }

        private void SetNote(string note)
        {
            Note = note;
            Length = 1;
            Size = FieldSize.None;
            Summary = String.Empty;

            var tokenizer = Tokenizer.CreateTokenizer(Note);
            do
            {
                var line = tokenizer.ReadTo('\n').Trim();
                tokenizer.Advance();

                if (Summary.Length == 0 && !line.IsEmpty)
                    Summary = TrimSize(line.Trim().ToString(), false);

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
                    if (Size != FieldSize.None)
                        break;
                }
            } while (tokenizer.NextChar != '\0');
        }

        private void ProcessPointer(Tokenizer tokenizer, string line)
        {
            _pointerData = new PointerData();

            ExtractSize(line, true);

            if (Size == FieldSize.None)
            {
                // pointer size not specified. assume 32-bit
                Size = FieldSize.DWord;
                Length = 4;
            }

            if (tokenizer.NextChar == '\0')
                return;

            int startIndex = 0;
            var remaining = tokenizer.ReadTo('\0');
            do
            {
                var index = remaining.IndexOf("\n+", startIndex);
                if (index != -1 && index < remaining.Length - 2 && !Char.IsDigit(remaining[index + 2]))
                    index = remaining.IndexOf("\n+", index + 1);

                var nextNoteToken = index == -1 ? remaining.SubToken(startIndex) : remaining.SubToken(startIndex, index - startIndex);
                startIndex = index == -1 ? -1 : index + 1;
                var nextNote = nextNoteToken.TrimRight().SubToken(1).ToString().Replace("\n+", "\n");

                uint offset;
                bool success;
                index = 0;
                if (nextNote.StartsWith("0x"))
                {
                    index = 2;
                    while (Char.IsLetterOrDigit(nextNote[index]))
                        index++;

                    success = UInt32.TryParse(nextNote.Substring(2, index - 2),
                        System.Globalization.NumberStyles.HexNumber, null, out offset);
                }
                else
                {
                    while (Char.IsDigit(nextNote[index]))
                        index++;

                    success = UInt32.TryParse(nextNote.Substring(0, index), out offset);
                }

                if (success)
                {
                    // skip over [whitespace] [optional separator] [whitespace]
                    while (index < nextNote.Length && Char.IsWhiteSpace(nextNote[index]) && nextNote[index] != '\n')
                        index++;
                    if (index < nextNote.Length && !Char.IsLetterOrDigit(nextNote[index]))
                        index++;
                    while (index < nextNote.Length && Char.IsWhiteSpace(nextNote[index]) && nextNote[index] != '\n')
                        index++;

                    var nestedNote = new CodeNote(offset, nextNote.Substring(index));
                    _pointerData.OffsetNotes.Add(nestedNote);
                    _pointerData.HasPointers |= nestedNote.IsPointer;
                }
            } while (startIndex != -1);

            // assume anything annotated as a 32-bit pointer will read the whole pointer
            // and apply a conversion
            if (Length == 4)
            {
                _pointerData.OffsetType = PointerData.PointerOffsetType.Converted;

                // if any offset exceeds the memory available for the system, assume it's leveraging
                // overflow math insteaed of masking, and don't attempt to translate the addresses.
                if (_pointerData.OffsetNotes.Any(n => n.Address >= 0x10000000))
                    _pointerData.OffsetType = PointerData.PointerOffsetType.Overflow;
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
                                Length = 4;
                                Size = FieldSize.MBF32;
                                bWordIsSize = true;
                                wasSizeFound = true;
                            }
                            else if (bits == 40)
                            {
                                Length = 5;
                                Size = FieldSize.MBF32;
                                bWordIsSize = true;
                                wasSizeFound = true;
                            }
                        }
                    }
                    else if (lastTokenType == TokenType.Double && token == "32")
                    {
                        Length = 4;
                        Size = FieldSize.Double32;
                        bWordIsSize = true;
                        wasSizeFound = true;
                    }
                }
                else if (isLastWordASize)
                {
                    if (tokenType == TokenType.Float)
                    {
                        if (Size == FieldSize.DWord)
                        {
                            Size = FieldSize.Float;
                            bWordIsSize = true; // allow trailing be/bigendian
                        }
                    }
                    else if (tokenType == TokenType.Double)
                    {
                        if (Size == FieldSize.DWord || Length == 8)
                        {
                            Size = FieldSize.Double32;
                            bWordIsSize = true; // allow trailing be/bigendian
                        }
                    }
                    else if (tokenType == TokenType.BigEndian)
                    {
                        switch (Size)
                        {
                            case FieldSize.Word: Size = FieldSize.BigEndianWord; break;
                            case FieldSize.TByte: Size = FieldSize.BigEndianTByte; break;
                            case FieldSize.DWord: Size = FieldSize.BigEndianDWord; break;
                            case FieldSize.Float: Size = FieldSize.BigEndianFloat; break;
                            case FieldSize.Double32: Size = FieldSize.BigEndianDouble32; break;
                            default: break;
                        }
                    }
                    else if (tokenType == TokenType.LittleEndian)
                    {
                        if (Size == FieldSize.MBF32)
                            Size = FieldSize.LittleEndianMBF32;
                    }
                    else if (tokenType == TokenType.MBF)
                    {
                        if (Length == 4 || Length == 5)
                            Size = FieldSize.MBF32;
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
                                Length = (bits + 7) / 8;
                                Size = FieldSize.None;
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
                                Length = bits;
                                Size = FieldSize.None;
                                isBytesFromBits = false;
                                bWordIsSize = true;
                                wasSizeFound = true;
                            }
                        }
                    }

                    if (bWordIsSize)
                    {
                        switch (Length)
                        {
                            case 0: Length = 1; break; // Unexpected size, reset to defaults (1 byte, Unknown)
                            case 1: Size = FieldSize.Byte; break;
                            case 2: Size = FieldSize.Word; break;
                            case 3: Size = FieldSize.TByte; break;
                            case 4: Size = FieldSize.DWord; break;
                            default: Size = FieldSize.Array; break;
                        }
                    }
                }
                else if (tokenType == TokenType.Float)
                {
                    if (!wasSizeFound)
                    {
                        Length = 4;
                        Size = FieldSize.Float;
                        bWordIsSize = true; // allow trailing be/bigendian

                        if (lastTokenType == TokenType.BigEndian)
                            Size = FieldSize.BigEndianFloat;
                    }
                }
                else if (tokenType == TokenType.Double)
                {
                    if (!wasSizeFound)
                    {
                        Length = 8;
                        Size = FieldSize.Double32;
                        bWordIsSize = true; // allow trailing be/bigendian

                        if (lastTokenType == TokenType.BigEndian)
                            Size = FieldSize.BigEndianDouble32;
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

            var tokenizer = Tokenizer.CreateTokenizer(line.ToLower(), startIndex, endIndex - startIndex);
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

            line = line.Remove(startIndex, endIndex - startIndex + 1);
            if (isPointer && keepPointer)
                line = "[pointer] " + line;

            return line;
        }
    }
}
