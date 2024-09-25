using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
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
            _valueFields = new SortedDictionary<string, ValueField>();
            _lookupFields = new SortedDictionary<string, Lookup>();
            _displayStrings = new List<ConditionalDisplayString>();
        }

        private readonly List<ConditionalDisplayString> _displayStrings;
        private readonly SortedDictionary<string, ValueField> _valueFields;
        private readonly SortedDictionary<string, Lookup> _lookupFields;

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

        internal class ConditionalDisplayString
        {
            private Dictionary<int, Parameter> _parameters;

            internal int ParameterCount
            {
                get { return _parameters != null ? _parameters.Count : 0; }
            }

            /// <summary>
            /// The raw string with placeholders.
            /// </summary>
            public StringConstantExpression Format { get; set; }

            /// <summary>
            /// The condition when the string should be displayed.
            /// </summary>
            /// <remarks>If null, this is the default case.</remarks>
            public Trigger Condition { get; set; }

            private class Parameter
            {
                public RichPresenceMacroExpressionBase Macro { get; set; }
                public StringConstantExpression Constant { get; set; }
                public Value Value { get; set; }
            }

            public void AddParameter(int index, RichPresenceMacroExpressionBase macro, Value value)
            {
                if (_parameters == null)
                    _parameters = new Dictionary<int, Parameter>();

                _parameters[index] = new Parameter { Macro = macro, Value = value };
            }

            public void AddParameter(int index, StringConstantExpression value)
            {
                if (_parameters == null)
                    _parameters = new Dictionary<int, Parameter>();

                _parameters[index] = new Parameter { Constant = value };
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
                    var maxIndex = 0;
                    foreach (var index in _parameters.Keys)
                        maxIndex = Math.Max(index, maxIndex);

                    for (int i = 0; i <= maxIndex; i++)
                        parameters.Entries.Add(null);

                    foreach (var kvp in _parameters)
                    {
                        if (kvp.Value.Macro != null)
                            parameters.Entries[kvp.Key] = kvp.Value.Macro;
                        else
                            parameters.Entries[kvp.Key] = kvp.Value.Constant;
                    }
                }

                var result = FormatFunction.Evaluate(Format, parameters, ignoreMissing,
                    (StringBuilder builder, int index, ExpressionBase parameter) => ProcessParameter(builder, index, parameter, serializationContext));

                var stringResult = result as StringConstantExpression;
                return (stringResult != null) ? stringResult.Value : "";
            }

            private ErrorExpression ProcessParameter(StringBuilder builder, int index, ExpressionBase parameter, SerializationContext serializationContext)
            {
                var str = parameter as StringConstantExpression;
                if (str != null)
                {
                    str.AppendStringLiteral(builder);
                    return null;
                }

                var param = _parameters[index];
                if (param.Macro != null)
                {
                    SerializationContext useSerializationContext = serializationContext;

                    if (serializationContext.MinimumVersion >= Data.Version._0_77)
                    {
                        if (param.Value.Values.Count() == 1 &&
                            param.Value.Values.First().Requirements.Count() == 1 &&
                            !param.Value.Values.First().Requirements.First().IsComparison)
                        {
                            // single field lookup - force legacy format, even if using sizes only available in 0.77+
                            useSerializationContext = serializationContext.WithVersion(Data.Version._0_76);
                        }
                        else if (param.Value.MinimumVersion() < Data.Version._0_77)
                        {
                            // simple AddSource chain, just use legacy format
                            useSerializationContext = serializationContext.WithVersion(Data.Version._0_76);
                        }
                    }

                    builder.Append('@');
                    builder.Append(param.Macro.Name.Value);
                    builder.Append('(');
                    builder.Append(param.Value.Serialize(useSerializationContext));
                    builder.Append(')');
                    return null;
                }

                builder.Append('{');
                builder.Append(index);
                builder.Append('}');
                return null;
            }

            public bool UsesMacro(string macroName)
            {
                if (_parameters != null)
                {
                    foreach (var kvp in _parameters)
                    {
                        if (kvp.Value.Macro != null && kvp.Value.Macro.Name.Value == macroName)
                            return true;
                    }
                }

                return false;
            }

            public SoftwareVersion MinimumVersion()
            {
                var minimumVersion = (Condition != null) ? Condition.MinimumVersion() : Data.Version.MinimumVersion;
                if (_parameters != null)
                {
                    foreach (var parameter in _parameters.Values)
                    {
                        if (parameter.Value != null)
                            minimumVersion = minimumVersion.OrNewer(parameter.Value.MinimumVersion());
                    }
                }

                return minimumVersion;
            }

            public uint MaximumAddress()
            {
                uint maximumAddress = (Condition != null) ? Condition.MaximumAddress() : 0;
                if (_parameters != null)
                {
                    foreach (var parameter in _parameters.Values)
                    {
                        if (parameter.Value != null)
                            maximumAddress = Math.Max(maximumAddress, parameter.Value.MaximumAddress());
                    }
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
            return RichPresenceValueExpression.GetFormatString(format);
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

            var lookupDict = new Dictionary<int, string>();
            foreach (var entry in dict.Entries)
            {
                var key = entry.Key as IntegerConstantExpression;
                if (key == null)
                    return new ErrorExpression("key is not an integer", entry.Key);

                var value = entry.Value as StringConstantExpression;
                if (value == null)
                    return new ErrorExpression("value is not a string", entry.Value);

                lookupDict[key.Value] = value.Value;
            }

            _lookupFields[name.Value] = new Lookup
            {
                Func = func,
                Entries = lookupDict,
                Fallback = fallback
            };

            return null;
        }

        internal ConditionalDisplayString AddDisplayString(Trigger condition, StringConstantExpression formatString)
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
            var defaultDisplayString = _displayStrings.FirstOrDefault(d => d.Condition == null);
            if (defaultDisplayString == null || String.IsNullOrEmpty(defaultDisplayString.Format?.Value))
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

        private bool IsMacroUsed(string macroName)
        {
            foreach (var displayString in _displayStrings)
            {
                if (displayString.UsesMacro(macroName))
                    return true;
            }

            return false;
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
                if (!IsMacroUsed(lookup.Key))
                    continue;

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

                if (!IsMacroUsed(value.Key))
                    continue;

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

        private static void AppendRichPresenceLookupEntries(StringBuilder builder, IDictionary<int, string> entries, SerializationContext serializationContext, string fallback)
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
            // only keep one default display string
            if (from._displayStrings.Any(d => d.Condition == null))
                _displayStrings.RemoveAll(d => d.Condition == null);

            _displayStrings.AddRange(from._displayStrings);

            foreach (var kvp in from._valueFields)
            {
                ValueField field;
                if (!_valueFields.TryGetValue(kvp.Key, out field))
                    _valueFields[kvp.Key] = kvp.Value;
                else if (field.Format != kvp.Value.Format)
                    return new ErrorExpression("Multiple rich_presence_value calls with the same name must have the same format", field.Func);
            }

            foreach (var kvp in from._lookupFields)
            {
                Lookup existing;
                if (!_lookupFields.TryGetValue(kvp.Key, out existing))
                {
                    _lookupFields[kvp.Key] = kvp.Value;
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
