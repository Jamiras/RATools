using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;

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

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var address = functionCall.Parameters.ElementAt(1);
            var result = BuildTrigger(context, scope, functionCall, address);
            if (result != null)
                return result;

            var index = ((IntegerConstantExpression)functionCall.Parameters.First()).Value;
            if (index < 0 || index > 31)
                return new ParseErrorExpression("index must be between 0 and 31", functionCall.Parameters.First());

            var offset = (uint)index / 8;
            index %= 8;

            FieldSize size;
            switch (index)
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

            var lastRequirement = context.LastRequirement;
            if (lastRequirement.Left.IsMemoryReference)
                lastRequirement.Left = new Field { Size = size, Type = lastRequirement.Left.Type, Value = lastRequirement.Left.Value + offset };
            if (lastRequirement.Right.IsMemoryReference)
                lastRequirement.Right = new Field { Size = size, Type = lastRequirement.Right.Type, Value = lastRequirement.Right.Value + offset };
            return null;
        }
    }
}
