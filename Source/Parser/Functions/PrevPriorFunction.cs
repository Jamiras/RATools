using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;

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

            if (!ReplaceVariables(result, scope, out result))
                return false;

            CopyLocation(result, scope);
            return true;
        }

        private bool ReplaceVariables(ExpressionBase parameter, InterpreterScope scope, out ExpressionBase result)
        { 
            switch (parameter.Type)
            {
                case ExpressionType.Mathematic:
                    // convert "prev(a + b)" => "prev(a) + prev(b)"
                    return ReplaceVariablesMathematic((MathematicExpression)parameter, scope, out result);

                case ExpressionType.Requirement:
                    // convert "prev(a == b)" => "prev(a) == prev(b)"
                    var condition = parameter as RequirementConditionExpression;
                    if (condition != null)
                        return DistributeFlag(condition, scope, out result);
                    break;

                case ExpressionType.MemoryAccessor:
                    var memoryAccessor = parameter as MemoryAccessorExpression;
                    if (memoryAccessor != null)
                        return WrapMemoryAccessor(memoryAccessor, out result);

                    var modifiedMemoryAccessor = parameter as ModifiedMemoryAccessorExpression;
                    if (modifiedMemoryAccessor != null)
                        return WrapModifiedMemoryAccessor(modifiedMemoryAccessor, out result);

                    var memoryValue = parameter as MemoryValueExpression;
                    if (memoryValue != null)
                        return WrapMemoryValue(memoryValue, out result);

                    break;
            }

            result = InvalidParameter(parameter, scope, "accessor", ExpressionType.MemoryAccessor);
            return false;
        }

        private bool ReplaceVariablesMathematic(MathematicExpression mathematic, InterpreterScope scope, out ExpressionBase result)
        {
            // assert: left/right already expanded by calling function
            // recursive ReplaceVariables calls will apply flag to memoryaccessors
            var left = mathematic.Left;
            if (left is not LiteralConstantExpressionBase)
            {
                if (!ReplaceVariables(left, scope, out result))
                    return false;
                left = result;
            }

            var right = mathematic.Right;
            if (right is not LiteralConstantExpressionBase)
            {
                if (!ReplaceVariables(right, scope, out result))
                    return false;
                right = result;
            }

            result = new MathematicExpression(left, mathematic.Operation, right);
            mathematic.CopyLocation(result);
            return true;
        }

        private bool DistributeFlag(RequirementConditionExpression condition, InterpreterScope scope, out ExpressionBase result)
        {
            // assert: left/right already expanded by calling function
            // recursive ReplaceVariables calls will apply flag to memoryaccessors
            var left = condition.Left;
            if (left is not LiteralConstantExpressionBase)
            {
                if (!ReplaceVariables(left, scope, out result))
                    return false;
                left = result;
            }

            var right = condition.Right;
            if (right is not LiteralConstantExpressionBase)
            {
                if (!ReplaceVariables(right, scope, out result))
                    return false;
                right = result;
            }

            result = new RequirementConditionExpression { Left = left, Comparison = condition.Comparison, Right = right };
            condition.CopyLocation(result);
            return true;
        }

        private bool WrapMemoryAccessor(MemoryAccessorExpression memoryAccessor, out ExpressionBase result)
        {
            if (_fieldType == FieldType.BinaryCodedDecimal)
            {
                result = new BinaryCodedDecimalExpression(memoryAccessor);
                CopyLocation(result);
                return true;
            }

            if (_fieldType == FieldType.Invert)
            {
                result = new BitwiseInvertExpression(memoryAccessor);
                CopyLocation(result);
                return true;
            }

            var remembered = memoryAccessor as RememberRecallExpression;
            if (remembered != null)
            {
                if (!WrapMemoryValue(remembered.RememberedValue, out result))
                    return false;

                result = new RememberRecallExpression((MemoryValueExpression)result);
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
                    result = new ErrorExpression("cannot apply modifier to constant", memoryAccessor);
                    return false;

                default:
                    result = new ErrorExpression("cannot apply multiple modifiers to memory accessor", memoryAccessor);
                    return false;
            }

            result = memoryAccessor;
            return true;
        }

        private bool WrapModifiedMemoryAccessor(ModifiedMemoryAccessorExpression memoryAccessor, out ExpressionBase result)
        {
            if (!WrapMemoryAccessor(memoryAccessor.MemoryAccessor, out result))
                return false;

            var clone = memoryAccessor.Clone();
            clone.MemoryAccessor = (MemoryAccessorExpression)result;
            if (!UpdateModifier(clone, out result))
                return false;

            result = clone;
            return true;
        }

        private bool WrapMemoryValue(MemoryValueExpression memoryValue, out ExpressionBase result)
        {
            var clone = memoryValue.Clone();
            foreach (var memoryAccessor in clone.MemoryAccessors)
            {
                if (!WrapMemoryAccessor(memoryAccessor.MemoryAccessor, out result))
                    return false;

                memoryAccessor.MemoryAccessor = (MemoryAccessorExpression)result;

                if (!UpdateModifier(memoryAccessor, out result))
                    return false;
            }

            result = clone;
            return true;
        }

        private bool UpdateModifier(ModifiedMemoryAccessorExpression memoryAccessor, out ExpressionBase error)
        {
            if (memoryAccessor.ModifyingOperator != RequirementOperator.None &&
                memoryAccessor.Modifier.IsMemoryReference)
            {
                if (memoryAccessor.Modifier.Type != FieldType.MemoryAddress)
                {
                    error = new ErrorExpression("cannot apply multiple modifiers to memory accessor", memoryAccessor);
                    return false;
                }

                memoryAccessor.Modifier = memoryAccessor.Modifier.ChangeType(_fieldType);
            }

            error = null;
            return true;
        }
    }
}
