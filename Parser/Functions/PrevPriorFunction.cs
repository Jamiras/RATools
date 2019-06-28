using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class PrevPriorFunction : TriggerBuilderContext.FunctionDefinition
    {
        public PrevPriorFunction(string name, FieldType fieldType)
            : base(name)
        {
            _fieldType = fieldType;

            Parameters.Add(new VariableDefinitionExpression("accessor"));
        }

        private readonly FieldType _fieldType;

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            if (!IsInTriggerClause(scope, out result))
                return false;

            var parameter = GetParameter(scope, "accessor", out result);
            if (parameter == null)
                return false;

            if (!parameter.ReplaceVariables(scope, out result))
                return false;
            parameter = result;

            var mathematic = parameter as MathematicExpression;
            if (mathematic != null)
            {
                var left = mathematic.Left;
                if (!(left is IntegerConstantExpression))
                {
                    left = new FunctionCallExpression(Name.Name, new ExpressionBase[] { mathematic.Left });
                    if (!left.ReplaceVariables(scope, out result))
                        return false;
                    left = result;
                }

                var right = mathematic.Right;
                if (!(right is IntegerConstantExpression))
                {
                    right = new FunctionCallExpression(Name.Name, new ExpressionBase[] { mathematic.Right });
                    if (!right.ReplaceVariables(scope, out result))
                        return false;
                    right = result;
                }

                result = new MathematicExpression(left, mathematic.Operation, right);
                return true;
            }

            var functionCall = parameter as FunctionCallExpression;
            if (functionCall == null)
            {
                result = new ParseErrorExpression("accessor did not evaluate to a memory accessor", parameter);
                return false;
            }

            var functionDefinition = scope.GetFunction(functionCall.FunctionName.Name);
            var memoryAccessor = functionDefinition as MemoryAccessorFunction;
            if (memoryAccessor == null)
            {
                result = new ParseErrorExpression("accessor did not evaluate to a memory accessor", parameter);
                return false;
            }

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { functionCall });
            return true;
        }

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var accessor = (FunctionCallExpression)functionCall.Parameters.First();
            var error = context.CallFunction(accessor, scope);
            if (error != null)
                return error;

            var left = context.LastRequirement.Left;
            context.LastRequirement.Left = new Field { Size = left.Size, Type = _fieldType, Value = left.Value };
            return null;
        }
    }
}
