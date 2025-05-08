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

            result = CreateMemoryAccessorExpression(address);
            if (result.Type == ExpressionType.Error)
                return false;

            CopyLocation(result, scope);
            result.MakeReadOnly();
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

            var memoryValue = address as MemoryValueExpression;
            if (memoryValue != null)
            {
                if (memoryValue.MemoryAccessors.Count() > 1)
                {
                    accessor = memoryValue.ConvertToMemoryAccessor();
                    if (accessor != null)
                    {
                        if (accessor.Field.Size != Size)
                        {
                            if (accessor.IsReadOnly)
                                accessor = accessor.Clone();

                            accessor.Field = accessor.Field.ChangeSize(Size);
                        }
                        return accessor;
                    }

                    // chain of logic to build address has to be stored in the recall accumulator
                    accessor = new MemoryAccessorExpression();
                    accessor.RememberPointer = new RememberRecallExpression(memoryValue);
                    accessor.Field = new Field { Type = FieldType.MemoryAddress, Size = Size, Value = 0 };
                    return accessor;
                }

                var result = CreateMemoryAccessorExpression(memoryValue.MemoryAccessors.First());
                if (result != null)
                {
                    result.Field = new Field
                    {
                        Type = FieldType.MemoryAddress,
                        Size = Size,
                        Value = (uint)memoryValue.IntegerConstant
                    };
                    return result;
                }
            }

            var modifiedMemoryAccessor = address as ModifiedMemoryAccessorExpression;
            if (modifiedMemoryAccessor != null)
            {
                var result = CreateMemoryAccessorExpression(modifiedMemoryAccessor);
                if (result != null)
                {
                    result.Field = new Field
                    {
                        Type = FieldType.MemoryAddress,
                        Size = Size,
                        Value = 0 // no offset
                    };
                    return result;
                }
            }

            return new ConversionErrorExpression(address, "memory address", address.Location);
        }

        private static MemoryAccessorExpression CreateMemoryAccessorExpression(ModifiedMemoryAccessorExpression modifiedMemoryAccessor)
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
                        return CreateMemoryAccessorExpression(negativeMemoryAccessor);
                    }
                }
                else if (modifiedMemoryAccessor.ModifyingOperator == RequirementOperator.None)
                {
                    var multipliedMemoryAccessor = modifiedMemoryAccessor.Clone();
                    multipliedMemoryAccessor.Modifier = FieldFactory.CreateField(new IntegerConstantExpression(-1));
                    multipliedMemoryAccessor.ModifyingOperator = RequirementOperator.Multiply;
                    multipliedMemoryAccessor.CombiningOperator = RequirementType.AddSource;
                    return CreateMemoryAccessorExpression(multipliedMemoryAccessor);
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

            return result;
        }
    }
}
