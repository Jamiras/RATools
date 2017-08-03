using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class DictionaryExpression : ExpressionBase
    {
        public DictionaryExpression()
            : base(ExpressionType.Dictionary)
        {
            Entries = new List<DictionaryEntry>();
        }

        public List<DictionaryEntry> Entries { get; private set; }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append('{');

            if (Entries.Count > 0)
            {
                foreach (var entry in Entries)
                {
                    entry.Key.AppendString(builder);
                    builder.Append(": ");
                    entry.Value.AppendString(builder);
                    builder.Append(", ");
                }
                builder.Length -= 2;
            }

            builder.Append('}');
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var entries = new List<DictionaryEntry>();
            foreach (var entry in Entries)
            {
                ExpressionBase key, value;
                if (!entry.Key.ReplaceVariables(scope, out key))
                {
                    result = key;
                    return false;
                }

                if (key.Type != ExpressionType.StringConstant && key.Type != ExpressionType.IntegerConstant)
                {
                    result = new ParseErrorExpression("Dictionary key must evaluate to a constant", key.Line, key.Column);
                    return false;
                }

                if (!entry.Value.ReplaceVariables(scope, out value))
                {
                    result = value;
                    return false;
                }

                entries.Add(new DictionaryEntry { Key = key, Value = value });
            }

            if (entries.Count == 0)
            {
                result = this;
                return true;
            }

            result = new DictionaryExpression { Entries = entries };
            return true;
        }

        [DebuggerDisplay("{Key}: {Value}")]
        public class DictionaryEntry
        {
            public ExpressionBase Key { get; set; }
            public ExpressionBase Value { get; set; }
        }
    }
}
