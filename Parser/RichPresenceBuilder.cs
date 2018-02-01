using Jamiras.Components;
using RATools.Parser.Internal;
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
        public RichPresenceBuilder()
        {
            _valueFields = new List<string>();
            _lookupFields = new TinyDictionary<string, IDictionary<int, string>>();
            _conditionalDisplayStrings = new List<string>();
        }

        private List<string> _conditionalDisplayStrings;
        private List<string> _valueFields;
        private TinyDictionary<string, IDictionary<int, string>> _lookupFields;

        public string DisplayString { get; set; }

        public void AddConditionalDisplayString(string condition, string displayString)
        {
            _conditionalDisplayStrings.Add(String.Format("?{0}?{1}", condition, displayString));
        }

        public void AddValueField(string name)
        {
            if (!_valueFields.Contains(name))
                _valueFields.Add(name);
        }

        public void AddLookupField(string name, IDictionary<int, string> dict)
        {
            _lookupFields[name] = dict;
        }

        internal ParseErrorExpression AddLookupField(string name, DictionaryExpression dict)
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

            AddLookupField(name, tinyDict);
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

                var list = new List<int>(lookup.Value.Keys);
                list.Sort();

                foreach (var key in list)
                {
                    builder.Append(key);
                    builder.Append('=');
                    builder.AppendLine(lookup.Value[key]);
                }

                builder.AppendLine();
            }

            foreach (var value in _valueFields)
            {
                builder.Append("Format:");
                builder.AppendLine(value);
                builder.AppendLine("FormatType=VALUE");
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
