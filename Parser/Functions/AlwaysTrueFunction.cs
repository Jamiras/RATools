using RATools.Data;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class AlwaysTrueFunction : FunctionDefinitionExpression
    {
        public AlwaysTrueFunction()
            : base("always_true")
        {
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var context = scope.GetContext<TriggerBuilderContext>();
            if (context == null)
            {
                result = new ParseErrorExpression(Name.Name + " has no meaning outside of a trigger clause");
                return false;
            }

            context.Trigger.Add(CreateAlwaysTrueRequirement());

            result = null;
            return true;
        }

        public static Requirement CreateAlwaysTrueRequirement()
        {
            var requirement = new Requirement();
            requirement.Left = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 1 };
            requirement.Operator = RequirementOperator.Equal;
            requirement.Right = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 1 };
            return requirement;
        }
    }
}
