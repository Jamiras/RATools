using Jamiras.Components;
using System.Linq;

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

        public FunctionDefinitionExpression GetFunction(string functionName)
        {
            FunctionDefinitionExpression function;
            if (_functions.TryGetValue(functionName, out function))
                return function;

            if (_parent != null)
                return _parent.GetFunction(functionName);

            return null;
        }

        public void AddFunction(FunctionDefinitionExpression function)
        {
            _functions[function.Name] = function;
        }

        public ExpressionBase GetVariable(string variableName)
        {
            ExpressionBase variable;
            if (_variables.TryGetValue(variableName, out variable))
                return variable;

            if (_parent != null)
                return _parent.GetVariable(variableName);

            return null;
        }

        public void AssignVariable(VariableExpression variable, ExpressionBase value)
        {
            var indexedVariable = variable as IndexedVariableExpression;
            if (indexedVariable != null)
            {
                var existing = GetVariable(indexedVariable.Name);
                if (existing == null)
                {
                    existing = new DictionaryExpression();
                    _variables[variable.Name] = existing;
                }

                ExpressionBase index;
                var dict = existing as DictionaryExpression;
                if (dict != null && indexedVariable.Index.ReplaceVariables(this, out index))
                {
                    var entry = dict.Entries.FirstOrDefault(e => e.Key == index);
                    if (entry == null)
                    {
                        entry = new DictionaryExpression.DictionaryEntry { Key = index };
                        dict.Entries.Add(entry);
                    }

                    entry.Value = value;
                    return;
                }
            }

            _variables[variable.Name] = value;
        }
    }
}
