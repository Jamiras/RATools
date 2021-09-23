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
            var parameter = GetParameter(scope, "accessor", out result);
            if (parameter == null)
                return false;

            if (!parameter.ReplaceVariables(scope, out result))
                return false;
            parameter = result;

            // convert "prev(a + b)" => "prev(a) + prev(b)"
            var mathematic = parameter as MathematicExpression;
            if (mathematic != null)
                return ReplaceVariablesMathematic(mathematic, scope, out result);

            // make sure accessor is a memory accessor
            return WrapMemoryAccessor(parameter, scope, out result);
        }

        private bool ReplaceVariablesMathematic(MathematicExpression mathematic, InterpreterScope scope, out ExpressionBase result)
        {
            // assert: left/right already expanded by calling function
            var left = mathematic.Left;

            var mathematicLeft = left as MathematicExpression;
            if (mathematicLeft != null)
            {
                if (!ReplaceVariablesMathematic(mathematicLeft, scope, out result))
                    return false;

                left = result;
            }
            else if (left.Type != ExpressionType.IntegerConstant)
            {
                if (!WrapMemoryAccessor(left, scope, out result))
                    return false;
                left = result;
            }

            var right = mathematic.Right;
            var mathematicRight = right as MathematicExpression;
            if (mathematicRight != null)
            {
                if (!ReplaceVariablesMathematic(mathematicRight, scope, out result))
                    return false;

                right = result;
            }
            else if (right.Type != ExpressionType.IntegerConstant)
            {
                if (!WrapMemoryAccessor(right, scope, out result))
                    return false;
                right = result;
            }

            result = new MathematicExpression(left, mathematic.Operation, right);
            CopyLocation(result);
            return true;
        }

        private bool WrapMemoryAccessor(ExpressionBase expression, InterpreterScope scope, out ExpressionBase result)
        {
            var functionCall = expression as FunctionCallExpression;
            if (functionCall == null)
            {
                result = new ParseErrorExpression("accessor did not evaluate to a memory accessor", expression);
                return false;
            }

            var functionDefinition = scope.GetFunction(functionCall.FunctionName.Name);
            var memoryAccessor = functionDefinition as MemoryAccessorFunction;
            if (memoryAccessor == null)
            {
                result = new ParseErrorExpression("accessor did not evaluate to a memory accessor", expression);
                return false;
            }

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { functionCall });
            CopyLocation(result);
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
