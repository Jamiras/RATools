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

                dict.Entries.Add(new DictionaryExpression.DictionaryEntry { Key = key, Value = value });

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

            var dictScope = new InterpreterScope(scope);

            var entries = new List<DictionaryEntry>();
            foreach (var entry in Entries)
            {
                ExpressionBase key, value;
                key = entry.Key;

                dictScope.Context = new AssignmentExpression(new VariableExpression("@key"), key);

                if (key.Type == ExpressionType.FunctionCall)
                {
                    var expression = (FunctionCallExpression)key;
                    if (!expression.ReplaceVariables(dictScope, out value))
                    {
                        result = value;
                        return false;
                    }

                    key = value;
                }

                if (!key.ReplaceVariables(dictScope, out key))
                {
                    result = key;
                    return false;
                }

                switch (key.Type)
                {
                    case ExpressionType.StringConstant:
                        dictScope.Context = new AssignmentExpression(new VariableExpression("[" + ((StringConstantExpression)key).Value + "]"), entry.Value);
                        break;

                    case ExpressionType.IntegerConstant:
                        dictScope.Context = new AssignmentExpression(new VariableExpression("[" + ((IntegerConstantExpression)key).Value.ToString() + "]"), entry.Value);
                        break;

                    default:
                        result = new ParseErrorExpression("Dictionary key must evaluate to a constant", key);
                        return false;
                }

                if (!entry.Value.ReplaceVariables(dictScope, out value))
                {
                    result = value;
                    return false;
                }

                if (entries.Exists(e => e.Key == key))
                {
                    StringBuilder builder = new StringBuilder();
                    key.AppendString(builder);
                    builder.Append(" already exists in dictionary");
                    result = new ParseErrorExpression(builder.ToString(), entry.Key);
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
            public virtual ExpressionBase Value { get; set; }
        }
    }
}
