using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class AlwaysTrueFunction : FunctionDefinitionExpression
    {
        public AlwaysTrueFunction()
            : base("always_true")
        {
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            result = new AlwaysTrueExpression();
            CopyLocation(result);
            return true;
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
