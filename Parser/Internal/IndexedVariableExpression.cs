using System;
using System.Text;
using System.Linq;

namespace RATools.Parser.Internal
{
    internal class IndexedVariableExpression : VariableExpression
    {
        public IndexedVariableExpression(string name, ExpressionBase index)
            : base(name)
        {
            Index = index;
        }

        public IndexedVariableExpression(ExpressionBase variable, ExpressionBase index)
            : this(String.Empty, index)
        {
            Variable = variable;
        }

        /// <summary>
        /// Gets the variable expression.
        /// </summary>
        public ExpressionBase Variable { get; private set; }

        /// <summary>
        /// Gets the index expression.
        /// </summary>
        public ExpressionBase Index { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            if (Variable != null)
                Variable.AppendString(builder);
            else
                builder.Append(Name);

            builder.Append('[');
            Index.AppendString(builder);
            builder.Append(']');
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
            var entry = GetDictionaryEntry(scope, out result, false);
            if (entry == null)
                return false;

            result = entry.Value;
            return true;
        }

        internal DictionaryExpression.DictionaryEntry GetDictionaryEntry(InterpreterScope scope, out ExpressionBase result, bool create)
        {
            ExpressionBase index;
            if (!Index.ReplaceVariables(scope, out index))
            {
                result = index;
                return null;
            }

            ExpressionBase value;
            if (Variable != null)
            {
                var indexed = Variable as IndexedVariableExpression;
                if (indexed != null)
                {
                    var entry = indexed.GetDictionaryEntry(scope, out result, create);
                    if (entry == null)
                        return null;

                    value = entry.Value;
                }
                else
                {
                    var variable = Variable as VariableExpression;
                    if (variable != null)
                    {
                        value = scope.GetVariable(variable.Name);
                    }
                    else if (!Variable.ReplaceVariables(scope, out value))
                    {
                        result = value;
                        return null;
                    }
                }
            }
            else
            {
                value = scope.GetVariable(Name);

                if (value == null)
                {
                    result = new ParseErrorExpression("Unknown variable: " + Name, Line, Column);
                    return null;
                }
            }

            var dict = value as DictionaryExpression;
            if (dict != null)
            {
                var entry = dict.Entries.FirstOrDefault(e => Object.Equals(e.Key, index));
                if (entry != null)
                {
                    result = dict;
                    return entry;
                }

                if (create)
                {
                    entry = new DictionaryExpression.DictionaryEntry { Key = index };
                    dict.Entries.Add(entry);
                    result = dict;
                    return entry;
                }

                var builder = new StringBuilder();
                builder.Append("No entry in dictionary for key: ");
                index.AppendString(builder);
                result = new ParseErrorExpression(builder.ToString());
            }
            else
            {
                var builder = new StringBuilder();
                builder.Append("Cannot index: ");

                if (Variable != null)
                    Variable.AppendString(builder);
                else
                    builder.Append(Name);

                builder.Append(" (");
                builder.Append(value.Type);
                builder.Append(')');
                result = new ParseErrorExpression(builder.ToString());
            }

            return null;
        }

        /// <summary>
        /// Determines whether the specified <see cref="IndexedVariableExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="IndexedVariableExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="IndexedVariableExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = (IndexedVariableExpression)obj;
            return Variable == that.Variable && Index == that.Index;
        }
    }
}
