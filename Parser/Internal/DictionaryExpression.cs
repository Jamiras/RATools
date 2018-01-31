using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class DictionaryExpression : ExpressionBase, INestedExpressions
    {
        public DictionaryExpression()
            : base(ExpressionType.Dictionary)
        {
            Entries = new List<DictionaryEntry>();
        }

        /// <summary>
        /// Gets the entries in the dictionary.
        /// </summary>
        public List<DictionaryEntry> Entries { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
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

        /// <summary>
        /// Replaces the variables in the expression with values from <paramref name="scope" />.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] The new expression containing the replaced variables.</param>
        /// <returns>
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ParseErrorExpression" />.
        /// </returns>
        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            if (Entries.Count == 0)
            {
                result = this;
                return true;
            }

            var entries = new List<DictionaryEntry>();
            foreach (var entry in Entries)
            {
                ExpressionBase key, value;
                key = entry.Key;

                if (key.Type == ExpressionType.FunctionCall)
                {
                    var expression = (FunctionCallExpression)key;
                    if (!expression.Evaluate(scope, out value))
                    {
                        result = value;
                        return false;
                    }

                    key = value;
                }

                if (!key.ReplaceVariables(scope, out key))
                {
                    result = key;
                    return false;
                }

                if (key.Type != ExpressionType.StringConstant && key.Type != ExpressionType.IntegerConstant)
                {
                    result = new ParseErrorExpression("Dictionary key must evaluate to a constant", key);
                    return false;
                }

                if (!entry.Value.ReplaceVariables(scope, out value))
                {
                    result = value;
                    return false;
                }

                entries.Add(new DictionaryEntry { Key = key, Value = value });
            }

            result = new DictionaryExpression { Entries = entries };
            return true;
        }

        /// <summary>
        /// Determines whether the specified <see cref="DictionaryExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="DictionaryExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="DictionaryExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = (DictionaryExpression)obj;
            return Entries == that.Entries;
        }

        bool INestedExpressions.GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            foreach (var entry in Entries)
            {
                if (line >= entry.Key.Line && line <= entry.Value.EndLine)
                    ExpressionGroup.GetExpressionsForLine(expressions, new[] { entry.Key, entry.Value }, line);
            }

            return true;
        }

        [DebuggerDisplay("{Key}: {Value}")]
        public class DictionaryEntry
        {
            /// <summary>
            /// Gets or sets the key.
            /// </summary>
            public ExpressionBase Key { get; set; }

            /// <summary>
            /// Gets or sets the value.
            /// </summary>
            public ExpressionBase Value { get; set; }
        }
    }
}
