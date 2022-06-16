using RATools.Parser.Expressions;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class TallyOfFunction : ArrayMapFunction
    {
        public TallyOfFunction()
            : base("tally_of")
        {
            Parameters.Clear();
            Parameters.Add(new VariableDefinitionExpression("inputs"));
            Parameters.Add(new VariableDefinitionExpression("count"));
            Parameters.Add(new VariableDefinitionExpression("predicate"));
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            var count = GetIntegerParameter(scope, "count", out result);
            if (count == null)
                return false;

            if (!base.ReplaceVariables(scope, out result))
                return false;

            result = new FunctionCallExpression("tally", new ExpressionBase[] { count, result });
            CopyLocation(result);
            return true;
        }
    }
}
