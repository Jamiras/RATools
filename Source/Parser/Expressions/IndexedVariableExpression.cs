﻿using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class IndexedVariableExpression : VariableExpression, INestedExpressions
    {
        public IndexedVariableExpression(VariableExpression variable, ExpressionBase index)
            : base(string.Empty)
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
        /// Gets the un-evaluated value at the referenced index.
        /// </summary>
        public override ExpressionBase GetValue(InterpreterScope scope)
        {
            StringBuilder builder;
            ExpressionBase container, index, result;

            GetContainerIndex(scope, out container, out index);

            switch (container.Type)
            {
                case ExpressionType.Dictionary:
                    result = ((DictionaryExpression)container).GetEntry(index);
                    if (result != null)
                        return result;

                    builder = new StringBuilder();
                    builder.Append("No entry in dictionary for key: ");
                    index.AppendString(builder);
                    return new ErrorExpression(builder.ToString(), Index);

                case ExpressionType.Array:
                    // ASSERT: index was validated in GetContainerIndex
                    return ((ArrayExpression)container).Entries[((IntegerConstantExpression)index).Value];

                case ExpressionType.Error:
                    return container;

                default:
                    builder = new StringBuilder();
                    builder.Append("Cannot index: ");
                    Variable.AppendString(builder);
                    builder.Append(" (");
                    builder.Append(container.Type);
                    builder.Append(')');
                    return new ErrorExpression(builder.ToString(), Variable);
            }
        }

        public ErrorExpression Assign(InterpreterScope scope, ExpressionBase newValue)
        {
            ExpressionBase container, index;
            GetContainerIndex(scope, out container, out index);

            switch (container.Type)
            {
                case ExpressionType.Dictionary:
                    ((DictionaryExpression)container).Assign(index, newValue);
                    break;

                case ExpressionType.Array:
                    // ASSERT: index was validated in GetContainerIndex
                    ((ArrayExpression)container).Entries[((IntegerConstantExpression)index).Value] = newValue;
                    break;

                case ExpressionType.Error:
                    return (ErrorExpression)container;

                default:
                    var builder = new StringBuilder();
                    builder.Append("Cannot index: ");
                    Variable.AppendString(builder);
                    builder.Append(" (");
                    builder.Append(container.Type);
                    builder.Append(')');
                    return new ErrorExpression(builder.ToString(), Variable);
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

            container = Variable.GetValue(scope);
            if (container is ErrorExpression)
                return;

            var variableReference = container as VariableReferenceExpression;
            if (variableReference != null)
                container = variableReference.Expression;

            var array = container as ArrayExpression;
            if (array != null)
            {
                var intIndex = index as IntegerConstantExpression;
                if (intIndex == null)
                    container = new ErrorExpression("Index does not evaluate to an integer constant", index);
                else if (intIndex.Value < 0 || intIndex.Value >= array.Entries.Count)
                    container = new ErrorExpression(string.Format("Index {0} not in range 0-{1}", intIndex.Value, array.Entries.Count - 1), index);
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
