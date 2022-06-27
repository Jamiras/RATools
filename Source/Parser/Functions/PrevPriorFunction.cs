using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class PrevPriorFunction : FunctionDefinitionExpression
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
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
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
            var memoryAccessor = expression as MemoryAccessorExpression;
            if (memoryAccessor == null)
            {
                result = new ErrorExpression("accessor did not evaluate to a memory accessor", expression);
                return false;
            }

            if (_fieldType == FieldType.BinaryCodedDecimal)
            {
                result = new BinaryCodedDecimalExpression(memoryAccessor);
                CopyLocation(result);
                return true;
            }

            memoryAccessor = memoryAccessor.Clone();

            switch (memoryAccessor.Field.Type)
            {
                case FieldType.MemoryAddress:
                    memoryAccessor.Field = memoryAccessor.Field.ChangeType(_fieldType);
                    break;

                case FieldType.Value:
                case FieldType.Float:
                    result = new ErrorExpression("cannot apply modifier to constant", expression);
                    return false;

                default:
                    result = new ErrorExpression("cannot apply multiple modifiers to memory accessor", expression);
                    return false;
            }

            result = memoryAccessor;
            CopyLocation(result);
            return true;
        }
    }
}
