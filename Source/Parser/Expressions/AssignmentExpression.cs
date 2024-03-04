using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Expressions
{
    /// <summary>
    /// Represents an assignment within an expression tree.
    /// </summary>
    public class AssignmentExpression : ExpressionBase, INestedExpressions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AssignmentExpression"/> class.
        /// </summary>
        public AssignmentExpression(VariableExpression variable, ExpressionBase value)
            : base(ExpressionType.Assignment)
        {
            Variable = variable;
            Value = value;

            Location = new Jamiras.Components.TextRange(Variable.Location.Start, value.Location.End);
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
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ErrorExpression" />.
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
        /// Updates the provided scope with the new variable value.
        /// </summary>
        /// <param name="scope">The scope object containing variable values and function parameters.</param>
        /// <returns>
        ///   <c>null</c> if successful, or a <see cref="ErrorExpression" /> if not.
        /// </returns>
        public ErrorExpression Evaluate(InterpreterScope scope)
        {
            var assignmentScope = new InterpreterScope(scope) { Context = this };
            ExpressionBase result;

            var functionDefinition = Value as FunctionDefinitionExpression;
            if (functionDefinition != null)
            {
                scope.AddFunction(functionDefinition);
                result = new FunctionReferenceExpression(functionDefinition.Name.Name);
            }
            else
            {
                var variable = Value as VariableExpression;
                if (variable != null)
                {
                    result = variable.GetValue(assignmentScope);
                    var error = result as ErrorExpression;
                    if (error != null)
                        return error;
                }
                else
                {
                    if (!Value.ReplaceVariables(assignmentScope, out result))
                        return (ErrorExpression)result;
                }
            }

            return scope.AssignVariable(Variable, result);
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
            var that = obj as AssignmentExpression;
            return that != null && Variable == that.Variable && Value == that.Value;
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
            // manually traverse the IndexedVariableExpression so we don't add the outermost variable to the
            // dependencies list. we are updating that, not reading it.
            IndexedVariableExpression indexedVariable;
            var variable = Variable;
            while ((indexedVariable = variable as IndexedVariableExpression) != null)
            {
                var indexNested = indexedVariable.Index as INestedExpressions;
                if (indexNested != null)
                    indexNested.GetDependencies(dependencies);

                variable = indexedVariable.Variable;
            }

            var nested = Value as INestedExpressions;
            if (nested != null)
                nested.GetDependencies(dependencies);
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
            IndexedVariableExpression indexedVariable;
            var variable = Variable;

            while ((indexedVariable = variable as IndexedVariableExpression) != null)
                variable = indexedVariable.Variable;

            modifies.Add(variable.Name);
        }
    }
}
