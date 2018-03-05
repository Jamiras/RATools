using RATools.Data;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class PrevFunction : FunctionDefinitionExpression
    {
        public PrevFunction()
            : base("prev")
        {
            Parameters.Add(new VariableExpression("accessor"));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var context = scope.GetContext<TriggerBuilderContext>();
            if (context == null)
            {
                result = new ParseErrorExpression(Name.Name + " has no meaning outside of a trigger clause");
                return false;
            }

            Field accessor = GetMemoryAccessorParameter(scope, "accessor", out result);
            if (accessor.Type == FieldType.None)
                return false;

            var requirement = new Requirement();
            requirement.Left = new Field { Size = accessor.Size, Type = FieldType.PreviousValue, Value = accessor.Value };
            context.Trigger.Add(requirement);

            return true;
        }
    }
}
