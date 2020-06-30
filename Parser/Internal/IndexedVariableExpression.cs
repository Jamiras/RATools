using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace RATools.Parser.Internal
{
    internal class IndexedVariableExpression : VariableExpression, INestedExpressions
    {
        public IndexedVariableExpression(ExpressionBase variable, ExpressionBase index)
            : base(String.Empty)
        {
            Variable = variable;
            Index = index;

            // assert: Name is not used for IndexedVariableExpression
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
            Variable.AppendString(builder);

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
            return result.ReplaceVariables(scope, out result);
        }

        internal DictionaryExpression.DictionaryEntry GetDictionaryEntry(InterpreterScope scope, out ExpressionBase result, bool create)
        {
            ExpressionBase index;
            if (Index.Type == ExpressionType.FunctionCall)
            {
                var expression = (FunctionCallExpression)Index;
                if (!expression.ReplaceVariables(scope, out index))
                {
                    result = index;
                    return null;
                }
            }
            else if (!Index.ReplaceVariables(scope, out index))
            {
                result = index;
                return null;
            }

            ExpressionBase value;
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

                    if (value == null)
                    {
                        result = new ParseErrorExpression("Unknown variable: " + variable.Name, variable);
                        return null;
                    }
                }
                else if (!Variable.ReplaceVariables(scope, out value))
                {
                    result = value;
                    return null;
                }
            }

            var dict = value as DictionaryExpression;
            if (dict != null)
            {
                var entry = new DictionaryExpression.DictionaryEntry() { Key = index };
                var entryIndex = dict.Entries.BinarySearch(entry, entry);
                if (entryIndex >= 0)
                {
                    result = dict;
                    return dict.Entries[entryIndex];
                }

                if (create)
                {
                    dict.Entries.Insert(~entryIndex, entry);
                    result = dict;
                    return entry;
                }

                var builder = new StringBuilder();
                builder.Append("No entry in dictionary for key: ");
                index.AppendString(builder);
                result = new ParseErrorExpression(builder.ToString(), Index);
            }
            else
            {
                var array = value as ArrayExpression;
                if (array != null)
                {
                    var intIndex = index as IntegerConstantExpression;
                    if (intIndex == null)
                    {
                        result = new ParseErrorExpression("Index does not evaluate to an integer constant", index);
                    }
                    else if (intIndex.Value < 0 || intIndex.Value >= array.Entries.Count)
                    {
                        result = new ParseErrorExpression(String.Format("Index {0} not in range 0-{1}", intIndex.Value, array.Entries.Count - 1), index);
                    }
                    else
                    {
                        result = array;
                        return new ArrayDictionaryEntryWrapper { Array = array, Key = index, Value = array.Entries[intIndex.Value] };
                    }
                }
                else
                {
                    var builder = new StringBuilder();
                    builder.Append("Cannot index: ");
                    Variable.AppendString(builder);
                    builder.Append(" (");
                    builder.Append(value.Type);
                    builder.Append(')');
                    result = new ParseErrorExpression(builder.ToString(), Variable);
                }
            }

            return null;
        }

        private class ArrayDictionaryEntryWrapper : DictionaryExpression.DictionaryEntry
        {
            public ArrayExpression Array { get; set; }
            public override ExpressionBase Value
            {
                get { return Array.Entries[((IntegerConstantExpression)Key).Value]; }
                set { Array.Entries[((IntegerConstantExpression)Key).Value] = value; }
            }
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

        bool INestedExpressions.GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            return ExpressionGroup.GetExpressionsForLine(expressions, new[] { Variable, Index }, line);
        }
    }
}
