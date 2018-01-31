using Jamiras.Components;

namespace RATools.Parser.Internal
{
    internal class InterpreterScope
    {
        public InterpreterScope()
        {
            _functions = new TinyDictionary<string, FunctionDefinitionExpression>();
            _variables = new TinyDictionary<string, ExpressionBase>();
        }

        public InterpreterScope(InterpreterScope parent)
            : this()
        {
            _parent = parent;
        }

        private readonly TinyDictionary<string, FunctionDefinitionExpression> _functions;
        private readonly TinyDictionary<string, ExpressionBase> _variables;
        private readonly InterpreterScope _parent;

        internal int VariableCount
        {
            get { return _variables.Count; }
        }

        /// <summary>
        /// Gets the function definition for a function.
        /// </summary>
        /// <param name="functionName">Name of the function.</param>
        /// <returns>Requested <see cref="FunctionDefinitionExpression"/>, <c>null</c> if not found.</returns>
        public FunctionDefinitionExpression GetFunction(string functionName)
        {
            FunctionDefinitionExpression function;
            if (_functions.TryGetValue(functionName, out function))
                return function;

            if (_parent != null)
                return _parent.GetFunction(functionName);

            return null;
        }

        /// <summary>
        /// Adds the function definition.
        /// </summary>
        public void AddFunction(FunctionDefinitionExpression function)
        {
            _functions[function.Name.Name] = function;
        }

        /// <summary>
        /// Gets the value of a variable.
        /// </summary>
        /// <param name="variableName">Name of the variable.</param>
        /// <returns>Value of the variable, <c>null</c> if not found.</returns>
        public ExpressionBase GetVariable(string variableName)
        {
            ExpressionBase variable;
            if (_variables.TryGetValue(variableName, out variable))
                return variable;

            if (_parent != null)
                return _parent.GetVariable(variableName);

            return null;
        }

        /// <summary>
        /// Updates the value of a variable.
        /// </summary>
        /// <param name="variable">The variable.</param>
        /// <param name="value">The value.</param>
        public void AssignVariable(VariableExpression variable, ExpressionBase value)
        {
            var indexedVariable = variable as IndexedVariableExpression;
            if (indexedVariable != null)
            {
                ExpressionBase result;
                var entry = indexedVariable.GetDictionaryEntry(this, out result, true);
                entry.Value = value;
                return;
            }

            // find the scope where the variable is defined and update it there.
            var scope = this;
            while (scope != null)
            {
                if (scope._variables.ContainsKey(variable.Name))
                {
                    scope._variables[variable.Name] = value;
                    return;
                }

                scope = scope._parent;
            }

            // variable not defined, store in the current scope.
            _variables[variable.Name] = value;
        }

        /// <summary>
        /// Assigns the value to a variable for the current scope.
        /// </summary>
        /// <param name="variable">The variable.</param>
        /// <param name="value">The value.</param>
        public void DefineVariable(VariableExpression variable, ExpressionBase value)
        {
            _variables[variable.Name] = value;
        }

        /// <summary>
        /// Gets whether or not the processor has encountered an early exit statement (return, break)
        /// </summary>
        public bool IsComplete { get; internal set; }

        /// <summary>
        /// Gets the value to return when leaving the scope.
        /// </summary>
        public ExpressionBase ReturnValue { get; internal set; }
    }
}
