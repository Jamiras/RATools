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
            var address = ((IntegerConstantExpression)functionCall.Parameters.ElementAt(1)).Value;

            var index = ((IntegerConstantExpression)functionCall.Parameters.First()).Value;
            if (index < 0 || index > 31)
                return new ParseErrorExpression("index must be between 0 and 31", functionCall.Parameters.First());

            address += index / 8;
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

            var requirement = new Requirement();
            requirement.Left = new Field { Size = size, Type = FieldType.MemoryAddress, Value = (uint)address };
            context.Trigger.Add(requirement);
            return null;
        }
    }
}
