using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;

namespace RATools.Parser.Functions
{
    internal class AlwaysFalseFunction : FunctionDefinitionExpression
    {
        public AlwaysFalseFunction()
            : base("always_false")
        {
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            result = new AlwaysFalseExpression();
            CopyLocation(result, scope);
            return true;
        }

        public static FunctionCallExpression CreateAlwaysFalseFunctionCall()
        {
            return new FunctionCallExpression("always_false", new ExpressionBase[0]);
        }

        public static Requirement CreateAlwaysFalseRequirement()
        {
            return Requirement.CreateAlwaysFalseRequirement();
        }
    }
}
