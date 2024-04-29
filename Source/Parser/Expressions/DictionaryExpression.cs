using Jamiras.Components;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class DictionaryExpression : ExpressionBase, INestedExpressions,
        IIterableExpression, IValueExpression
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

        internal ExpressionBase GetEntry(ExpressionBase key)
        {
            if (_state == DictionaryState.Unprocessed)
            {
                var error = UpdateState();
                if (error != null)
                    return error;
            }

            if (_state == DictionaryState.DynamicKeysUnsorted)
            {
                foreach (var entry in _entries)
                {
                    if (entry.Key == key)
                        return entry.Value;
                }
            }
            else
            {
                var entry = new DictionaryEntry { Key = key, Value = null };
                var index = _entries.BinarySearch(entry, entry);
                if (index >= 0)
                    return _entries[index].Value;
            }

            return null;
        }

        internal ErrorExpression Assign(ExpressionBase key, ExpressionBase value)
        {
            if (_state == DictionaryState.Unprocessed)
            {
                var error = UpdateState();
                if (error != null)
                    return error;
            }

            if (_state == DictionaryState.DynamicKeysUnsorted)
            {
                foreach (var entry in _entries)
                {
                    if (entry.Key == key)
                    {
                        entry.Value = value;
                        return null;
                    }
                }

                _entries.Add(new DictionaryEntry { Key = key, Value = value });
            }
            else
            {
                var entry = new DictionaryEntry { Key = key, Value = value };
                var index = _entries.BinarySearch(entry, entry);
                if (index >= 0)
                {
                    _entries[index].Value = value;
                }
                else
                {
                    _entries.Insert(~index, entry);
                    if (!key.IsConstant)
                        _state = DictionaryState.DynamicKeysUnsorted;
                }

                if (_state == DictionaryState.ConstantSorted && !value.IsConstant)
                    _state = DictionaryState.ConstantKeysSorted;
            }

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
                var key = ParseValueClause(tokenizer);
                if (key is not IValueExpression)
                {
                    if (key.Type == ExpressionType.Error)
                        return key;
                    return new ErrorExpression("Invalid dictionary key", key);
                }

                SkipWhitespace(tokenizer);
                if (tokenizer.NextChar != ':')
                    return ParseError(tokenizer, "Expecting colon following key expression");
                tokenizer.Advance();
                SkipWhitespace(tokenizer);

                var value = ParseValueClause(tokenizer);
                if (value is not IValueExpression)
                {
                    if (value.Type == ExpressionType.Error)
                        return value;
                    return new ErrorExpression("Invalid dictionary value", value);
                }

                dict.Add(key, value);

                SkipWhitespace(tokenizer);
                if (tokenizer.NextChar == '}')
                    break;

                if (tokenizer.NextChar != ',')
                    return ParseError(tokenizer, "Expecting comma between entries");
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

        private ErrorExpression UpdateState()
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
                        return new ErrorExpression(builder.ToString(), entry.Key);
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
        /// Evaluates an expression
        /// </summary>
        /// <returns><see cref="ErrorExpression"/> indicating the failure, or the result of evaluating the expression.</returns>
        public ExpressionBase Evaluate(InterpreterScope scope)
        {
            ExpressionBase result;
            ReplaceVariables(scope, out result);
            return result;
        }

        /// <summary>
        /// Replaces the variables in the expression with values from <paramref name="scope" />.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] The new expression containing the replaced variables.</param>
        /// <returns>
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ErrorExpression" />.
        /// </returns>
        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            if (_state == DictionaryState.Unprocessed)
            {
                result = UpdateState();
                if (result != null)
                    return false;
            }

            var newDict = new DictionaryExpression();
            var entries = newDict._entries;
            entries.Capacity = (_entries.Count + 3) & ~3;

            // constant dictionary
            if (_state == DictionaryState.ConstantSorted)
            {
                entries.AddRange(_entries);
                result = newDict;
                CopyLocation(result);
                return true;
            }

            // non-constant dictionary - have to evaluate
            foreach (var entry in _entries)
            {
                ExpressionBase key = entry.Key, value = entry.Value;

                if (!key.IsConstant)
                {
                    var valueExpression = key as IValueExpression;
                    if (valueExpression != null)
                    {
                        key = valueExpression.Evaluate(scope);
                        if (key is ErrorExpression)
                        {
                            result = key;
                            return false;
                        }
                    }

                    if (key is not LiteralConstantExpressionBase)
                    {
                        result = new ErrorExpression("Dictionary key must evaluate to a string or numeric constant", key);
                        return false;
                    }
                }

                if (!value.IsConstant)
                {
                    var valueExpression = entry.Value as IValueExpression;
                    if (valueExpression != null)
                    {
                        value = valueExpression.Evaluate(scope);
                        if (value is ErrorExpression)
                        {
                            result = value;
                            return false;
                        }
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
                        result = new ErrorExpression(builder.ToString(), entry.Key);
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
                if (i == -1 || unmatched[i].Value != kvp.Value)
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
                    return (int)x.Key.Type - (int)y.Key.Type;

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

                    case ExpressionType.FloatConstant:
                        var xValue = ((FloatConstantExpression)x.Key).Value;
                        var yValue = ((FloatConstantExpression)y.Key).Value;
                        return xValue == yValue ? 0 : xValue < yValue ? -1 : 1;

                    default:
                        return 0;
                }
            }
        }
    }
}
