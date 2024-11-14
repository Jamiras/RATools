using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;

namespace RATools.Parser.Functions
{
    internal class BitFunction : MemoryAccessorFunction
    {
        public BitFunction()
            : base("bit", FieldSize.None)
        {
            Parameters.Clear();
            Parameters.Add(new VariableDefinitionExpression("index"));
            Parameters.Add(new VariableDefinitionExpression("address"));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var index = GetIntegerParameter(scope, "index", out result);
            if (index == null)
                return false;

            if (index.Value < 0 || index.Value > 31)
            {
                result = new ErrorExpression("index must be between 0 and 31", index);
                return false;
            }

            var address = GetParameter(scope, "address", out result);
            if (address == null)
                return false;

            var offset = (uint)index.Value / 8;
            FieldSize size;
            switch (index.Value % 8)
            {
                default:
                case 0: size = FieldSize.Bit0; break;
                case 1: size = FieldSize.Bit1; break;
                case 2: size = FieldSize.Bit2; break;
                case 3: size = FieldSize.Bit3; break;
                case 4: size = FieldSize.Bit4; break;
                case 5: size = FieldSize.Bit5; break;
                case 6: size = FieldSize.Bit6; break;
                case 7: size = FieldSize.Bit7; break;
            }

            result = CreateMemoryAccessorExpression(address);
            if (result.Type == ExpressionType.Error)
                return false;

            var accessor = result as MemoryAccessorExpression;
            if (accessor != null)
                accessor.Field = new Field { Type = accessor.Field.Type, Size = size, Value = accessor.Field.Value + offset };

            CopyLocation(result, scope);
            return true;
        }
    }
}
