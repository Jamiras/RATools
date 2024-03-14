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
    [DebuggerDisplay("{_valueFields.Count} macros, {_lookupFields.Count} lookups, {_displayStrings.Count} display strings")]
    public class RichPresenceBuilder
    {
        public RichPresenceBuilder()
        {
            _valueFields = new TinyDictionary<string, ValueField>();
            _lookupFields = new TinyDictionary<string, Lookup>();
            _displayStrings = new List<ConditionalDisplayString>();
        }

        private List<ConditionalDisplayString> _displayStrings;
        private TinyDictionary<string, ValueField> _valueFields;
        private TinyDictionary<string, Lookup> _lookupFields;

        /// <summary>
        /// The line associated to the `rich_presence_display` call.
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// Defines a mapping macro.
        /// </summary>
        private class Lookup
        {
            /// <summary>
            /// A reference to the function call that generated this lookup.
            /// </summary>
            public ExpressionBase Func { get; set; }

            /// <summary>
            /// The entries in the lookup.
            /// </summary>
            public IDictionary<int, string> Entries { get; set; }

            /// <summary>
            /// The string to display if an entry doesn't exist in the lookup.
            /// </summary>
            public StringConstantExpression Fallback { get; set; }
        };

        /// <summary>
        /// Defines a formatting macro.
        /// </summary>
        [DebuggerDisplay("{Format}")]
        private class ValueField
        {
            /// <summary>
            /// A reference to the function call that generated this lookup.
            /// </summary>
            public ExpressionBase Func { get; set; }

            /// <summary>
            /// The format to apply when using this lookup.
            /// </summary>
            public ValueFormat Format { get; set; }
        }

        [DebuggerDisplay("@{Name}({Value})")]
        private class DisplayStringParameter
        {
            /// <summary>
            /// The name of the macro to use for this parameter.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// The parameter to pass to the macro.
            /// </summary>
            public Value Value { get; set; }
        }

        public class ConditionalDisplayString
        {
            private ICollection<DisplayStringParameter> _parameters;

            /// <summary>
            /// The raw string with placeholders.
            /// </summary>
            public StringConstantExpression Format { get; set; }

            /// <summary>
            /// The condition when the string should be displayed.
            /// </summary>
            /// <remarks>If null, this is the default case.</remarks>
            public Trigger Condition { get; set; }

            public void AddParameter(string macro, Value parameter)
            {
                if (_parameters == null)
                    _parameters = new List<DisplayStringParameter>();

                _parameters.Add(new DisplayStringParameter { Name = macro, Value = parameter });
            }

            public void AddParameter(StringConstantExpression parameter)
            {
                if (_parameters == null)
                    _parameters = new List<DisplayStringParameter>();

                _parameters.Add(new DisplayStringParameter { Name = parameter.Value, Value = null });
            }

            /// <summary>
            /// Merges the Parameters into the Format string.
            /// </summary>
            public string Serialize(SerializationContext serializationContext)
            {
                return Serialize(serializationContext, false);
            }

            public override string ToString()
            {
                return Serialize(new SerializationContext(), true);
            }

            private string Serialize(SerializationContext serializationContext, bool ignoreMissing)
            {
                var parameters = new ArrayExpression();
                if (_parameters != null)
                {
                    foreach (var parameter in _parameters)
                    {
                        string formatted;
                        if (parameter.Value == null)
                        {
                            // raw string, not a macro
                            formatted = parameter.Name;
                        }
                        else
                        {
                            formatted = String.Format("@{0}({1})",
                                parameter.Name, parameter.Value.Serialize(serializationContext));
                        }

                        parameters.Entries.Add(new StringConstantExpression(formatted));
                    }
                }

                ExpressionBase result = FormatFunction.Evaluate(Format, parameters, ignoreMissing);
                var stringResult = result as StringConstantExpression;
                return (stringResult != null) ? stringResult.Value : "";
            }

            public SoftwareVersion MinimumVersion()
            {
                var minimumVersion = (Condition != null) ? Condition.MinimumVersion() : Data.Version.MinimumVersion;
                if (_parameters != null)
                {
                    foreach (var parameter in _parameters)
                        minimumVersion = minimumVersion.OrNewer(parameter.Value.MinimumVersion());
                }

                return minimumVersion;
            }

            public uint MaximumAddress()
            {
                uint maximumAddress = (Condition != null) ? Condition.MaximumAddress() : 0;
                if (_parameters != null)
                {
                    foreach (var parmeter in _parameters)
                        maximumAddress = Math.Max(maximumAddress, parmeter.Value.MaximumAddress());
                }

                return maximumAddress;
            }
        }

        public bool IsValid
        {
            get { return _displayStrings.Any(d => d.Condition == null); }
        }

        public string DisplayString
        {
            get
            {
                var defaultDisplayString = _displayStrings.FirstOrDefault(d => d.Condition == null);
                if (defaultDisplayString == null)
                    return "[no default display string]";

                return defaultDisplayString.Serialize(new SerializationContext());
            }
        }

        public static ValueFormat GetValueFormat(string macro)
        {
            return RichPresenceMacroFunction.GetValueFormat(macro);
        }

        public static string GetFormatString(ValueFormat format)
        {
            return RichPresenceValueFunction.GetFormatString(format);
        }

        public ErrorExpression AddValueField(ExpressionBase func, StringConstantExpression name, ValueFormat format)
        {
            ValueField field;
            if (_valueFields.TryGetValue(name.Value, out field))
            {
                if (field.Format != format)
                    return new ErrorExpression("Multiple rich_presence_value calls with the same name must have the same format", name);

                return null;
            }

            _valueFields[name.Value] = new ValueField
            {
                Func = func,
                Format = format
            };

            return null;
        }

        public ErrorExpression AddLookupField(ExpressionBase func, StringConstantExpression name, DictionaryExpression dict, StringConstantExpression fallback)
        {
            if (_valueFields.ContainsKey(name.Value))
                return new ErrorExpression("A rich_presence_value already exists for '" + name.Value + "'", name);

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

            _lookupFields[name.Value] = new Lookup
            {
                Func = func,
                Entries = tinyDict,
                Fallback = fallback
            };

            return null;
        }

        public ConditionalDisplayString AddDisplayString(Trigger condition, StringConstantExpression formatString)
        {
            var displayString = new ConditionalDisplayString
            {
                Format = formatString,
                Condition = condition,                
            };

            _displayStrings.Add(displayString);
            return displayString;
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

            foreach (var displayString in _displayStrings)
                minimumVersion = minimumVersion.OrNewer(displayString.MinimumVersion());

            return minimumVersion;
        }

        public uint MaximumAddress()
        {
            uint maximumAddress = 0;
            foreach (var displayString in _displayStrings)
                maximumAddress = Math.Max(maximumAddress, displayString.MaximumAddress());

            return maximumAddress;
        }

        public override string ToString()
        {
            return Serialize(new SerializationContext());
        }

        public string Serialize(SerializationContext serializationContext)
        {
            if (!IsValid)
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
            foreach (var displayString in _displayStrings.Where(d => d.Condition != null))
            {
                builder.Append('?');
                builder.Append(displayString.Condition.Serialize(serializationContext));
                builder.Append('?');
                builder.AppendLine(displayString.Serialize(serializationContext));
            }
            var defaultDisplayString = _displayStrings.FirstOrDefault(d => d.Condition == null);
            if (defaultDisplayString != null)
                builder.AppendLine(defaultDisplayString.Serialize(serializationContext));

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
                return _displayStrings.Count == 0 && _valueFields.Count == 0 &&
                    _lookupFields.Count == 0;
            }
        }

        public void Clear()
        {
            _displayStrings.Clear();
            _valueFields.Clear();
            _lookupFields.Clear();
        }

        public ErrorExpression Merge(RichPresenceBuilder from)
        {
            _displayStrings.AddRange(from._displayStrings);

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
