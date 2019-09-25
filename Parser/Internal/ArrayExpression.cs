﻿using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Internal
{
    internal class ArrayExpression : ExpressionBase, INestedExpressions
    {
        public ArrayExpression()
            : base(ExpressionType.Array)
        {
            Entries = new List<ExpressionBase>();
        }

        /// <summary>
        /// Gets the entries in the array.
        /// </summary>
        public List<ExpressionBase> Entries { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append('[');

            if (Entries.Count > 0)
            {
                foreach (var entry in Entries)
                {
                    entry.AppendString(builder);
                    builder.Append(", ");
                }
                builder.Length -= 2;
            }

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
            if (Entries.Count == 0)
            {
                result = new ArrayExpression { Entries = new List<ExpressionBase>() };
                CopyLocation(result);
                return true;
            }

            var arrayScope = new InterpreterScope(scope);

            int index = 0;
            var entries = new List<ExpressionBase>();
            foreach (var entry in Entries)
            {
                ExpressionBase value;
                arrayScope.Context = new AssignmentExpression(new VariableExpression("[" + (index++) + "]"), entry);
                if (!entry.ReplaceVariables(arrayScope, out value))
                {
                    result = value;
                    return false;
                }

                entries.Add(value);
            }

            result = new ArrayExpression { Entries = entries };
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
            var that = (ArrayExpression)obj;
            return Entries == that.Entries;
        }

        bool INestedExpressions.GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            foreach (var entry in Entries)
            {
                if (line >= entry.Line && line <= entry.EndLine)
                    ExpressionGroup.GetExpressionsForLine(expressions, new[] { entry }, line);
            }

            return true;
        }
    }
}
