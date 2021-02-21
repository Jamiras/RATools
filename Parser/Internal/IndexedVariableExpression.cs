using System;
using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class IndexedVariableExpression : VariableExpression, INestedExpressions
    {
        public IndexedVariableExpression(VariableExpression variable, ExpressionBase index)
            : base(String.Empty)
        {
            Variable = variable;
            Index = index;

            // assert: Name is not used for IndexedVariableExpression
        }

        /// <summary>
        /// Gets the variable expression.
        /// </summary>
        public VariableExpression Variable { get; private set; }

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
            ExpressionBase container, index;
            GetContainerIndex(scope, out container, out index);

            switch (container.Type)
            {
                case ExpressionType.Dictionary:
                    result = ((DictionaryExpression)container).GetEntry(index);
                    if (result == null)
                    {
                        var builder = new StringBuilder();
                        builder.Append("No entry in dictionary for key: ");
                        index.AppendString(builder);
                        result = new ParseErrorExpression(builder.ToString(), Index);
                        return false;
                    }
                    break;

                case ExpressionType.Array:
                    result = ((ArrayExpression)container).Entries[((IntegerConstantExpression)index).Value];
                    break;

                case ExpressionType.ParseError:
                    result = container;
                    return false;

                default:
                    {
                        var builder = new StringBuilder();
                        builder.Append("Cannot index: ");
                        Variable.AppendString(builder);
                        builder.Append(" (");
                        builder.Append(container.Type);
                        builder.Append(')');
                        result = new ParseErrorExpression(builder.ToString(), Variable);
                    }
                    return false;
            }

            if (result == null)
                return false;

            return result.ReplaceVariables(scope, out result);
        }

        public ParseErrorExpression Assign(InterpreterScope scope, ExpressionBase newValue)
        {
            ExpressionBase container, index;
            GetContainerIndex(scope, out container, out index);

            switch (container.Type)
            {
                case ExpressionType.Dictionary:
                    ((DictionaryExpression)container).Assign(index, newValue);
                    break;

                case ExpressionType.Array:
                    ((ArrayExpression)container).Entries[((IntegerConstantExpression)index).Value] = newValue;
                    break;

                case ExpressionType.ParseError:
                    return (ParseErrorExpression)container;

                default:
                    var builder = new StringBuilder();
                    builder.Append("Cannot index: ");
                    Variable.AppendString(builder);
                    builder.Append(" (");
                    builder.Append(container.Type);
                    builder.Append(')');
                    return new ParseErrorExpression(builder.ToString(), Variable);
            }

            return null;
        }

        private void GetContainerIndex(InterpreterScope scope, out ExpressionBase container, out ExpressionBase index)
        {
            if (Index.Type == ExpressionType.FunctionCall)
            {
                var expression = (FunctionCallExpression)Index;
                if (!expression.ReplaceVariables(scope, out index))
                {
                    container = index;
                    return;
                }
            }
            else if (!Index.ReplaceVariables(scope, out index))
            {
                container = index;
                return;
            }

            var indexed = Variable as IndexedVariableExpression;
            if (indexed != null)
            {
                indexed.ReplaceVariables(scope, out container);
                return;
            }

            container = scope.GetVariable(Variable.Name);
            if (container == null)
            {
                container = new UnknownVariableParseErrorExpression("Unknown variable: " + Variable.Name, Variable);
                return;
            }

            var array = container as ArrayExpression;
            if (array != null)
            {
                var intIndex = index as IntegerConstantExpression;
                if (intIndex == null)
                    container = new ParseErrorExpression("Index does not evaluate to an integer constant", index);
                else if (intIndex.Value < 0 || intIndex.Value >= array.Entries.Count)
                    container = new ParseErrorExpression(String.Format("Index {0} not in range 0-{1}", intIndex.Value, array.Entries.Count - 1), index);
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
            var that = obj as IndexedVariableExpression;
            return that != null && Variable == that.Variable && Index == that.Index;
        }

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                yield return Variable;
                yield return Index;
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            var nested = Variable as INestedExpressions;
            if (nested != null)
                nested.GetDependencies(dependencies);

            nested = Index as INestedExpressions;
            if (nested != null)
                nested.GetDependencies(dependencies);
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
        }
    }
}
