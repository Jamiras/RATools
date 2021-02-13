using Jamiras.Components;
using System;
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
            {
                function = parentScope.GetFunction(functionName);
                if (function != null)
                {
                    // if found, and the current scope is a function call, store for faster future lookups
                    if (Context is FunctionCallExpression)
                        _functions.Add(functionName, function);
                    return function;
                }
            }

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
        public ParseErrorExpression AssignVariable(VariableExpression variable, ExpressionBase value)
        {
            var indexedVariable = variable as IndexedVariableExpression;
            if (indexedVariable != null)
                return indexedVariable.Assign(this, value);

            // find the scope where the variable is defined and update it there.
            var scope = this;
            do
            {
                if (scope._variables.ContainsKey(variable.Name))
                {
                    scope.DefineVariable(new VariableDefinitionExpression(variable), value);
                    return null;
                }

                scope = GetParentScope(scope);
            } while (scope != null);

            // variable not defined, store in the current scope.
            DefineVariable(new VariableDefinitionExpression(variable), value);
            return null;
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
        /// Removes the definition of a variable from the current scope.
        /// </summary>
        /// <param name="name">The variable name.</param>
        public void UndefineVariable(string name)
        {
            _variables.Remove(name);
        }

        /// <summary>
        /// Removes the definition of a function from the current scope.
        /// </summary>
        /// <param name="name">The function name.</param>
        public void UndefineFunction(string name)
        {
            _functions.Remove(name);
        }

        private bool UpdateVariable(string name, IEnumerable<ExpressionBase> expressions)
        {
            foreach (var expression in expressions)
            {
                var assignment = expression as AssignmentExpression;
                if (assignment != null && assignment.Variable.Name == name)
                {
                    DefineVariable(new VariableDefinitionExpression(assignment.Variable), assignment.Value);
                    return true;
                }

                var nested = expression as INestedExpressions;
                if (nested != null)
                {
                    if (UpdateVariable(name, nested.NestedExpressions))
                        return true;
                }
            }

            return false;
        }

        private bool UpdateFunction(string name, IEnumerable<ExpressionBase> expressions)
        {
            foreach (var expression in expressions)
            {
                var definition = expression as FunctionDefinitionExpression;
                if (definition != null && definition.Name.Name == name)
                {
                    _functions[name] = definition;
                    return true;
                }

                var nested = expression as INestedExpressions;
                if (nested != null)
                {
                    if (UpdateFunction(name, nested.NestedExpressions))
                        return true;
                }
            }

            return false;
        }

        internal void UpdateVariables(IEnumerable<string> names, ExpressionGroup newGroup)
        {
            foreach (var name in names)
            {
                KeyValuePair<VariableDefinitionExpression, ExpressionBase> variable;
                if (_variables.TryGetValue(name, out variable))
                {
                    UpdateVariable(name, newGroup.Expressions);
                }
                else
                {
                    FunctionDefinitionExpression function;
                    if (_functions.TryGetValue(name, out function))
                    {
                        UpdateFunction(name, newGroup.Expressions);
                    }
                }
            }
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
