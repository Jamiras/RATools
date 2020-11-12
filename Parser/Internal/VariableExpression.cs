using System.Collections.Generic;
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
            Line = line;
            EndLine = line;
            Column = column;
            EndColumn = column + name.Length - 1;
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
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ParseErrorExpression" />.
        /// </returns>
        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            ExpressionBase value = scope.GetVariable(Name);
            if (value == null)
            {
                var func = scope.GetFunction(Name);
                if (func != null)
                    result = new ParseErrorExpression("Function used like a variable: " + Name, this);
                else
                    result = new ParseErrorExpression("Unknown variable: " + Name, this);

                return false;
            }

            return value.ReplaceVariables(scope, out result);
        }

        /// <summary>
        /// Determines whether the specified <see cref="VariableExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="VariableExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="VariableExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = (VariableExpression)obj;
            return Name == that.Name;
        }

        bool INestedExpressions.GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            if (Line == line)
                expressions.Add(this);

            return true;
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            dependencies.Add(Name);
        }

        void INestedExpressions.GetModifications(HashSet<string> dependencies)
        {
        }
    }

    internal class VariableDefinitionExpression : VariableExpressionBase
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
            : base(variable.Name, variable.Line, variable.Column)
        {
            EndLine = variable.EndLine;
            EndColumn = variable.EndColumn;
        }

        /// <summary>
        /// Determines whether the specified <see cref="VariableExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="VariableExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="VariableExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = (VariableDefinitionExpression)obj;
            return Name == that.Name;
        }
    }
}
