using Jamiras.Components;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class DictionaryExpression : ExpressionBase, INestedExpressions, IIterableExpression
    {
        public DictionaryExpression()
            : base(ExpressionType.Dictionary)
        {
            _entries = new List<DictionaryEntry>();
            _state = DictionaryState.ConstantSorted;
        }

        private readonly List<DictionaryEntry> _entries;

        private enum DictionaryState
        {
            Unprocessed = 0,
            ConstantSorted,
            ConstantKeysSorted,
            DynamicKeysUnsorted,
        }
        private DictionaryState _state;

        internal void MarkUnprocessed()
        {
            _state = DictionaryState.Unprocessed;
        }

        /// <summary>
        /// Gets the keys for the items in the dictionary.
        /// </summary>
        public IEnumerable<ExpressionBase> Keys
        {
            get
            {
                foreach (var entry in _entries)
                    yield return entry.Key;
            }
        }

        /// <summary>
        /// Gets an enumerator for the items in the dictionary.
        /// </summary>
        public IEnumerable<KeyValuePair<ExpressionBase, ExpressionBase>> Entries
        {
            get
            {
                foreach (var entry in _entries)
                    yield return new KeyValuePair<ExpressionBase, ExpressionBase>(entry.Key, entry.Value);
            }
        }

        private DictionaryEntry GetEntry(ExpressionBase key, bool createIfNotFound)
        {
            if (_state == DictionaryState.Unprocessed)
            {
                var error = UpdateState();
                if (error != null)
                    return new DictionaryEntry { Value = error };
            }

            if (_state == DictionaryState.DynamicKeysUnsorted)
            {
                foreach (var entry in _entries)
                {
                    if (entry.Key == key)
                        return entry;
                }

                if (createIfNotFound)
                {
                    var entry = new DictionaryEntry { Key = key };
                    _entries.Add(entry);
                    return entry;
                }
            }
            else
            {
                var entry = new DictionaryEntry { Key = key };
                var comparer = (IComparer<DictionaryEntry>)entry;
                var index = _entries.BinarySearch(entry, comparer);
                if (index >= 0)
                    return _entries[index];

                if (createIfNotFound)
                {
                    _entries.Insert(~index, entry);
                    if (!key.IsConstant)
                        _state = DictionaryState.DynamicKeysUnsorted;

                    return entry;
                }
            }

            return null;
        }

        internal ExpressionBase GetEntry(ExpressionBase key)
        {
            var entry = GetEntry(key, false);
            return (entry != null) ? entry.Value : null;
        }

        internal ParseErrorExpression Assign(ExpressionBase key, ExpressionBase value)
        {
            var entry = GetEntry(key, true);

            var error = entry.Value as ParseErrorExpression;
            if (error != null)
                return error;

            entry.Value = value;

            if (_state == DictionaryState.ConstantSorted && !value.IsConstant)
                _state = DictionaryState.ConstantKeysSorted;

            return null;
        }

        /// <summary>
        /// Gets the number of entries in the dictionary.
        /// </summary>
        public int Count
        {
            get { return _entries.Count; }
        }

        /// <summary>
        /// Helper function for unit tests
        /// </summary>
        internal KeyValuePair<ExpressionBase, ExpressionBase> this[int index]
        {
            get
            {
                var entry = _entries[index];
                return new KeyValuePair<ExpressionBase, ExpressionBase>(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append('{');

            if (_entries.Count > 0)
            {
                foreach (var entry in _entries)
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

        internal static new ExpressionBase Parse(PositionalTokenizer tokenizer)
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
            _entries.Add(new DictionaryEntry { Key = key, Value = value });
            _state = DictionaryState.Unprocessed;
        }

        private ParseErrorExpression UpdateState()
        {
            if (_entries.Count == 0)
            {
                _state = DictionaryState.ConstantSorted;
            }
            else if (_entries.TrueForAll(e => e.Key.IsConstant))
            {
                // sort by key
                var comparer = (IComparer<DictionaryEntry>)_entries[0];
                _entries.Sort(comparer);

                // check for duplicates
                for (int i = 0; i < _entries.Count - 1; i++)
                {
                    if (comparer.Compare(_entries[i], _entries[i + 1]) == 0)
                    {
                        var entry = _entries[i + 1];
                        StringBuilder builder = new StringBuilder();
                        entry.Key.AppendString(builder);
                        builder.Append(" already exists in dictionary");
                        return new ParseErrorExpression(builder.ToString(), entry.Key);
                    }
                }

                // check for constant values
                if (_entries.TrueForAll(e => e.Value.IsConstant))
                    _state = DictionaryState.ConstantSorted;
                else
                    _state = DictionaryState.ConstantKeysSorted;
            }
            else
            {
                _state = DictionaryState.DynamicKeysUnsorted;
            }

            return null;
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
            var newDict = new DictionaryExpression();
            var entries = newDict._entries;

            if (_state == DictionaryState.Unprocessed)
            {
                result = UpdateState();
                if (result != null)
                    return false;
            }

            // constant dictionary
            if (_state == DictionaryState.ConstantSorted)
            {
                entries.AddRange(_entries);
                result = newDict;
                CopyLocation(result);
                return true;
            }

            // non-constant dictionary - have to evaluate
            var dictScope = new InterpreterScope(scope);

            foreach (var entry in _entries)
            {
                ExpressionBase key, value;
                key = entry.Key;

                if (!key.IsConstant)
                {
                    dictScope.Context = new AssignmentExpression(new VariableExpression("@key"), key);
                    if (!key.ReplaceVariables(dictScope, out value))
                    {
                        result = value;
                        return false;
                    }

                    if (!value.IsConstant)
                    {
                        result = new ParseErrorExpression("Dictionary key must evaluate to a constant", key);
                        return false;
                    }

                    key = value;
                }

                if (entry.Value.IsConstant)
                {
                    value = entry.Value;
                }
                else
                {
                    if (key.Type == ExpressionType.IntegerConstant)
                        dictScope.Context = new AssignmentExpression(new VariableExpression("[" + ((IntegerConstantExpression)key).Value.ToString() + "]"), entry.Value);
                    else // key.Type == ExpressionType.StringConstant
                        dictScope.Context = new AssignmentExpression(new VariableExpression("[" + ((StringConstantExpression)key).Value + "]"), entry.Value);

                    if (!entry.Value.ReplaceVariables(dictScope, out value))
                    {
                        result = value;
                        return false;
                    }
                }

                var newEntry = new DictionaryEntry { Key = key, Value = value };

                if (_state == DictionaryState.ConstantKeysSorted)
                {
                    entries.Add(newEntry);
                }
                else
                {
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
            }

            newDict._state = DictionaryState.ConstantSorted;
            result = newDict;
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
            if (that == null || _entries.Count != that._entries.Count)
                return false;

            if (ReferenceEquals(_entries, that._entries))
                return true;

            var unmatched = new List<DictionaryEntry>(that._entries);
            foreach (var kvp in _entries)
            {
                int i = unmatched.FindIndex(e => e.Key == kvp.Key);
                if (i == -1|| unmatched[i].Value != kvp.Value)
                    return false;

                unmatched.RemoveAt(i);
            }

            return true;
        }

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                foreach (var entry in _entries)
                {
                    yield return entry.Key;
                    yield return entry.Value;
                }
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            foreach (var entry in _entries)
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

        IEnumerable<ExpressionBase> IIterableExpression.IterableExpressions()
        {
            return Keys;
        }

        [DebuggerDisplay("{Key}: {Value}")]
        private class DictionaryEntry : IComparer<DictionaryEntry>
        {
            /// <summary>
            /// Gets or sets the key.
            /// </summary>
            public ExpressionBase Key { get; set; }

            /// <summary>
            /// Gets or sets the value.
            /// </summary>
            public ExpressionBase Value { get; set; }

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

                    case ExpressionType.BooleanConstant:
                        return (((BooleanConstantExpression)x.Key).Value ? 1 : 0) -
                            (((BooleanConstantExpression)y.Key).Value ? 1 : 0);

                    default:
                        return 0;
                }
            }
        }
    }
}
