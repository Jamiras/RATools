using RATools.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RATools.Parser
{
    [DebuggerDisplay("{Address,h}: {Alias}")]
    public class MemoryAccessorAlias : IComparer<MemoryAccessorAlias>
    {
        public MemoryAccessorAlias(uint address)
        {
            Address = address;
        }

        public MemoryAccessorAlias(uint address, CodeNote note)
            : this(address)
        {
            SetNote(note);
        }

        private void SetNote(CodeNote note)
        {
            Note = note;

            if (note != null)
            {
                PrimarySize = note.Size;

                // if note doesn't specify a size, assume 8-bit
                if (PrimarySize == FieldSize.None)
                    PrimarySize = FieldSize.Byte;
            }
        }

        public CodeNote Note { get; private set; }
        private string _aliasFromNote = String.Empty;
        private string _subtextFromNote = null;

        public uint Address { get; private set; }

        public string Alias
        {
            get { return _alias ?? _aliasFromNote; }
            set
            {
                if (value == _aliasFromNote)
                    _alias = null;
                else
                    _alias = value;
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _alias;
        private NameStyle _aliasStyle = NameStyle.None;
        private Dictionary<FieldSize, string> _aliases;

        public IEnumerable<MemoryAccessorAlias> Children
        {
            get
            {
                return _children ?? Enumerable.Empty<MemoryAccessorAlias>();
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<MemoryAccessorAlias> _children;

        [Flags]
        private enum ReferencedSizeMask
        {
            None = 0,
            Bit0 = 1 << FieldSize.Bit0,
            Bit1 = 1 << FieldSize.Bit1,
            Bit2 = 1 << FieldSize.Bit2,
            Bit3 = 1 << FieldSize.Bit3,
            Bit4 = 1 << FieldSize.Bit4,
            Bit5 = 1 << FieldSize.Bit5,
            Bit6 = 1 << FieldSize.Bit6,
            Bit7 = 1 << FieldSize.Bit7,
            LowNibble = 1 << FieldSize.LowNibble,
            HighNibble = 1 << FieldSize.HighNibble,
            Byte = 1 << FieldSize.Byte,
            Word = 1 << FieldSize.Word,
            TByte = 1 << FieldSize.TByte,
            DWord = 1 << FieldSize.DWord,
            BitCount = 1 << FieldSize.BitCount,
            BigEndianWord = 1 << FieldSize.BigEndianWord,
            BigEndianTByte = 1 << FieldSize.BigEndianTByte,
            BigEndianDWord = 1 << FieldSize.BigEndianDWord,
            Float = 1 << FieldSize.Float,
            MBF32 = 1 << FieldSize.MBF32,
            LittleEndianMBF32 = 1 << FieldSize.LittleEndianMBF32,
            BigEndianFloat = 1 << FieldSize.BigEndianFloat,
            Double32 = 1 << FieldSize.Double32,
            BigEndianDouble32 = 1 << FieldSize.BigEndianDouble32,
        }

        private ReferencedSizeMask _referencedSizes = ReferencedSizeMask.None;

        public FieldSize PrimarySize { get; private set; }

        public void ReferenceSize(FieldSize size)
        {
            _referencedSizes |= (ReferencedSizeMask)(1 << (int)size);

            if (PrimarySize == FieldSize.None)
            {
                if (Field.GetMaxValue(size) < 255)
                    PrimarySize = FieldSize.Byte;
                else
                    PrimarySize = size;
            }
        }

        public IEnumerable<FieldSize> ReferencedSizes
        {
            get
            {
                var referencedSizes = (int)_referencedSizes >> 1;
                var size = 1;
                while (referencedSizes != 0)
                {
                    if ((referencedSizes & 1) == 1)
                        yield return (FieldSize)size;

                    ++size;
                    referencedSizes >>= 1;
                }
            }
        }

        public bool HasMultipleReferencedSizes
        {
            get
            {
                // masking with n-1 removes the rightmost non-zero bit.
                // if any bits remain, there are multiple sizes.
                var n = (int)_referencedSizes;
                return (n & (n - 1)) != 0;
            }
        }

        public bool HasReferencedSize(FieldSize size)
        {
            return ((int)_referencedSizes & (1 << (int)size)) != 0;
        }

        public bool IsOnlyReferencedSize(FieldSize size)
        {
            return ((int)_referencedSizes ^ (1 << (int)size)) == 0;
        }

        public string GetAlias(FieldSize size)
        {
            if (!HasReferencedSize(size))
                return null;

            if (size == PrimarySize)
                return Alias;

            _aliases ??= new Dictionary<FieldSize, string>();

            string alias;
            if (!_aliases.TryGetValue(size, out alias))
            {
                alias = Alias;

                var subNote = Note?.GetSubNote(size);
                if (subNote == null)
                {
                    // if size is the only referenced size, make it the primary size.
                    if (IsOnlyReferencedSize(size))
                    {
                        PrimarySize = size;
                        return alias;
                    }

                    // multiple sizes are referenced. we can't generate a unique name without a note alias
                    if (String.IsNullOrEmpty(alias))
                    {
                        _aliases[size] = null;
                        return null;
                    }

                    // if the primary size isn't referenced (note didn't specify size or all
                    // sizes are smaller than a byte), and the size is byte or larger, make
                    // it the primary size.
                    if (!HasReferencedSize(PrimarySize) && Field.GetMaxValue(size) >= 255)
                    {
                        PrimarySize = size;
                        return alias;
                    }

                    // append the size to the note alias to differentiate it from the primary size accessor
                    subNote = Field.GetSizeFunction(size);
                }

                if (String.IsNullOrEmpty(alias))
                {
                    alias = _aliasStyle.BuildName(subNote);
                }
                else
                {
                    var suffix = _aliasStyle.BuildName("x " + subNote);
                    alias = String.Concat(Alias, suffix.AsSpan(1));
                }

                _aliases[size] = alias;
            }

            return alias;
        }

        public void UpdateAliasFromNote(NameStyle style)
        {
            _aliases = null;
            _aliasStyle = style;

            if (style == NameStyle.None || Note == null)
            {
                _aliasFromNote = String.Empty;

                if (_children != null)
                {
                    foreach (var child in _children)
                        child.UpdateAliasFromNote(style);
                }

                return;
            }

            var text = Note.Summary;

            if (text == "Unlabelled")
            {
                text = null;

                var enumerator = Note.Values.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var firstValue = enumerator.Current.Value;
                    if (!enumerator.MoveNext())
                        text = firstValue.ToString();
                }
            }

            if (!HasReferencedSize(PrimarySize) && !HasMultipleReferencedSizes && _referencedSizes != ReferencedSizeMask.None)
            {
                PrimarySize = ReferencedSizes.First();

                var subNote = Note.GetSubNote(PrimarySize);
                if (subNote != null && subNote != text)
                {
                    if (String.IsNullOrEmpty(text))
                        text = subNote;
                    else
                        text = text + " - " + subNote;
                }
            }

            if (!String.IsNullOrEmpty(text))
            {
                // remove subtext: "score (bcd)" => "score"
                int leftParen = -1;
                if (text[text.Length - 1] == ')')
                    leftParen = text.LastIndexOf('(');
                else if (text[text.Length - 1] == ']')
                    leftParen = text.LastIndexOf('[');
                if (leftParen > 4)
                {
                    var subtext = text.Substring(leftParen + 1, text.Length - leftParen - 2);
                    if (!KeepSubtext(subtext))
                    {
                        _subtextFromNote = subtext;
                        text = text.Substring(0, leftParen).TrimEnd();
                    }
                }

                // if string starts with numbers, potentially treat it as subtext
                if (_subtextFromNote == null && text.Length > 1 && Char.IsDigit(text[0]))
                {
                    int index = 1;
                    while (index < text.Length && !Char.IsLetter(text[index]))
                        ++index;

                    if (text.Length - index >= 4 && !Char.IsDigit(text[index - 1]))
                    {
                        _subtextFromNote = text.Substring(0, index).Trim();
                        text = text.Substring(index);
                    }
                }
            }

            // build the function name
            var functionName = style.BuildName(text);
            if (!String.IsNullOrEmpty(functionName))
            {
                if (AchievementScriptInterpreter.IsReservedFunctionName(functionName))
                    functionName += '_';

                _aliasFromNote = functionName.ToString();
            }

            if (_children != null)
            {
                foreach (var child in _children)
                    child.UpdateAliasFromNote(style);
            }
        }

        private static bool KeepSubtext(string text)
        {
            // keep regional indicators as part of note. [purposefully requires uppercase]
            switch (text.Length)
            {
                case 1:
                    return (text == "E" || text == "U" || text == "J");
                case 2:
                    return (text == "EU" || text == "US" || text == "JP");
                default:
                    return false;
            }
        }

        public static void ResolveConflictingAliases(IEnumerable<MemoryAccessorAlias> memoryAccessors)
        {
            var aliasMap = new Dictionary<string, List<MemoryAccessorAlias>>();
            foreach (var memoryAccessor in memoryAccessors)
                CaptureAliases(aliasMap, memoryAccessor);

            // have to put new keys in separate dictionary to avoid modified iterator error
            var newAliases = new Dictionary<string, List<MemoryAccessorAlias>>();
            foreach (var kvp in aliasMap)
            {
                if (kvp.Value.Count == 1)
                    continue;

                for (int i = kvp.Value.Count - 1; i >= 0; i--)
                {
                    var memoryAccessor = kvp.Value[i];
                    if (!String.IsNullOrEmpty(memoryAccessor._subtextFromNote))
                    {
                        var suffix = memoryAccessor._aliasStyle.BuildName("x " + memoryAccessor._subtextFromNote);
                        var newAlias = String.Concat(memoryAccessor._aliasFromNote, suffix.AsSpan(1));
                        memoryAccessor._aliasFromNote = newAlias;

                        kvp.Value.RemoveAt(i);

                        List<MemoryAccessorAlias> list;
                        if (!aliasMap.TryGetValue(newAlias, out list) &&
                            !newAliases.TryGetValue(newAlias, out list))
                        {
                            list = new List<MemoryAccessorAlias>();
                            newAliases[newAlias] = list;
                        }

                        list.Add(memoryAccessor);
                    }
                }
            }

            foreach (var kvp in newAliases)
            {
                if (kvp.Value.Count > 1)
                    aliasMap[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in aliasMap)
            {
                if (kvp.Value.Count < 2)
                    continue;

                kvp.Value.Sort(new MemoryAccessorAlias(0));

                int suffix = 1;
                foreach (var memoryAccessor in kvp.Value)
                {
                    if (suffix > 1)
                        memoryAccessor.Alias = memoryAccessor.Alias + '_' + suffix;

                    suffix++;
                }
            }
        }

        private static void CaptureAliases(Dictionary<string, List<MemoryAccessorAlias>> aliasMap, MemoryAccessorAlias memoryAccessor)
        {
            var alias = memoryAccessor.Alias;
            if (!String.IsNullOrEmpty(alias))
            {
                List<MemoryAccessorAlias> list;
                if (!aliasMap.TryGetValue(alias, out list))
                {
                    list = new List<MemoryAccessorAlias>();
                    list.Add(memoryAccessor);
                    aliasMap.Add(alias, list);
                }
                else
                {
                    list.Add(memoryAccessor);
                }
            }

            foreach (var child in memoryAccessor.Children)
                CaptureAliases(aliasMap, child);
        }


        public static void AddMemoryAccessors(List<MemoryAccessorAlias> memoryAccessors, Achievement achievement, Dictionary<uint, CodeNote> codeNotes)
        {
            MemoryAccessorAlias root = new MemoryAccessorAlias(0);
            root._children = memoryAccessors;

            AddMemoryAccessors(root, achievement.Trigger.Groups, codeNotes);
        }

        public static void AddMemoryAccessors(List<MemoryAccessorAlias> memoryAccessors, Leaderboard leaderboard, Dictionary<uint, CodeNote> codeNotes)
        {
            MemoryAccessorAlias root = new MemoryAccessorAlias(0);
            root._children = memoryAccessors;

            AddMemoryAccessors(root, leaderboard.Start.Groups, codeNotes);
            AddMemoryAccessors(root, leaderboard.Cancel.Groups, codeNotes);
            AddMemoryAccessors(root, leaderboard.Submit.Groups, codeNotes);
            AddMemoryAccessors(root, leaderboard.Value.Values, codeNotes);
        }

        public static void AddMemoryAccessors(List<MemoryAccessorAlias> memoryAccessors, RichPresence richPresence, Dictionary<uint, CodeNote> codeNotes)
        {
            MemoryAccessorAlias root = new MemoryAccessorAlias(0);
            root._children = memoryAccessors;

            foreach (var displayString in richPresence.DisplayStrings)
            {
                if (displayString.Condition != null)
                    AddMemoryAccessors(root, displayString.Condition.Groups, codeNotes);

                foreach (var macro in displayString.Macros)
                    AddMemoryAccessors(root, macro.Value.Values, codeNotes);
            }
        }

        public static void AddMemoryAccessors(List<MemoryAccessorAlias> memoryAccessors, Trigger trigger, Dictionary<uint, CodeNote> codeNotes)
        {
            MemoryAccessorAlias root = new MemoryAccessorAlias(0);
            root._children = memoryAccessors;

            AddMemoryAccessors(root, trigger.Groups, codeNotes);
        }

        public static void AddMemoryAccessors(List<MemoryAccessorAlias> memoryAccessors, Value value, Dictionary<uint, CodeNote> codeNotes)
        {
            MemoryAccessorAlias root = new MemoryAccessorAlias(0);
            root._children = memoryAccessors;

            AddMemoryAccessors(root, value.Values, codeNotes);
        }

        private static void AddMemoryAccessors(MemoryAccessorAlias root, IEnumerable<RequirementGroup> groups, Dictionary<uint, CodeNote> codeNotes)
        {
            foreach (var group in groups)
            {
                CodeNote parentNote = null;
                MemoryAccessorAlias parentMemoryAccessor = root;

                foreach (var requirement in group.Requirements)
                {
                    MemoryAccessorAlias leftMemoryAccessor = requirement.Left.IsMemoryReference ? 
                        GetMemoryAccessor(parentMemoryAccessor, requirement.Left, codeNotes, parentNote) : null;
                    MemoryAccessorAlias rightMemoryAccessor = requirement.Right.IsMemoryReference ?
                        GetMemoryAccessor(parentMemoryAccessor, requirement.Right, codeNotes, parentNote) : null;

                    if (requirement.Type != RequirementType.AddAddress)
                    {
                        // not an AddAddress. reset to root
                        parentMemoryAccessor = root;
                        parentNote = null;
                    }
                    else if (requirement.Operator.IsModifier() && requirement.Operator != RequirementOperator.BitwiseAnd)
                    {
                        // scaled value - assume indexing - keep current parent
                    }
                    else if (leftMemoryAccessor != null)
                    {
                        // chaining off left accessor
                        parentMemoryAccessor = leftMemoryAccessor;
                        parentNote = leftMemoryAccessor.Note;
                    }
                    else if (rightMemoryAccessor != null)
                    {
                        // chaining off right accessor
                        parentMemoryAccessor = rightMemoryAccessor;
                        parentNote = rightMemoryAccessor.Note;
                    }
                }
            }
        }

        int IComparer<MemoryAccessorAlias>.Compare(MemoryAccessorAlias x, MemoryAccessorAlias y)
        {
            return (int)x.Address - (int)y.Address;
        }

        private static MemoryAccessorAlias GetMemoryAccessor(MemoryAccessorAlias parentMemoryAccessor,
            Field field, Dictionary<uint, CodeNote> codeNotes, CodeNote parentNote)
        {
            parentMemoryAccessor._children ??= new List<MemoryAccessorAlias>();

            var memoryAccessor = new MemoryAccessorAlias(field.Value);
            var index = parentMemoryAccessor._children.BinarySearch(memoryAccessor, memoryAccessor);

            if (index < 0)
            {
                CodeNote note = null;
                if (parentNote == null)
                    codeNotes.TryGetValue(field.Value, out note);
                else
                    note = parentNote.OffsetNotes.FirstOrDefault(n => n.Address == field.Value);

                if (note != null)
                    memoryAccessor.SetNote(note);

                parentMemoryAccessor._children.Insert(~index, memoryAccessor);
            }
            else
            {
                memoryAccessor = parentMemoryAccessor._children[index];
            }

            memoryAccessor.ReferenceSize(field.Size);
            return memoryAccessor;
        }
    }
}
