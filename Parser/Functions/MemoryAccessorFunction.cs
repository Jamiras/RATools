using RATools.Data;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class MemoryAccessorFunction : FunctionDefinitionExpression
    {
        public MemoryAccessorFunction(string name, FieldSize size)
            : base(name)
        {
            _size = size;

            Parameters.Add(new VariableDefinitionExpression("address"));
        }

        private readonly FieldSize _size;

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var context = scope.GetContext<TriggerBuilderContext>();
            if (context == null)
            {
                result = new ParseErrorExpression(Name.Name + " has no meaning outside of a trigger clause");
                return false;
            }

            var address = GetIntegerParameter(scope, "address", out result);
            if (address == null)
                return false;

            var requirement = new Requirement();
            requirement.Left = new Field { Size = _size, Type = FieldType.MemoryAddress, Value = (uint)address.Value };
            context.Trigger.Add(requirement);

            return true;
        }
    }
}
