using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Linq;
using System.Text;

namespace RATools.Parser.Functions
{
    internal class MemoryAccessorFunction : FunctionDefinitionExpression
    {
        public MemoryAccessorFunction(string name, FieldSize size)
            : base(name)
        {
            Size = size;

            Parameters.Add(new VariableDefinitionExpression("address"));
        }

        public FieldSize Size { get; private set; }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            // we want to create a MemoryAccessorExpression for assignments too
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var address = GetParameter(scope, "address", out result);
            if (address == null)
                return false;

            result = CreateMemoryAccessorExpression(address);
            if (result.Type == ExpressionType.Error)
                return false;

            CopyLocation(result);
            return true;
        }

        protected ExpressionBase CreateMemoryAccessorExpression(ExpressionBase address)
        {
            var integerConstant = address as IntegerConstantExpression;
            if (integerConstant != null)
                return new MemoryAccessorExpression(FieldType.MemoryAddress, Size, (uint)integerConstant.Value);

            var accessor = address as MemoryAccessorExpression;
            if (accessor != null)
            {
                var result = new MemoryAccessorExpression();
                foreach (var pointer in accessor.PointerChain)
                    result.AddPointer(pointer);

                result.AddPointer(new Requirement { Type = RequirementType.AddAddress, Left = accessor.Field });
                result.Field = new Field { Type = FieldType.MemoryAddress, Size = Size, Value = 0 };
                return result;
            }

            var mathematic = address as MathematicExpression;
            if (mathematic != null)
            {
                var result = CreateMemoryAccessorExpression(mathematic.Left);
                if (result.Type == ExpressionType.Error)
                    return result;

                accessor = result as MemoryAccessorExpression;
                if (accessor == null)
                    return new ErrorExpression("Cannot create pointer", mathematic.Left);

                if (accessor.Field.Value != 0) // pointer chain already has an offset
                    return new ErrorExpression("Cannot construct single address lookup from multiple memory references", mathematic);

                integerConstant = mathematic.Right as IntegerConstantExpression;

                switch (mathematic.Operation)
                {
                    case MathematicOperation.Add:
                        if (integerConstant == null) // offset cannot be a memory read because we need the size field for the final read
                            return new ErrorExpression("Cannot construct single address lookup from multiple memory references", mathematic);

                        accessor.Field = new Field
                        {
                            Type = accessor.Field.Type,
                            Size = accessor.Field.Size,
                            Value = (uint)integerConstant.Value
                        };
                        return accessor;

                    case MathematicOperation.Subtract:
                        if (integerConstant == null) // offset cannot be a memory read because we need the size field for the final read
                            return new ErrorExpression("Cannot construct single address lookup from multiple memory references", mathematic);

                        accessor.Field = new Field
                        {
                            Type = accessor.Field.Type,
                            Size = accessor.Field.Size,
                            Value = (uint)(-integerConstant.Value)
                        };
                        break;
                }

                Field field;
                if (integerConstant != null)
                {
                    field = new Field
                    {
                        Type = FieldType.Value,
                        Size = FieldSize.DWord,
                        Value = (uint)integerConstant.Value
                    };
                }
                else
                {
                    var accessorOperand = mathematic.Right as MemoryAccessorExpression;
                    if (accessorOperand == null)
                        return new ErrorExpression("Cannot create pointer", mathematic);

                    if (!accessor.PointerChainMatches(accessorOperand))
                        return new ErrorExpression("Cannot create pointer", mathematic);

                    field = accessorOperand.Field;
                }

                Requirement requirement = accessor.PointerChain.Last();
                requirement.Right = field;

                switch (mathematic.Operation)
                {
                    case MathematicOperation.Multiply:
                        requirement.Operator = RequirementOperator.Multiply;
                        break;

                    case MathematicOperation.Divide:
                        requirement.Operator = RequirementOperator.Divide;
                        break;

                    case MathematicOperation.BitwiseAnd:
                        requirement.Operator = RequirementOperator.BitwiseAnd;
                        break;
                }

                return accessor;
            }

            var builder = new StringBuilder();
            builder.Append("Cannot convert to an address: ");
            address.AppendString(builder);

            return new ErrorExpression(builder.ToString(), address);
        }
    }
}
