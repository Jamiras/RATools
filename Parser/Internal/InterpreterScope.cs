using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RATools.Parser.Internal
{
    using VariableDefinitionPair = KeyValuePair<VariableDefinitionExpression, ExpressionBase>;

    [DebuggerDisplay("\\{Context={Context} Vars={VariableCount} Funcs={FunctionCount} Depth={Depth}\\}")]
    internal class InterpreterScope
    {
        public InterpreterScope()
        {
        }

        public InterpreterScope(InterpreterScope parent)
            : this()
        {
            _parent = parent;
            Depth = parent.Depth + 1;
        }

        private Dictionary<string, FunctionDefinitionExpression> _functions;
        private Dictionary<string, VariableDefinitionPair> _variables;
        private VariableDefinitionPair _variable;
        private readonly InterpreterScope _parent;

        internal int VariableCount
        {
            get
            {
                if (_variables != null)
                    return _variables.Count;

                if (_variable.Key != null)
                    return 1;

                return 0;
            }
        }

        internal int FunctionCount
        {
            get
            {
                if (_functions != null)
                    return _functions.Count;

                return 0;
            }
        }

        private IEnumerable<InterpreterScope> VisibleScopes
        {
            get
            {
                var scope = this;
                do
                {
                    yield return scope;

                    var functionCall = scope.Context as FunctionCallExpression;
                    if (functionCall == null)
                    {
                        // not a function call - scope is not limiting
                    }
                    else if (AnonymousUserFunctionDefinitionExpression.IsAnonymousFunctionName(functionCall.FunctionName.Name))
                    {
                        // anonymous function may capture variables from outer scope
                    }
                    else
                    {
                        // function call starts a new local scope, jump over any other local scopes to the script scope
                        InterpreterScope outermostScriptScope = null;

                        do
                        {
                            if (scope.Context is AchievementScriptContext)
                                outermostScriptScope = scope;

                            if (scope._parent == null)
                                break;

                            scope = scope._parent;
                        } while (true);

                        if (outermostScriptScope != null)
                            yield return outermostScriptScope;

                        if (scope != outermostScriptScope)
                            yield return scope;
                    }

                    scope = scope._parent;
                } while (scope != null);
            }
        }

        /// <summary>
        /// Gets the function definition for a function.
        /// </summary>
        /// <param name="functionName">Name of the function.</param>
        /// <returns>Requested <see cref="FunctionDefinitionExpression"/>, <c>null</c> if not found.</returns>
        public FunctionDefinitionExpression GetFunction(string functionName)
        {
            foreach (var scope in VisibleScopes)
            {
                FunctionDefinitionExpression function;
                if (scope._functions != null && scope._functions.TryGetValue(functionName, out function))
                    return function;

                var functionVariable = scope.GetVariableDefinitionPair(functionName);
                if (functionVariable.Key != null)
                {
                    var functionDefinition = functionVariable.Value as FunctionDefinitionExpression;
                    if (functionDefinition != null)
                        return functionDefinition;

                    var functionReference = functionVariable.Value as FunctionReferenceExpression;
                    if (functionReference != null)
                        return scope.GetFunction(functionReference.Name);
                }
            }

            return null;
        }

        /// <summary>
        /// Adds the function definition.
        /// </summary>
        public void AddFunction(FunctionDefinitionExpression function)
        {
            if (_functions == null)
                _functions = new Dictionary<string, FunctionDefinitionExpression>();

            _functions[function.Name.Name] = function;
        }

        private VariableDefinitionPair GetVariableDefinitionPair(string variableName)
        {
            VariableDefinitionPair variable;
            if (_variables != null && _variables.TryGetValue(variableName, out variable))
                return variable;

            if (_variable.Key != null && _variable.Key.Name == variableName)
                return _variable;

            return new VariableDefinitionPair();
        }

        /// <summary>
        /// Gets the value of a variable.
        /// </summary>
        /// <param name="variableName">Name of the variable.</param>
        /// <returns>Value of the variable, <c>null</c> if not found.</returns>
        public ExpressionBase GetVariable(string variableName)
        {
            foreach (var scope in VisibleScopes)
            {
                var variable = scope.GetVariableDefinitionPair(variableName);
                if (variable.Key != null)
                    return variable.Value;
            }

            return null;
        }

        /// <summary>
        /// Gets the definition of a variable.
        /// </summary>
        /// <param name="variableName">Name of the variable.</param>
        /// <returns>Definition of the variable, <c>null</c> if not found.</returns>
        public ExpressionBase GetVariableDefinition(string variableName)
        {
            foreach (var scope in VisibleScopes)
            {
                var variable = scope.GetVariableDefinitionPair(variableName);
                if (variable.Key != null)
                    return variable.Key;
            }

            return null;
        }

        /// <summary>
        /// Gets a reference to a variable.
        /// </summary>
        /// <param name="variableName">Name of the variable.</param>
        /// <returns>Reference to the variable, <c>null</c> if not found.</returns>
        public VariableReferenceExpression GetVariableReference(string variableName)
        {
            foreach (var scope in VisibleScopes)
            {
                var variable = scope.GetVariableDefinitionPair(variableName);
                if (variable.Key != null)
                    return new VariableReferenceExpression(variable.Key, variable.Value);
            }

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

            var variableDefinition = new VariableDefinitionExpression(variable);

            // find the scope where the variable is defined and update it there.
            foreach (var scope in VisibleScopes)
            {
                if (scope._variables != null && scope._variables.ContainsKey(variable.Name))
                {
                    scope._variables[variable.Name] = new VariableDefinitionPair(variableDefinition, value);
                    return null;
                }

                if (scope._variable.Key != null && scope._variable.Key.Name == variable.Name)
                {
                    scope._variable = new VariableDefinitionPair(variableDefinition, value);
                    return null;
                }
            }

            // variable not defined, store in the current scope.
            DefineVariable(variableDefinition, value);
            return null;
        }

        /// <summary>
        /// Assigns the value to a variable for the current scope.
        /// </summary>
        /// <param name="variable">The variable.</param>
        /// <param name="value">The value.</param>
        public void DefineVariable(VariableDefinitionExpression variable, ExpressionBase value)
        {
            if (_variables == null)
            {
                if (_variable.Key == null)
                {
                    _variable = new VariableDefinitionPair(variable, value);
                    return;
                }

                _variables = new Dictionary<string, VariableDefinitionPair>();
                _variables.Add(_variable.Key.Name, _variable);
                _variable = new VariableDefinitionPair();
            }

            _variables[variable.Name] = new VariableDefinitionPair(variable, value);
        }

        /// <summary>
        /// Removes the definition of a variable from the current scope.
        /// </summary>
        /// <param name="name">The variable name.</param>
        public void UndefineVariable(string name)
        {
            if (_variables != null)
                _variables.Remove(name);
            else if (_variable.Key != null && _variable.Key.Name == name)
                _variable = new VariableDefinitionPair();
        }

        /// <summary>
        /// Removes the definition of a function from the current scope.
        /// </summary>
        /// <param name="name">The function name.</param>
        public void UndefineFunction(string name)
        {
            if (_functions != null)
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
            if (_variable.Key != null)
            {
                if (names.Contains(_variable.Key.Name))
                    UpdateVariable(_variable.Key.Name, newGroup.Expressions);
            }
            else if (_variables != null)
            {
                foreach (var name in names)
                {
                    VariableDefinitionPair variable;
                    if (_variables.TryGetValue(name, out variable))
                        UpdateVariable(name, newGroup.Expressions);
                }
            }

            if (_functions != null)
            {
                foreach (var name in names)
                {
                    FunctionDefinitionExpression function;
                    if (_functions.TryGetValue(name, out function))
                        UpdateFunction(name, newGroup.Expressions);
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
