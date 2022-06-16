using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class AlwaysTrueFunction : TriggerBuilderContext.FunctionDefinition
    {
        public AlwaysTrueFunction()
            : base("always_true")
        {
        }

        public override ErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            context.Trigger.Add(CreateAlwaysTrueRequirement());
            return null;
        }

        public static FunctionCallExpression CreateAlwaysTrueFunctionCall()
        {
            return new FunctionCallExpression("always_true", new ExpressionBase[0]);
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
