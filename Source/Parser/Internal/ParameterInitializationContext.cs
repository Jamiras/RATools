using RATools.Parser.Expressions;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Internal
{
    internal class ParameterInitializationContext
    {
        public ParameterInitializationContext(FunctionDefinitionExpression function, IEnumerable<ExpressionBase> parameters)
        {
            Function = function;
            Parameters = parameters;
        }

        public FunctionDefinitionExpression Function { get; private set; }

        public IEnumerable<ExpressionBase> Parameters { get; private set; }

        public ExpressionBase GetParameter(string name)
        {
            var namedParameter = Parameters.OfType<AssignmentExpression>().FirstOrDefault(p => p.Variable.Name == name);
            if (namedParameter != null)
                return namedParameter.Value;

            var index = 0;
            foreach (var parameter in Function.Parameters)
            {
                if (parameter.Name == name)
                {
                    var value = Parameters.ElementAtOrDefault(index);
                    if (value != null && value.Type != ExpressionType.Assignment)
                        return value;

                    if (Function.DefaultParameters.TryGetValue(name, out value))
                        return value;

                    break;
                }

                index++;
            }

            return null;
        }

        public T GetParameter<T>(InterpreterScope scope, string name)
            where T : ExpressionBase
        {
            var parameter = GetParameter(name);
            if (parameter == null)
                return null;

            var parameterT = parameter as T;
            if (parameterT == null)
            {
                ExpressionBase value;
                if (parameter.ReplaceVariables(scope, out value))
                    parameterT = value as T;
            }

            return parameterT;
        }
    }
}
