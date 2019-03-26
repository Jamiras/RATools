using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class MemoryAccessorFunction : TriggerBuilderContext.FunctionDefinition
    {
        public MemoryAccessorFunction(string name, FieldSize size)
            : base(name)
        {
            _size = size;

            Parameters.Add(new VariableDefinitionExpression("address"));
        }

        private readonly FieldSize _size;

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var address = GetIntegerParameter(scope, "address", out result);
            if (address == null)
                return false;

            result = new FunctionCallExpression(Name.Name, new ExpressionBase[] { address });
            return true;
        }

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            var requirement = new Requirement();
            var address = (IntegerConstantExpression)functionCall.Parameters.First();
            requirement.Left = new Field { Size = _size, Type = FieldType.MemoryAddress, Value = (uint)address.Value };
            context.Trigger.Add(requirement);
            return null;
        }
    }
}
