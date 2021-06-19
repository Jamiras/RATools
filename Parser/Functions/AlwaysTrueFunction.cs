using RATools.Data;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class AlwaysTrueFunction : TriggerBuilderContext.FunctionDefinition
    {
        public AlwaysTrueFunction()
            : base("always_true")
        {
        }

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            context.Trigger.Add(CreateAlwaysTrueRequirement());
            return null;
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
