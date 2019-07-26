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

        internal ParseErrorExpression AddLookupField(string name, DictionaryExpression dict)
        {
            var tinyDict = new TinyDictionary<int, string>();
            string fallback = null;
            foreach (var entry in dict.Entries)
            {
                var value = entry.Value as StringConstantExpression;
                if (value == null)
                    return new ParseErrorExpression("value is not a string", entry.Value);

                var key = entry.Key as IntegerConstantExpression;
                if (key != null)
                    tinyDict[key.Value] = value.Value;
                else
                {
                    var strKey = entry.Key as StringConstantExpression;
                    if (strKey == null || strKey.Value != "*")
                        return new ParseErrorExpression("key is not an integer or \"*\"", entry.Key);
                    fallback = value.Value;
                }
            }

            AddLookupField(name, tinyDict, fallback);
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

                if (lookup.Value.Fallback != null)
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
                switch (value.Value)
                {
                    case ValueFormat.Value:
                        builder.AppendLine("VALUE");
                        break;

                    case ValueFormat.Score:
                        builder.AppendLine("SCORE");
                        break;

                    case ValueFormat.TimeSecs:
                        builder.AppendLine("SECS");
                        break;

                    case ValueFormat.TimeMillisecs:
                        builder.AppendLine("MILLISECS");
                        break;

                    case ValueFormat.TimeFrames:
                        builder.AppendLine("FRAMES");
                        break;

                    case ValueFormat.Other:
                        builder.AppendLine("OTHER");
                        break;
                }

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
