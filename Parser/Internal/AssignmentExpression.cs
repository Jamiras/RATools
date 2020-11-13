using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Internal
{
    /// <summary>
    /// Represents an assignment within an expression tree.
    /// </summary>
    internal class AssignmentExpression : ExpressionBase, INestedExpressions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AssignmentExpression"/> class.
        /// </summary>
        public AssignmentExpression(VariableExpression variable, ExpressionBase value)
            : base(ExpressionType.Assignment)
        {
            Variable = variable;
            Value = value;

            Line = Variable.Line;
            Column = Variable.Column;
            EndLine = value.EndLine;
            EndColumn = value.EndColumn;
        }

        /// <summary>
        /// Gets the variable where the value will be stored.
        /// </summary>
        public VariableExpression Variable { get; private set; }

        /// <summary>
        /// Gets the expression that will be resolved into the value to be stored.
        /// </summary>
        public ExpressionBase Value { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            Variable.AppendString(builder);
            builder.Append(" = ");
            Value.AppendString(builder);
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
            var expressionScope = new InterpreterScope(scope) { Context = this };
            ExpressionBase value;
            if (!Value.ReplaceVariables(expressionScope, out value))
            {
                result = value;
                return false;
            }

            result = new AssignmentExpression(Variable, value);
            CopyLocation(result);
            return true;
        }

        /// <summary>
        /// Determines whether the specified <see cref="AssignmentExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="AssignmentExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="AssignmentExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = (AssignmentExpression)obj;
            return Variable == that.Variable && Value == that.Value;
        }

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                yield return Variable;
                yield return Value;
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            // don't call GetDependencies on a base VariableExpression as we're updating it, not reading it
            // do call GetDependencies on an IndexedVariableExpression to get the dependencies of the index
            var indexedVariable = Variable as IndexedVariableExpression;
            if (indexedVariable != null)
                ((INestedExpressions)indexedVariable).GetDependencies(dependencies);

            var nested = Value as INestedExpressions;
            if (nested != null)
                nested.GetDependencies(dependencies);
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
            modifies.Add(Variable.Name);
        }
    }
}
