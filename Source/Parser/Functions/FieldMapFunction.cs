using RATools.Parser.Expressions;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class FieldMapFunction : FunctionDefinitionExpression
    {
        public FieldMapFunction()
            : base("field_map")
        {
            Parameters.Add(new VariableDefinitionExpression("input"));
            Parameters.Add(new VariableDefinitionExpression("predicate"));
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var value = GetParameter(scope, "input", out result);
            if (value == null)
                return false;

            if (!value.ReplaceVariables(scope, out result))
                return false;

            var input = result as ClassInstanceExpression;
            if (input == null)
            {
                result = new ConversionErrorExpression(result, ExpressionType.ClassInstance);
                return false;
            }

            var predicate = GetFunctionParameter(scope, "predicate", out result);
            if (predicate == null)
                return false;

            if ((predicate.Parameters.Count - predicate.DefaultParameters.Count) != 2)
            {
                result = new ErrorExpression("predicate function must accept two parameters");
                return false;
            }

            var iteratorScope = predicate.CreateCaptureScope(scope);

            var predicateFieldParameter = new VariableExpression(predicate.Parameters.First().Name);
            var predicateValueParameter = new VariableExpression(predicate.Parameters.ElementAt(1).Name);
            foreach (var kvp in predicate.DefaultParameters)
                iteratorScope.AssignVariable(new VariableExpression(kvp.Key), kvp.Value);

            DictionaryExpression dictionaryResult = null;
            ArrayExpression arrayResult = null;
            foreach (var field in input.ClassDefinition.Fields)
            {
                iteratorScope.AssignVariable(predicateFieldParameter, new StringConstantExpression(field.Variable.Name) { Location = field.Variable.Location });
                iteratorScope.AssignVariable(predicateValueParameter, input.GetFieldValue(field.Variable.Name));

                if (!predicate.Evaluate(iteratorScope, out result))
                    return false;

                if (result == null)
                {
                    result = new ErrorExpression("predicate did not return a value", predicate);
                    return false;
                }

                var boolResult = result as BooleanConstantExpression;
                if (boolResult != null && boolResult.Value == false)
                    continue;

                var dict = result as DictionaryExpression;
                if (dict != null)
                {
                    if (arrayResult != null)
                    {
                        result = new ErrorExpression("Cannot combine mapped and unmapped values", result);
                        return false;
                    }

                    if (dict.Entries.Count() != 1)
                    {
                        result = new ErrorExpression("Dictionary returned from predicate must have a single entry (found " + dict.Entries.Count() + ")", result);
                        return false;
                    }

                    if (dictionaryResult == null)
                        dictionaryResult = new DictionaryExpression() { Location = this.Location };

                    foreach (var kvp in dict.Entries)
                        dictionaryResult.Assign(kvp.Key, kvp.Value);
                }
                else
                {
                    if (dictionaryResult != null)
                    {
                        result = new ErrorExpression("Cannot combine mapped and unmapped values", result);
                        return false;
                    }

                    if (arrayResult == null)
                        arrayResult = new ArrayExpression() { Location = this.Location };

                    arrayResult.Entries.Add(result);
                }
            }

            if (dictionaryResult != null)
                result = dictionaryResult;
            else if (arrayResult != null)
                result = arrayResult;
            else
                result = new ArrayExpression() { Location = this.Location };

            result.IsLogicalUnit = true;
            return true;
        }
    }
}
