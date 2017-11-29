using System.Text;

namespace RATools.Parser.Internal
{
    internal class VariableExpression : ExpressionBase
    {
        public VariableExpression(string name)
            : base(ExpressionType.Variable)
        {
            Name = name;
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
                    result = new ParseErrorExpression("Function used like a variable: " + Name);
                else
                    result = new ParseErrorExpression("Unknown variable: " + Name);

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
    }
}
