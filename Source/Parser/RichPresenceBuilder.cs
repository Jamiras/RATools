using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser
{
    [DebuggerDisplay("{DisplayString}")]
    public class RichPresenceBuilder
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

        public static ValueFormat GetValueFormat(string macro)
        {
            return RichPresenceMacroFunction.GetValueFormat(macro);
        }

        public static string GetFormatString(ValueFormat format)
        {
            return RichPresenceValueFunction.GetFormatString(format);
        }

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

        public ErrorExpression AddLookupField(ExpressionBase func, string name, DictionaryExpression dict, StringConstantExpression fallback)
        {
            var tinyDict = new TinyDictionary<int, string>();
            foreach (var entry in dict.Entries)
            {
                var key = entry.Key as IntegerConstantExpression;
                if (key == null)
                    return new ErrorExpression("key is not an integer", entry.Key);

                var value = entry.Value as StringConstantExpression;
                if (value == null)
                    return new ErrorExpression("value is not a string", entry.Value);

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

        public SoftwareVersion MinimumVersion()
        {
            if (String.IsNullOrEmpty(DisplayString))
                return Data.Version.Uninitialized;

            var minimumVersion = Data.Version.MinimumVersion;

            if (_valueFields.Any(f => f.Value.Format == ValueFormat.ASCIIChar || f.Value.Format == ValueFormat.UnicodeChar))
                minimumVersion = minimumVersion.OrNewer(Data.Version._1_0);

            foreach (var lookup in _lookupFields)
            {
                if (lookup.Value.Fallback != null && lookup.Value.Fallback.Value.Length > 0)
                {
                    minimumVersion = minimumVersion.OrNewer(Data.Version._0_73);
                    break;
                }
            }

            return minimumVersion;
        }

        public override string ToString()
        {
            return Serialize(new SerializationContext());
        }

        public string Serialize(SerializationContext serializationContext)
        {
            if (String.IsNullOrEmpty(DisplayString))
                return "[No display string]";

            var builder = new StringBuilder();

            foreach (var lookup in _lookupFields)
            {
                builder.Append("Lookup:");
                builder.AppendLine(lookup.Key);

                var fallback = (lookup.Value.Fallback != null && lookup.Value.Fallback.Value.Length > 0) ? lookup.Value.Fallback.Value : null;
                AppendRichPresenceLookupEntries(builder, lookup.Value.Entries, serializationContext, fallback);

                if (fallback != null)
                {
                    builder.Append("*=");
                    builder.AppendLine(fallback);
                }

                builder.AppendLine();
            }

            bool disableBuiltInMacros = serializationContext.MinimumVersion < Data.Version._1_0;
            foreach (var value in _valueFields)
            {
                if (!disableBuiltInMacros)
                {
                    if (RichPresenceMacroFunction.GetValueFormat(value.Key) == value.Value.Format)
                        continue;
                }

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

        private void AppendRichPresenceLookupEntries(StringBuilder builder, IDictionary<int, string> entries, SerializationContext serializationContext, string fallback)
        {
            // determine how many entries have the same values
            var sharedValues = new HashSet<string>();
            var sharedValueCount = 0;

            if (serializationContext.MinimumVersion >= Data.Version._0_79)
            {
                var uniqueValues = new HashSet<string>();
                foreach (var value in entries.Values)
                {
                    if (value != fallback && !uniqueValues.Add(value))
                    {
                        sharedValues.Add(value);
                        sharedValueCount++;
                    }
                }
            }

            // if there are at least 10 entries and at least 20% of the lookup is repeated, or if there
            // are less than 10 entries and at least half of the lookup is repeated, then generate ranges
            bool useRanges;
            var entryCount = (fallback == null) ? entries.Count : entries.Count(e => e.Value != fallback);
            if (entryCount >= 10)
                useRanges = (sharedValueCount > entryCount / 5);
            else
                useRanges = (sharedValueCount > entryCount / 2);

            // get an ordered set of keys for the lookup
            var list = new List<int>(entries.Keys);
            list.Sort();

            if (!useRanges)
            {
                // just dump each entry as its own line
                foreach (var key in list)
                {
                    if (entries[key] != fallback)
                    {
                        builder.Append(key);
                        builder.Append('=');
                        builder.AppendLine(entries[key]);
                    }
                }
            }
            else
            {
                // remove the lowest entry from the list and generate a row for it
                list.Reverse();
                while (list.Count > 0)
                {
                    var key = list[list.Count - 1];
                    list.RemoveAt(list.Count - 1);

                    var value = entries[key];
                    if (value == fallback)
                        continue;

                    if (!sharedValues.Contains(value))
                    {
                        // singular entry, just dump it
                        builder.Append(key);
                    }
                    else
                    {
                        // shared entry, get the other keys (and remove them from the list
                        // so they don't get separate entries)
                        var keys = new List<int>();

                        foreach (var kvp in entries)
                        {
                            if (kvp.Value == value)
                            {
                                list.Remove(kvp.Key);
                                keys.Add(kvp.Key);
                            }
                        }

                        // build the list of ranges for the entry
                        keys.Sort();

                        int i = 0;
                        while (i < keys.Count)
                        {
                            var first = i;
                            var next = keys[first] + 1;
                            while (i + 1 < keys.Count && keys[i + 1] == next)
                            {
                                next++;
                                i++;
                            }

                            if (first > 0)
                                builder.Append(',');

                            builder.Append(keys[first]);
                            if (i != first)
                            {
                                builder.Append('-');
                                builder.Append(keys[i]);
                            }

                            i++;
                        }
                    }

                    builder.Append('=');
                    builder.AppendLine(value);
                }
            }
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

        public ErrorExpression Merge(RichPresenceBuilder from)
        {
            if (!String.IsNullOrEmpty(from.DisplayString))
            {
                DisplayString = from.DisplayString;
                Line = from.Line;
            }

            _conditionalDisplayStrings.AddRange(from._conditionalDisplayStrings);

            foreach (var kvp in from._valueFields)
            {
                ValueField field;
                if (!_valueFields.TryGetValue(kvp.Key, out field))
                    _valueFields.Add(kvp);
                else if (field.Format != kvp.Value.Format)
                    return new ErrorExpression("Multiple rich_presence_value calls with the same name must have the same format", field.Func);
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
                        return new ErrorExpression("Multiple rich_presence_lookup calls with the same name must have the same fallback", toMerge.Fallback ?? existing.Fallback);

                    if (existing.Entries.Count != toMerge.Entries.Count)
                        return new ErrorExpression("Multiple rich_presence_lookup calls with the same name must have the same dictionary", toMerge.Func ?? existing.Func);

                    foreach (var kvp2 in existing.Entries)
                    {
                        string value;
                        if (!toMerge.Entries.TryGetValue(kvp2.Key, out value) || kvp2.Value != value)
                            return new ErrorExpression("Multiple rich_presence_lookup calls with the same name must have the same dictionary", toMerge.Func ?? existing.Func);
                    }
                }
            }

            return null;
        }
    }
}
