using Jamiras.Components;
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

        internal static ExpressionBase Parse(PositionalTokenizer tokenizer, int line = 0, int column = 0)
        {
            SkipWhitespace(tokenizer);

            var dict = new DictionaryExpression();
            while (tokenizer.NextChar != '}')
            {
                var key = ExpressionBase.ParseClause(tokenizer);
                if (key.Type == ExpressionType.ParseError)
                    return key;

                SkipWhitespace(tokenizer);
                if (tokenizer.NextChar != ':')
                {
                    ParseError(tokenizer, "Expecting colon following key expression");
                    break;
                }
                tokenizer.Advance();
                SkipWhitespace(tokenizer);

                var value = ExpressionBase.ParseClause(tokenizer);
                if (value.Type == ExpressionType.ParseError)
                    break;

                dict.Add(key, value);

                SkipWhitespace(tokenizer);
                if (tokenizer.NextChar == '}')
                    break;

                if (tokenizer.NextChar != ',')
                {
                    ParseError(tokenizer, "Expecting comma between entries");
                    break;
                }
                tokenizer.Advance();
                SkipWhitespace(tokenizer);
            }

            tokenizer.Advance();
            return dict;
        }

        /// <summary>
        /// Adds an entry to the dictionary.
        /// </summary>
        /// <remarks>Does not check for duplicate keys.</remarks>
        public void Add(ExpressionBase key, ExpressionBase value)
        {
            Entries.Add(new DictionaryEntry { Key = key, Value = value });
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
                result = new DictionaryExpression { Entries = new List<DictionaryEntry>() };
                CopyLocation(result);
                return true;
            }

            var dictScope = new InterpreterScope(scope);

            var entries = new List<DictionaryEntry>();
            foreach (var entry in Entries)
            {
                ExpressionBase key, value;
                key = entry.Key;

                switch (key.Type)
                {
                    case ExpressionType.StringConstant:
                    case ExpressionType.IntegerConstant:
                        // simple data types, do nothing
                        break;

                    default:
                        dictScope.Context = new AssignmentExpression(new VariableExpression("@key"), key);
                        if (!key.ReplaceVariables(dictScope, out value))
                        {
                            result = value;
                            return false;
                        }

                        if (value.Type != ExpressionType.StringConstant && value.Type != ExpressionType.IntegerConstant)
                        {
                            result = new ParseErrorExpression("Dictionary key must evaluate to a constant", key);
                            return false;
                        }

                        key = value;
                        break;
                }

                switch (entry.Value.Type)
                {
                    case ExpressionType.StringConstant:
                    case ExpressionType.IntegerConstant:
                        // simple data types, avoid overhead of generating an AssignmentExpression
                        value = entry.Value;
                        break;

                    default:
                        if (key.Type == ExpressionType.IntegerConstant)
                            dictScope.Context = new AssignmentExpression(new VariableExpression("[" + ((IntegerConstantExpression)key).Value.ToString() + "]"), entry.Value);
                        else // key.Type == ExpressionType.StringConstant
                            dictScope.Context = new AssignmentExpression(new VariableExpression("[" + ((StringConstantExpression)key).Value + "]"), entry.Value);

                        if (!entry.Value.ReplaceVariables(dictScope, out value))
                        {
                            result = value;
                            return false;
                        }
                        break;
                }

                var newEntry = new DictionaryEntry { Key = key, Value = value };

                var index = entries.BinarySearch(newEntry, newEntry);
                if (index >= 0)
                {
                    StringBuilder builder = new StringBuilder();
                    key.AppendString(builder);
                    builder.Append(" already exists in dictionary");
                    result = new ParseErrorExpression(builder.ToString(), entry.Key);
                    return false;
                }

                entries.Insert(~index, newEntry);
            }

            result = new DictionaryExpression { Entries = entries };
            CopyLocation(result);
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
            var that = obj as DictionaryExpression;
            return that != null && Entries == that.Entries;
        }

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                foreach (var entry in Entries)
                {
                    yield return entry.Key;
                    yield return entry.Value;
                }
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            foreach (var entry in Entries)
            {
                var nested = entry.Key as INestedExpressions;
                if (nested != null)
                    nested.GetDependencies(dependencies);

                nested = entry.Value as INestedExpressions;
                if (nested != null)
                    nested.GetDependencies(dependencies);
            }
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
        }

        [DebuggerDisplay("{Key}: {Value}")]
        public class DictionaryEntry : IComparer<DictionaryEntry>
        {
            /// <summary>
            /// Gets or sets the key.
            /// </summary>
            public ExpressionBase Key { get; set; }

            /// <summary>
            /// Gets or sets the value.
            /// </summary>
            public virtual ExpressionBase Value { get; set; }

            int IComparer<DictionaryEntry>.Compare(DictionaryEntry x, DictionaryEntry y)
            {
                if (x.Key.Type != y.Key.Type)
                    return ((int)x.Key.Type - (int)y.Key.Type);

                switch (x.Key.Type)
                {
                    case ExpressionType.IntegerConstant:
                        return ((IntegerConstantExpression)x.Key).Value -
                            ((IntegerConstantExpression)y.Key).Value;

                    case ExpressionType.StringConstant:
                        return string.Compare(((StringConstantExpression)x.Key).Value,
                            ((StringConstantExpression)y.Key).Value);

                    default:
                        return 0;
                }
            }
        }
    }
}
