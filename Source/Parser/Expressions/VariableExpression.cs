using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions
{
    public abstract class VariableExpressionBase : ExpressionBase
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

    public class VariableExpression : VariableExpressionBase, INestedExpressions, IValueExpression
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
        /// Evaluates an expression
        /// </summary>
        /// <returns><see cref="ErrorExpression"/> indicating the failure, or the result of evaluating the expression.</returns>
        public ExpressionBase Evaluate(InterpreterScope scope)
        {
            ExpressionBase value = GetValue(scope);
            if (value == null) // not found
                return value;

            switch (value.Type)
            {
                case ExpressionType.Error:
                    return value;

                case ExpressionType.FunctionDefinition:
                    // a variable storing a function definition should just return the
                    // definition and let the caller decide whether or not to call the function.
                    return value;

                default:
                    break;
            }

            var valueExpression = value as IValueExpression;
            if (valueExpression != null)
                return valueExpression.Evaluate(scope);

            value.ReplaceVariables(scope, out value);
            return value;
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
            ExpressionBase value = GetValue(scope);
            if (value == null || value is ErrorExpression)
            {
                result = value;
                return false;
            }

            return value.ReplaceVariables(scope, out result);
        }

        /// <summary>
        /// Gets the un-evaluated value of the variable.
        /// </summary>
        public virtual ExpressionBase GetValue(InterpreterScope scope)
        {
            ExpressionBase value = scope.GetVariable(Name);
            if (value != null)
            {
                // when a parameter is assigned to a variable that is an array or dictionary,
                // assume it has already been evaluated and pass it by reference. this is magnitudes
                // more performant, and allows the function to modify the data in the container.
                if (VariableReferenceExpression.CanReference(value.Type))
                {
                    var reference = scope.GetVariableReference(Name);
                    CopyLocation(reference);
                    return reference;
                }

                return value;
            }

            var func = scope.GetFunction(Name);
            if (func != null)
            {
                // special wrapper for returning a function as a variable
                var result = new FunctionReferenceExpression(Name);
                CopyLocation(result);
                return result;
            }

            return new UnknownVariableParseErrorExpression("Unknown variable: " + Name, this);
        }

        /// <summary>
        /// Determines whether the expression evaluates to true for the provided <paramref name="scope"/>
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="error">[out] The error that prevented evaluation (or null if successful).</param>
        /// <returns>The result of evaluating the expression</returns>
        public override bool? IsTrue(InterpreterScope scope, out ErrorExpression error)
        {
            ExpressionBase value;
            if (!ReplaceVariables(scope, out value))
            {
                error = value as ErrorExpression;
                return null;
            }

            return value.IsTrue(scope, out error);
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

    public class VariableDefinitionExpression : VariableExpressionBase, INestedExpressions
    {
        public VariableDefinitionExpression(string name)
            : base(name)
        {
        }

        internal VariableDefinitionExpression(string name, int line, int column)
            : base(name, line, column)
        {
        }

        internal VariableDefinitionExpression(VariableExpressionBase variable)
            : base(variable.Name)
        {
            Location = variable.Location;
        }

        public bool IsMutableReference { get; set;}

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

    public class VariableReferenceExpression : ExpressionBase, IValueExpression
    {
        public VariableReferenceExpression(VariableDefinitionExpression variable, ExpressionBase expression)
            : base(ExpressionType.VariableReference)
        {
            Variable = variable;
            Expression = expression;

            MakeReadOnly();
        }

        public VariableDefinitionExpression Variable { get; private set; }

        public ExpressionBase Expression { get; private set; }

        /// <summary>
        /// Evaluates an expression
        /// </summary>
        /// <returns><see cref="ErrorExpression"/> indicating the failure, or the result of evaluating the expression.</returns>
        public ExpressionBase Evaluate(InterpreterScope scope)
        {
            // don't evaluate variable references when building parameter list.
            // arrays and dictionaries are passed to functions by reference.
            if (scope.Context is ParameterInitializationContext)
                return this;

            ExpressionBase result;
            ReplaceVariables(scope, out result);
            return result;
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Expression.ReplaceVariables(scope, out result);
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as VariableReferenceExpression;
            return that != null && Expression == that.Expression;
        }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
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

        internal static bool CanReference(ExpressionType type)
        {
            switch (type)
            {
                case ExpressionType.Dictionary:
                case ExpressionType.Array:
                    return true;

                default:
                    return false;
            }
        }
    }
}
