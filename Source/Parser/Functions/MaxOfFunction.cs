using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;

namespace RATools.Parser.Functions
{
    internal class MaxOfFunction : FunctionDefinitionExpression
    {
        public MaxOfFunction()
            : base("max_of")
        {
            Parameters.Add(new VariableDefinitionExpression("..."));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            return ReplaceVariables(scope, out result);
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var varargs = GetVarArgsParameter(scope, out result, null, true);
            if (varargs == null)
                return false;

            var maxOf = new MaxOfRequirementExpression();
            CopyLocation(maxOf);

            foreach (var entry in varargs.Entries)
            {
                if (!entry.ReplaceVariables(scope, out result))
                    return false;

                var requirement = RequirementExpressionBase.ConvertToRequirementExpression(result);
                if (requirement == null)
                {
                    result = new ErrorExpression("Cannot convert " + result.Type.ToLowerString() + " to value", entry);
                    return false;
                }

                maxOf.AddValue(requirement);
            }

            result = maxOf;
            return true;
        }
    }
}
