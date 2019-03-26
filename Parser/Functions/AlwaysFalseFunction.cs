using RATools.Data;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class AlwaysFalseFunction : TriggerBuilderContext.FunctionDefinition
    {
        public AlwaysFalseFunction()
            : base("always_false")
        {
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            result = new FunctionCallExpression(Name.Name, new ExpressionBase[0]);
            return true;
        }

        public override ParseErrorExpression BuildTrigger(TriggerBuilderContext context, InterpreterScope scope, FunctionCallExpression functionCall)
        {
            context.Trigger.Add(CreateAlwaysFalseRequirement());
            return null;
        }

        public static Requirement CreateAlwaysFalseRequirement()
        {
            var requirement = new Requirement();
            requirement.Left = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 0 };
            requirement.Operator = RequirementOperator.Equal;
            requirement.Right = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 1 };
            return requirement;
        }
    }
}
