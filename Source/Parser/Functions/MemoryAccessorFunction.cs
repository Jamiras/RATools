using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

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

            result = CreateMemoryAccessorExpression(address, Size);
            if (result == null)
            {
                result = new ConversionErrorExpression(address, "memory address", address.Location);
                return false;
            }

            CopyLocation(result, scope);
            result.MakeReadOnly();
            return true;
        }

        public static MemoryAccessorExpression CreateMemoryAccessorExpression(ExpressionBase address, FieldSize size)
        {
            var integerConstant = address as IntegerConstantExpression;
            if (integerConstant != null)
                return new MemoryAccessorExpression(FieldType.MemoryAddress, size, (uint)integerConstant.Value);

            var accessor = address as MemoryAccessorExpression;
            if (accessor != null)
            {
                var result = new MemoryAccessorExpression();
                foreach (var pointer in accessor.PointerChain)
                    result.AddPointer(pointer);

                result.AddPointer(new Requirement { Type = RequirementType.AddAddress, Left = accessor.Field });
                result.Field = new Field { Type = FieldType.MemoryAddress, Size = size, Value = 0 };
                return result;
            }

            var memoryValue = address as MemoryValueExpression;
            if (memoryValue != null)
            {
                var result = CreateMemoryAccessorExpression(memoryValue, size);
                if (result != null)
                    return result;
            }

            var modifiedMemoryAccessor = address as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null)
            {
                var result = CreateMemoryAccessorExpression(modifiedMemoryAccessor, size, 0);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static MemoryAccessorExpression CreateMemoryAccessorExpression(MemoryValueExpression memoryValue, FieldSize size)
        {
            var count = memoryValue.MemoryAccessors.Count();
            if (count == 1)
                return CreateMemoryAccessorExpression(memoryValue.MemoryAccessors.First(), size, (uint)memoryValue.IntegerConstant);

            var first = memoryValue.MemoryAccessors.First();
            bool needRemember = false;

            if (count > 2)
            {
                needRemember = true;
            }
            else if (memoryValue.HasConstant)
            {
                needRemember = true;
            }
            else if (!memoryValue.MemoryAccessors.All(m => m.ModifyingOperator == RequirementOperator.None))
            {
                needRemember = true;
            }
            else
            {
                var second = memoryValue.MemoryAccessors.ElementAt(1).MemoryAccessor;
                needRemember = !first.MemoryAccessor.PointerChainMatches(second);
            }

            MemoryAccessorExpression memoryAccessor;

            if (needRemember)
            {
                memoryAccessor = new MemoryAccessorExpression(FieldType.MemoryAddress, size, 0U);
                memoryAccessor.RememberPointer = new RememberRecallExpression(memoryValue);
            }
            else
            {
                memoryAccessor = new MemoryAccessorExpression(FieldType.MemoryAddress, size, (uint)memoryValue.IntegerConstant);

                foreach (var pointer in first.MemoryAccessor.PointerChain)
                    memoryAccessor.AddPointer(pointer);

                var requirement = new Requirement
                {
                    Type = RequirementType.AddAddress,
                    Left = first.MemoryAccessor.Field
                };

                var second = memoryValue.MemoryAccessors.ElementAt(1);
                requirement.Operator = second.CombiningOperator == RequirementType.AddSource ? RequirementOperator.Add : RequirementOperator.Subtract;
                requirement.Right = second.MemoryAccessor.Field;

                memoryAccessor.AddPointer(requirement);
            }

            memoryValue.CopyLocation(memoryAccessor);
            return memoryAccessor;
        }

        private static MemoryAccessorExpression CreateMemoryAccessorExpression(ModifiedMemoryAccessorExpression modifiedMemoryAccessor, FieldSize size, uint offset)
        {
            if (modifiedMemoryAccessor.CombiningOperator == RequirementType.SubSource)
            {
                if (modifiedMemoryAccessor.ModifyingOperator == RequirementOperator.Multiply ||
                    modifiedMemoryAccessor.ModifyingOperator == RequirementOperator.Divide)
                {
                    Field negativeValue = FieldFactory.NegateValue(modifiedMemoryAccessor.Modifier);
                    if (negativeValue.Type != FieldType.None)
                    {
                        var negativeMemoryAccessor = modifiedMemoryAccessor.Clone();
                        negativeMemoryAccessor.Modifier = negativeValue;
                        negativeMemoryAccessor.CombiningOperator = RequirementType.AddSource;
                        return CreateMemoryAccessorExpression(negativeMemoryAccessor, size, offset);
                    }
                }
                else if (modifiedMemoryAccessor.ModifyingOperator == RequirementOperator.None)
                {
                    var multipliedMemoryAccessor = modifiedMemoryAccessor.Clone();
                    multipliedMemoryAccessor.Modifier = FieldFactory.CreateField(new IntegerConstantExpression(-1));
                    multipliedMemoryAccessor.ModifyingOperator = RequirementOperator.Multiply;
                    multipliedMemoryAccessor.CombiningOperator = RequirementType.AddSource;
                    return CreateMemoryAccessorExpression(multipliedMemoryAccessor, size, offset);
                }

                return null;
            }

            var result = new MemoryAccessorExpression();

            var requirements = new List<Requirement>();
            var context = new TriggerBuilderContext();
            context.Trigger = requirements;
            modifiedMemoryAccessor.BuildTrigger(context);
            requirements.Last().Type = RequirementType.AddAddress;

            // a pointer chain can only contain AddAddress conditions.
            // however, the AddAdress can use {recall}, which can be
            // constructed from Remembering AddSource and SubSource conditions
            RequirementType accumulatorType = RequirementType.None;
            foreach (var requirement in requirements)
            {
                switch (requirement.Type)
                {
                    case RequirementType.AddAddress:
                        break;

                    case RequirementType.Remember:
                        accumulatorType = RequirementType.None;
                        break;

                    case RequirementType.AddSource:
                    case RequirementType.SubSource:
                        accumulatorType = requirement.Type;
                        break;

                    default:
                        return null;
                }
            }

            if (accumulatorType != RequirementType.None)
                return null;

            foreach (var requirement in requirements)
                result.AddPointer(requirement);

            result.Field = new Field
            {
                Type = FieldType.MemoryAddress,
                Size = size,
                Value = offset
            };

            return result;
        }
    }
}
