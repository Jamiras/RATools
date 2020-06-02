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
    public class RichPresenceBuilder
    {
        private class Lookup
        {
            public Lookup(IDictionary<int, string> dict, string fallback)
            {
                Dict = dict;
                Fallback = fallback;
            }

            public IDictionary<int, string> Dict { get; private set; }
            public string Fallback { get; private set; }
        };

        public RichPresenceBuilder()
        {
            _valueFields = new TinyDictionary<string, ValueFormat>();
            _lookupFields = new TinyDictionary<string, Lookup>();
            _conditionalDisplayStrings = new List<string>();
        }

        private List<string> _conditionalDisplayStrings;
        private TinyDictionary<string, ValueFormat> _valueFields;
        private TinyDictionary<string, Lookup> _lookupFields;

        public string DisplayString { get; set; }
        public int Line { get; set; }

        public void AddConditionalDisplayString(string condition, string displayString)
        {
            _conditionalDisplayStrings.Add(String.Format("?{0}?{1}", condition, displayString));
        }

        public void AddValueField(string name, ValueFormat format)
        {
            _valueFields[name] = format;
        }

        public void AddLookupField(string name, IDictionary<int, string> dict, string fallback)
        {
            _lookupFields[name] = new Lookup(dict, fallback);
        }

        internal ParseErrorExpression AddLookupField(string name, DictionaryExpression dict, ExpressionBase fallback)
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

            var fallbackValue = fallback as StringConstantExpression;
            if (fallbackValue == null)
                return new ParseErrorExpression("Fallback value is not a string", fallback);

            AddLookupField(name, tinyDict, fallbackValue.Value);
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

                var list = new List<int>(lookup.Value.Dict.Keys);
                list.Sort();

                foreach (var key in list)
                {
                    builder.Append(key);
                    builder.Append('=');
                    builder.AppendLine(lookup.Value.Dict[key]);
                }

                if (lookup.Value.Fallback.Length > 0)
                {
                    builder.Append("*=");
                    builder.AppendLine(lookup.Value.Fallback);
                }

                builder.AppendLine();
            }

            foreach (var value in _valueFields)
            {
                builder.Append("Format:");
                builder.AppendLine(value.Key);

                builder.Append("FormatType=");
                builder.AppendLine(Leaderboard.GetFormatString(value.Value));

                builder.AppendLine();
            }

            builder.AppendLine("Display:");
            foreach (var conditionalString in _conditionalDisplayStrings)
                builder.AppendLine(conditionalString);
            builder.AppendLine(DisplayString);

            return builder.ToString();
        }

    }
}
