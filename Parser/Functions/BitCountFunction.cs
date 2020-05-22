using RATools.Data;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class BitCountFunction : TriggerBuilderContext.FunctionDefinition
    {
        public BitCountFunction()
            : base("bitcount")
        {
            Parameters.Add(new VariableDefinitionExpression("accessor"));
        }

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
            CopyLocation(result);
            return true;
        }

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var accessor = (FunctionCallExpression)functionCall.Parameters.First();
            var error = context.CallFunction(accessor, scope);
            if (error != null)
                return error;

            var list = (IList<Requirement>)context.Trigger;
            var last = list.Last();
            list.RemoveAt(list.Count - 1);

            var newRequirements = new List<Requirement>();

            switch (last.Left.Size)
            {
                case FieldSize.Bit0:
                case FieldSize.Bit1:
                case FieldSize.Bit2:
                case FieldSize.Bit3:
                case FieldSize.Bit4:
                case FieldSize.Bit5:
                case FieldSize.Bit6:
                case FieldSize.Bit7:
                    // bitcount of a single bit is the value of the bit
                    newRequirements.Add(last);
                    break;

                case FieldSize.LowNibble:
                    // expand to four AddSources
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.Bit0, Type = FieldType.MemoryAddress, Value = last.Left.Value }, Type = RequirementType.AddSource });
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.Bit1, Type = FieldType.MemoryAddress, Value = last.Left.Value }, Type = RequirementType.AddSource });
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.Bit2, Type = FieldType.MemoryAddress, Value = last.Left.Value }, Type = RequirementType.AddSource });
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.Bit3, Type = FieldType.MemoryAddress, Value = last.Left.Value } });
                    break;

                case FieldSize.HighNibble:
                    // expand to four AddSources
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.Bit4, Type = FieldType.MemoryAddress, Value = last.Left.Value }, Type = RequirementType.AddSource });
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.Bit5, Type = FieldType.MemoryAddress, Value = last.Left.Value }, Type = RequirementType.AddSource });
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.Bit6, Type = FieldType.MemoryAddress, Value = last.Left.Value }, Type = RequirementType.AddSource });
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.Bit7, Type = FieldType.MemoryAddress, Value = last.Left.Value } });
                    break;

                case FieldSize.Byte:
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.BitCount, Type = FieldType.MemoryAddress, Value = last.Left.Value } });
                    break;

                case FieldSize.Word:
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.BitCount, Type = FieldType.MemoryAddress, Value = last.Left.Value }, Type = RequirementType.AddSource });
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.BitCount, Type = FieldType.MemoryAddress, Value = last.Left.Value + 1 } });
                    break;

                case FieldSize.TByte:
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.BitCount, Type = FieldType.MemoryAddress, Value = last.Left.Value }, Type = RequirementType.AddSource });
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.BitCount, Type = FieldType.MemoryAddress, Value = last.Left.Value + 1 }, Type = RequirementType.AddSource });
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.BitCount, Type = FieldType.MemoryAddress, Value = last.Left.Value + 2 } });
                    break;

                case FieldSize.DWord:
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.BitCount, Type = FieldType.MemoryAddress, Value = last.Left.Value }, Type = RequirementType.AddSource });
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.BitCount, Type = FieldType.MemoryAddress, Value = last.Left.Value + 1 }, Type = RequirementType.AddSource });
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.BitCount, Type = FieldType.MemoryAddress, Value = last.Left.Value + 2 }, Type = RequirementType.AddSource });
                    newRequirements.Add(new Requirement { Left = new Field { Size = FieldSize.BitCount, Type = FieldType.MemoryAddress, Value = last.Left.Value + 3 } });
                    break;

                default:
                    return new ParseErrorExpression("Unsupported size for bitcount: " + last.Left.Size);
            }

            if (list.Count > 0 && list.Last().Type == RequirementType.AddAddress)
            {
                var addAddress = list.Last();
                list.RemoveAt(list.Count - 1);

                foreach (var requirement in newRequirements)
                {
                    list.Add(addAddress);
                    list.Add(requirement);
                }
            }
            else
            {
                foreach (var requirement in newRequirements)
                    list.Add(requirement);
            }

            return null;
        }
    }
}
