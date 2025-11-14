using Jamiras.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RATools.Data
{
    /// <summary>
    /// Defines a rich presence script.
    /// </summary>
    [DebuggerDisplay("Rich Presence")]
    public class RichPresence : AssetBase
    {
        public RichPresence()
        {
            Title = "Rich Presence";
        }

        /// <summary>
        /// Gets the maximum number of characters allowed in a rich presence script.
        /// </summary>
        /// <remarks>
        /// The database field actually supports 65535 bytes, but the field on the
        /// webpage limits submissions to 60000 characters so it doesn't have to
        /// convert characters to bytes.
        /// </remarks>
        public const int ScriptMaxLength = 60000;

        public string Script 
        { 
            get { return _script; }
            set
            {
                // normalize to Windows line endings as they take more space and that's what's
                // probably going to be uploaded on the server when the user pastes into the
                // web site.
                if (!value.Contains('\r'))
                    value = value.Replace("\n", "\r\n");

                // ignore any leading/trailing whitespace. normally, the server value won't have
                // a trailing newline, but the generated script will.
                value = value.Trim();

                _script = value;
                _macros = null;
                _displayStrings = null;
                Description = string.Format("{0}/{1} characters", _script.Length, ScriptMaxLength);
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _script;

        public class MacroDefinition
        {
            public MacroDefinition(string name, ValueFormat formatType)
                : this(name, formatType, null)
            {
            }

            public MacroDefinition(string name, ValueFormat formatType, Dictionary<string, string> lookupEntries)
            {
                Name = name;
                FormatType = formatType;
                LookupEntries = lookupEntries;
            }

            public string Name { get; private set; }
            public ValueFormat FormatType { get; private set; }
            public Dictionary<string, string> LookupEntries { get; private set; }

            public override string ToString()
            {
                if (LookupEntries != null)
                    return String.Format("\"{0}\" ({1} Entries)", Name, LookupEntries.Count);

                return String.Format("\"{0}\" ({1})", Name, FormatType);
            }
        }

        public IEnumerable<MacroDefinition> Macros
        {
            get
            {
                if (_macros == null)
                    Parse();

                return _macros;
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<MacroDefinition> _macros;

        [DebuggerDisplay("{Text}")]
        public class DisplayString
        {
            public DisplayString(Trigger condition, string text, IEnumerable<Macro> macros)
            {
                Condition = condition;
                Text = text;
                Macros = macros;
            }

            public Trigger Condition { get; private set; }
            public string Text { get; private set; }

            [DebuggerDisplay("@{Name,nq}({Value})")]
            public class Macro
            {
                public Macro(string name, Value value)
                {
                    Name = name;
                    Value = value;
                }
                public string Name { get; private set; }
                public Value Value { get; private set; }
            }

            public IEnumerable<Macro> Macros { get; private set; }
        }

        public IEnumerable<DisplayString> DisplayStrings
        {
            get
            {
                if (_macros == null)
                    Parse();

                return _displayStrings;
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<DisplayString> _displayStrings;

        private enum Part
        {
            None,
            Format,
            Lookup,
            Display,
        }

        private void Parse()
        {
            _macros = new List<MacroDefinition>();
            _displayStrings = new List<DisplayString>();

            string macroName = null;
            Part part = Part.None;
            Dictionary<string, string> lookups = null;

            var tokenizer = Tokenizer.CreateTokenizer(_script);
            while (tokenizer.NextChar != '\0')
            {
                var line = tokenizer.ReadTo('\n');
                tokenizer.Advance();
                if (line.StartsWith("//"))
                    continue;

                if (line.EndsWith("\r"))
                    line = line.SubToken(0, line.Length - 1);

                if (line.Length == 0)
                    continue;

                if (line.StartsWith("Format:"))
                {
                    macroName = line.Substring(7);
                    part = Part.Format;
                }
                else if (line.StartsWith("FormatType="))
                {
                    if (part == Part.Format)
                    {
                        var formatType = Leaderboard.ParseFormat(line.Substring(11));
                        _macros.Add(new MacroDefinition(macroName, formatType));
                    }
                    part = Part.None;
                }
                else if (line.StartsWith("Lookup:"))
                {
                    macroName = line.Substring(7);
                    lookups = new Dictionary<string, string>();
                    _macros.Add(new MacroDefinition(macroName, ValueFormat.None, lookups));
                    part = Part.Lookup;
                }
                else if (line.StartsWith("Display:"))
                {
                    part = Part.Display;
                }
                else if (part == Part.Lookup)
                {
                    var index = line.IndexOf('=');
                    if (index > 0)
                        lookups.Add(line.Substring(0, index), line.Substring(index + 1));
                }
                else if (part == Part.Display)
                {
                    Trigger condition = null;
                    int index = 0;

                    if (line.StartsWith("?"))
                    {
                        index = line.IndexOf('?', 1);
                        if (index != -1)
                            condition = Trigger.Deserialize(line.Substring(1, index - 1));

                        ++index;
                    }

                    var macros = new List<DisplayString.Macro>();
                    var text = line.Substring(index);
                    while ((index = line.IndexOf('@', index)) != -1)
                    {
                        var leftParen = line.IndexOf('(', index + 1);
                        if (leftParen == -1)
                            break;

                        var rightParen = line.IndexOf(')', leftParen + 1);
                        if (rightParen == -1)
                            break;

                        macroName = line.Substring(index + 1, leftParen - index - 1);
                        var value = Value.Deserialize(line.Substring(leftParen + 1, rightParen - leftParen - 1));
                        macros.Add(new DisplayString.Macro(macroName, value));

                        index = rightParen + 1;
                    }

                    _displayStrings.Add(new DisplayString(condition, text, macros.ToArray()));
                }
            }
        }
    }
}
