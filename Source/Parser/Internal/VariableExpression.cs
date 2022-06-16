using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Parser.Internal
{
    internal abstract class VariableExpressionBase : ExpressionBase
    {
        public VariableExpressionBase(string name)
            : base(ExpressionType.Variable)
        {
            Name = name;
        }

        internal VariableExpressionBase(string name, int line, int column)
            : this(name)
        {
            Location = new Jamiras.Components.TextRange(line, column, line, column + name.Length - 1);
        }

        /// <summary>
        /// Gets the name of the variable.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(Name);
        }

        /// <summary>
        /// Determines whether the specified <see cref="VariableExpressionBase" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="VariableExpressionBase" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="VariableExpressionBase" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as VariableExpressionBase;
            return that != null && Name == that.Name && GetType() == that.GetType();
        }
    }

    internal class VariableExpression : VariableExpressionBase, INestedExpressions
    {
        public VariableExpression(string name)
            : base(name)
        {
        }

        internal VariableExpression(string name, int line, int column)
            : base(name, line, column)
        {
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
            ExpressionBase value = scope.GetVariable(Name);
            if (value == null)
            {
                var func = scope.GetFunction(Name);
                if (func != null)
                {
                    // special wrapper for returning a function as a variable
                    result = new FunctionReferenceExpression(Name);
                    this.CopyLocation(result);
                    return true;
                }

                result = new UnknownVariableParseErrorExpression("Unknown variable: " + Name, this);
                return false;
            }

            return value.ReplaceVariables(scope, out result);
        }

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                return Enumerable.Empty<ExpressionBase>();
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            dependencies.Add(Name);
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
        }
    }

    internal class VariableDefinitionExpression : VariableExpressionBase, INestedExpressions
    {
        public VariableDefinitionExpression(string name)
            : base(name)
        {
        }

        internal VariableDefinitionExpression(string name, int line, int column)
            : base(name, line, column)
        {
        }

        internal VariableDefinitionExpression(VariableExpression variable)
            : base(variable.Name)
        {
            Location = variable.Location;
        }

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                return Enumerable.Empty<ExpressionBase>();
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
            modifies.Add(Name);
        }
    }

    internal class VariableReferenceExpression : ExpressionBase
    {
        public VariableReferenceExpression(VariableDefinitionExpression variable, ExpressionBase expression)
            : base(ExpressionType.VariableReference)
        {
            Variable = variable;
            Expression = expression;
        }

        public VariableDefinitionExpression Variable { get; private set; }

        public ExpressionBase Expression { get; private set; }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Expression.ReplaceVariables(scope, out result);
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as VariableReferenceExpression;
            return (that != null && Expression == that.Expression);
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append(Expression.Type);
            builder.Append(" Reference: ");
            AppendString(builder);
            return builder.ToString();
        }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(Variable.Name);
        }
    }
}
