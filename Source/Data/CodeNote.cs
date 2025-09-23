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

        private List<KeyValuePair<Token, Token>> _values;

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
                {
                    Summary = TrimSize(line.Trim().ToString(), false);

                    if (Summary.IndexOfAny(new[] { '=', ':' }) != -1)
                        ExtractValuesFromSummary();
                }

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

                    // if we found a size, stop looking for a size/pointer
                    if (Size != FieldSize.None)
                        break;
                }
            } while (tokenizer.NextChar != '\0');

            // if we didn't find a size, reset the tokenizer so we can look for values.
            // otherwise, just look for values in the remaining part of the note.
            if (Size == FieldSize.None)
            {
                // skip the first line, values would have already been extracted from the summary
                tokenizer = Tokenizer.CreateTokenizer(Note);
                tokenizer.ReadTo('\n');
                tokenizer.Advance();
            }

            while (tokenizer.NextChar != '\0')
            {
                var line = tokenizer.ReadTo('\n').Trim();
                tokenizer.Advance();

                if (line.Length >= 4)
                    CheckValue(line);
            }
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
                while (index != -1 && index < remaining.Length - 2 &&
                    !Char.IsDigit(remaining[index + 2]))
                {
                    // found a plus at the start of a line, but it's not followed by an offset. skip it.
                    // this primarily handles nested pointers "\n++", but also handles things like "\n+Why?"
                    index = remaining.IndexOf("\n+", index + 1);
                }

                Token nextNoteToken;
                if (index == -1)
                {
                    // last line. capture remaining text
                    nextNoteToken = remaining.SubToken(startIndex).TrimRight();
                    startIndex = -1;
                }
                else
                {
                    // found another line. capture intermediate text
                    nextNoteToken = remaining.SubToken(startIndex, index - startIndex).TrimRight();
                    startIndex = index + 1;
                }

                if (nextNoteToken.IsEmpty || nextNoteToken[0] != '+')
                {
                    // ignore non-offset data between header and body
                    continue;
                }

                // remove the leading plus and any further plusses at the start
                // of newlines within the note token.
                var nextNote = nextNoteToken.SubToken(1).ToString().Replace("\n+", "\n");

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
                    {
                        index++;
                        while (index < nextNote.Length && Char.IsWhiteSpace(nextNote[index]) && nextNote[index] != '\n')
                            index++;
                    }

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

        private void ExtractValuesFromSummary()
        {
            if (Summary.IndexOfAny(new[] { '=', ':' }) == -1)
                return;

            var commaIndex = Summary.IndexOfAny(new[] { ',', ';' });
            if (commaIndex == -1)
                return;

            var newSummary = Summary;
            Tokenizer tokenizer = null;
            var separator = Summary[commaIndex];
            var bracket = Summary.IndexOf('['); // note [1=a, 2=b]
            if (bracket != -1)
            {
                var bracket2 = Summary.IndexOf(']');
                if (bracket2 != -1)
                    tokenizer = Tokenizer.CreateTokenizer(Summary, bracket + 1, bracket2 - bracket - 1);

                newSummary = Summary.Substring(0, bracket).Trim();
            }

            if (tokenizer == null)
            {
                var paren = Summary.IndexOf('('); // note (1=a, 2=b)
                if (paren != -1)
                {
                    var paren2 = Summary.IndexOf(')');
                    if (paren2 != -1)
                        tokenizer = Tokenizer.CreateTokenizer(Summary, paren + 1, paren2 - paren - 1);

                    newSummary = Summary.Substring(0, paren).Trim();
                }
            }

            if (tokenizer == null)
            {
                var index = commaIndex;
                while (index > 0 && Summary[index - 1] != '=' && Summary[index - 1] != ':')
                    index--;
                if (index > 0)
                {
                    index--;
                    while (index > 0 && (Char.IsLetterOrDigit(Summary[index - 1]) || Char.IsWhiteSpace(Summary[index - 1])))
                        index--;

                    var separatorIndex = Summary.IndexOfAny(new[] { '=', ':' }, index);
                    if (separatorIndex != -1 && IsValue(new Token(Summary, index, separatorIndex - index).Trim()))
                    {
                        tokenizer = Tokenizer.CreateTokenizer(Summary, index, Summary.Length - index);
                        tokenizer.SkipWhitespace();

                        newSummary = Summary.Substring(0, index);
                    }
                }
            }

            if (tokenizer != null)
            {
                while (tokenizer.NextChar != '\0')
                {
                    var clause = tokenizer.ReadTo(separator);
                    tokenizer.Advance();

                    CheckValue(clause.Trim());
                }
            }

            Summary = newSummary;
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private static bool IsValue(Token token)
        {
            if (token.Length == 0)
                return false;

            char lowBit, highBit;
            if (GetBitRange(token, out lowBit, out highBit))
                return true;

            int index = 0;
            if (token.StartsWith("0x"))
                index += 2;

            if (index == token.Length)
                return false;

            while (IsHexDigit(token[index]))
            {
                if (++index == token.Length)
                    return true;
            }

            while (index < token.Length && Char.IsWhiteSpace(token[index]))
                index++;

            if (index < token.Length && token[index] == '-')
            {
                index++;

                while (index < token.Length && Char.IsWhiteSpace(token[index]))
                    index++;

                if (!IsHexDigit(token[index]))
                    return false;

                do
                {
                    if (++index == token.Length)
                        return true;
                } while (IsHexDigit(token[index]));
            }

            return false;
        }

        private void CheckValue(Token clause)
        {
            var separatorLength = 1;
            var separator = clause.IndexOf('=');

            var colon = clause.IndexOf(':');
            if (colon != -1 && (separator == -1 || colon < separator))
                separator = colon;

            var arrow = clause.IndexOf("->");
            if (arrow != -1 && (separator == -1 || arrow < separator))
            {
                separator = arrow;
                separatorLength = 2;
            }

            if (separator != -1)
            {
                var left = clause.SubToken(0, separator).Trim();
                var right = clause.SubToken(separator + separatorLength).Trim();

                if (IsValue(left))
                    AddValue(left, right);
                else if (IsValue(right))
                    AddValue(right, left);
            }
        }

        private void AddValue(Token value, Token note)
        {
            if (_values == null)
                _values = new List<KeyValuePair<Token, Token>>();

            _values.Add(new KeyValuePair<Token, Token>(value, note));
        }

        public string GetSubNote(FieldSize size)
        {
            switch (size)
            {
                case FieldSize.Bit0: return GetBitNote((low, high) => low <= '0' && high >= '0');
                case FieldSize.Bit1: return GetBitNote((low, high) => low <= '1' && high >= '1');
                case FieldSize.Bit2: return GetBitNote((low, high) => low <= '2' && high >= '2');
                case FieldSize.Bit3: return GetBitNote((low, high) => low <= '3' && high >= '3');
                case FieldSize.Bit4: return GetBitNote((low, high) => low <= '4' && high >= '4');
                case FieldSize.Bit5: return GetBitNote((low, high) => low <= '5' && high >= '5');
                case FieldSize.Bit6: return GetBitNote((low, high) => low <= '6' && high >= '6');
                case FieldSize.Bit7: return GetBitNote((low, high) => low <= '7' && high >= '7');
                case FieldSize.LowNibble: return GetBitNote((low, high) => low == '0' && high == '3');
                case FieldSize.HighNibble: return GetBitNote((low, high) => low == '4' && high == '7');
                default: return null;
            }
        }

        private string GetBitNote(Func<char, char, bool> checkBitFunc)
        {
            if (_values != null)
            {
                foreach (var kvp in _values)
                {
                    char lowBit, highBit;
                    if (GetBitRange(kvp.Key, out lowBit, out highBit) && checkBitFunc(lowBit, highBit))
                        return kvp.Value.ToString();
                }
            }

            return null;
        }

        private static bool GetBitRange(Token token, out char lowBit, out char highBit)
        {
            lowBit = highBit = 'X';

            var index = 0;
            if (token[0] == 'b' || token[0] == 'B')
            {
                index = 1;

                if (token.StartsWith("bit", StringComparison.InvariantCultureIgnoreCase))
                {
                    index = 3;
                    if (index < token.Length && (token[index] == 's' || token[index] == 'S'))
                        index = 4;

                    while (index < token.Length && Char.IsWhiteSpace(token[index]))
                        index++;
                }

                if (index < token.Length && Char.IsDigit(token[index]))
                {
                    lowBit = highBit = token[index];

                    index++;
                    while (index < token.Length && Char.IsWhiteSpace(token[index]))
                        index++;
                    if (index < token.Length)
                    {
                        if (token[index] == '-')
                        {
                            index++;
                            while (index < token.Length && Char.IsWhiteSpace(token[index]))
                                index++;

                            if (index < token.Length && (token[index] == 'b' || token[index] == 'B'))
                            {
                                index++;
                                if (index < token.Length - 2 &&
                                    (token[index] == 'i' || token[index] == 'I') &&
                                    (token[index + 1] == 't' || token[index + 1] == 'T'))
                                {
                                    index += 2;
                                }
                            }
                            if (index < token.Length && Char.IsDigit(token[index]))
                                highBit = token[index++];
                        }
                        else if (Char.IsDigit(token[index]))
                        {
                            highBit = token[index++];
                        }
                        else if (token.SubToken(index).CompareTo("set", StringComparison.InvariantCultureIgnoreCase) == 0)
                        {
                            return true;
                        }
                    }
                }
                return (index == token.Length);
            }

            if (token.CompareTo("low4", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                lowBit = '0';
                highBit = '3';
                return true;
            }

            if (token.CompareTo("high4", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                lowBit = '4';
                highBit = '7';
                return true;
            }

            if (token.CompareTo("upper4", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                lowBit = '4';
                highBit = '7';
                return true;
            }

            if (token.CompareTo("low nibble", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                lowBit = '0';
                highBit = '3';
                return true;
            }

            if (token.CompareTo("high nibble", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                lowBit = '4';
                highBit = '7';
                return true;
            }

            return false;
        }
    }
}
