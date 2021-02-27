using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RATools.Parser
{
    [DebuggerDisplay("{DisplayString}")]
    internal class RichPresenceBuilder
    {
        private class Lookup
        {
            public ExpressionBase Func { get; set; }
            public IDictionary<int, string> Entries { get; set; }
            public StringConstantExpression Fallback { get; set; }
        };

        private class ValueField
        {
            public ExpressionBase Func { get; set; }
            public ValueFormat Format { get; set; }
        }

        public RichPresenceBuilder()
        {
            _valueFields = new TinyDictionary<string, ValueField>();
            _lookupFields = new TinyDictionary<string, Lookup>();
            _conditionalDisplayStrings = new List<string>();
        }

        private List<string> _conditionalDisplayStrings;
        private TinyDictionary<string, ValueField> _valueFields;
        private TinyDictionary<string, Lookup> _lookupFields;

        public string DisplayString { get; set; }
        public int Line { get; set; }

        public void AddConditionalDisplayString(string condition, string displayString)
        {
            _conditionalDisplayStrings.Add(String.Format("?{0}?{1}", condition, displayString));
        }

        public void AddValueField(ExpressionBase func, string name, ValueFormat format)
        {
            _valueFields[name] = new ValueField
            {
                Func = func,
                Format = format
            };
        }

        public ParseErrorExpression AddLookupField(ExpressionBase func, string name, DictionaryExpression dict, StringConstantExpression fallback)
        {
            var tinyDict = new TinyDictionary<int, string>();
            foreach (var entry in dict.Entries)
            {
                var key = entry.Key as IntegerConstantExpression;
                if (key == null)
                    return new ParseErrorExpression("key is not an integer", entry.Key);

                var value = entry.Value as StringConstantExpression;
                if (value == null)
                    return new ParseErrorExpression("value is not a string", entry.Value);

                tinyDict[key.Value] = value.Value;
            }

            _lookupFields[name] = new Lookup
            {
                Func = func,
                Entries = tinyDict,
                Fallback = fallback
            };

            return null;
        }

        public override string ToString()
        {
            if (String.IsNullOrEmpty(DisplayString))
                return "[No display string]";

            var builder = new StringBuilder();

            foreach (var lookup in _lookupFields)
            {
                builder.Append("Lookup:");
                builder.AppendLine(lookup.Key);

                var list = new List<int>(lookup.Value.Entries.Keys);
                list.Sort();

                foreach (var key in list)
                {
                    builder.Append(key);
                    builder.Append('=');
                    builder.AppendLine(lookup.Value.Entries[key]);
                }

                if (lookup.Value.Fallback != null && !String.IsNullOrEmpty(lookup.Value.Fallback.Value))
                {
                    builder.Append("*=");
                    builder.AppendLine(lookup.Value.Fallback.Value);
                }

                builder.AppendLine();
            }

            foreach (var value in _valueFields)
            {
                builder.Append("Format:");
                builder.AppendLine(value.Key);

                builder.Append("FormatType=");
                builder.AppendLine(Leaderboard.GetFormatString(value.Value.Format));

                builder.AppendLine();
            }

            builder.AppendLine("Display:");
            foreach (var conditionalString in _conditionalDisplayStrings)
                builder.AppendLine(conditionalString);
            builder.AppendLine(DisplayString);

            return builder.ToString();
        }

        public bool IsEmpty
        {
            get
            {
                return String.IsNullOrEmpty(DisplayString) && _valueFields.Count == 0 &&
                    _lookupFields.Count == 0 && _conditionalDisplayStrings.Count == 0;
            }
        }

        public void Clear()
        {
            _conditionalDisplayStrings.Clear();
            _valueFields.Clear();
            _lookupFields.Clear();
            DisplayString = null;
        }

        public ParseErrorExpression Merge(RichPresenceBuilder from)
        {
            if (!String.IsNullOrEmpty(from.DisplayString))
                DisplayString = from.DisplayString;

            _conditionalDisplayStrings.AddRange(from._conditionalDisplayStrings);

            foreach (var kvp in from._valueFields)
            {
                ValueField field;
                if (!_valueFields.TryGetValue(kvp.Key, out field))
                    _valueFields.Add(kvp);
                else if (field.Format != kvp.Value.Format)
                    return new ParseErrorExpression("Multiple rich_presence_value calls with the same name must have the same format", field.Func);
            }

            foreach (var kvp in from._lookupFields)
            {
                Lookup existing;
                if (!_lookupFields.TryGetValue(kvp.Key, out existing))
                {
                    _lookupFields.Add(kvp);
                }
                else
                {
                    var toMerge = kvp.Value;

                    if (existing.Fallback != toMerge.Fallback)
                        return new ParseErrorExpression("Multiple rich_presence_lookup calls with the same name must have the same fallback", toMerge.Fallback ?? existing.Fallback);

                    if (existing.Entries.Count != toMerge.Entries.Count)
                        return new ParseErrorExpression("Multiple rich_presence_lookup calls with the same name must have the same dictionary", toMerge.Func ?? existing.Func);

                    foreach (var kvp2 in existing.Entries)
                    {
                        string value;
                        if (!toMerge.Entries.TryGetValue(kvp2.Key, out value) || kvp2.Value != value)
                            return new ParseErrorExpression("Multiple rich_presence_lookup calls with the same name must have the same dictionary", toMerge.Func ?? existing.Func);
                    }
                }
            }

            return null;
        }
    }
}
