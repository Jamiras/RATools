using Jamiras.Components;
using System.Collections.Generic;

namespace RATools.Parser.Internal
{
    internal class InterpreterScope
    {
        public InterpreterScope()
        {
            _functions = new TinyDictionary<string, FunctionDefinitionExpression>();
            _variables = new TinyDictionary<string, KeyValuePair<VariableDefinitionExpression, ExpressionBase>>();
        }

        public InterpreterScope(InterpreterScope parent)
            : this()
        {
            _parent = parent;
            Depth = parent.Depth + 1;
        }

        private readonly TinyDictionary<string, FunctionDefinitionExpression> _functions;
        private readonly TinyDictionary<string, KeyValuePair<VariableDefinitionExpression, ExpressionBase>> _variables;
        private readonly InterpreterScope _parent;

        internal int VariableCount
        {
            get { return _variables.Count; }
        }

        private static InterpreterScope GetParentScope(InterpreterScope scope)
        {
            if (scope.Context is FunctionCallExpression)
            {
                // function call starts a new local scope, jump over any other local scopes to the script scope
                InterpreterScope outermostScriptScope = null;

                do
                {
                    if (scope.Context is AchievementScriptContext)
                        outermostScriptScope = scope;

                    if (scope._parent == null)
                        return outermostScriptScope ?? scope;

                    scope = scope._parent;
                } while (true);
            }

            return scope._parent;
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

            var parentScope = GetParentScope(this);
            if (parentScope != null)
                return parentScope.GetFunction(functionName);

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
            KeyValuePair<VariableDefinitionExpression, ExpressionBase> variable;
            if (_variables.TryGetValue(variableName, out variable))
                return variable.Value;

            var parentScope = GetParentScope(this);
            if (parentScope != null)
                return parentScope.GetVariable(variableName);

            return null;
        }

        /// <summary>
        /// Gets the definition of a variable.
        /// </summary>
        /// <param name="variableName">Name of the variable.</param>
        /// <returns>Definition of the variable, <c>null</c> if not found.</returns>
        public ExpressionBase GetVariableDefinition(string variableName)
        {
            KeyValuePair<VariableDefinitionExpression, ExpressionBase> variable;
            if (_variables.TryGetValue(variableName, out variable))
                return variable.Key;

            var parentScope = GetParentScope(this);
            if (parentScope != null)
                return parentScope.GetVariableDefinition(variableName);

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
                if (entry != null)
                    entry.Value = value;
                return;
            }

            // find the scope where the variable is defined and update it there.
            var scope = this;
            do
            {
                if (scope._variables.ContainsKey(variable.Name))
                {
                    scope.DefineVariable(new VariableDefinitionExpression(variable), value);
                    return;
                }

                scope = GetParentScope(scope);
            } while (scope != null);

            // variable not defined, store in the current scope.
            DefineVariable(new VariableDefinitionExpression(variable), value);
        }

        /// <summary>
        /// Assigns the value to a variable for the current scope.
        /// </summary>
        /// <param name="variable">The variable.</param>
        /// <param name="value">The value.</param>
        public void DefineVariable(VariableDefinitionExpression variable, ExpressionBase value)
        {
            _variables[variable.Name] = new KeyValuePair<VariableDefinitionExpression, ExpressionBase>(variable, value);
        }

        /// <summary>
        /// Gets whether or not the processor has encountered an early exit statement (return, break)
        /// </summary>
        public bool IsComplete { get; internal set; }
 
        /// <summary>
        /// Gets the value to return when leaving the scope.
        /// </summary>
        public ExpressionBase ReturnValue { get; internal set; }

        internal object Context { get; set; }

        internal T GetContext<T>()
            where T : class
        {
            var scope = this;
            do
            {
                var context = scope.Context as T;
                if (context != null)
                    return context;

                scope = scope._parent;
            } while (scope != null);

            return null;
        }

        internal T GetInterpreterContext<T>()
            where T : class
        {
            var scope = this;
            do
            {
                var context = scope.Context as T;
                if (context != null)
                    return context;

                if (scope.Context is AchievementScriptInterpreter)
                    break;

                scope = scope._parent;
            } while (scope != null);

            return null;
        }

        internal T GetOutermostContext<T>()
            where T : class
        {
            T outermostContext = null;

            var scope = this;
            do
            {
                var context = scope.Context as T;
                if (context != null)
                    outermostContext = context;

                scope = scope._parent;
            } while (scope != null);

            return outermostContext;
        }

        internal int Depth { get; private set; }
    }
}
