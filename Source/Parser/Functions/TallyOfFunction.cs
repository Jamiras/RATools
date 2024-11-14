using RATools.Parser.Expressions;

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

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var count = GetIntegerParameter(scope, "count", out result);
            if (count == null)
                return false;

            if (!base.Evaluate(scope, out result))
                return false;

            var array = result as ArrayExpression;
            if (array == null)
            {
                result = new ErrorExpression("Expansion of parameters did not generate an array", this);
                return false;
            }

            var tallyScope = new InterpreterScope(scope);
            tallyScope.Context = this;

            if (!TallyFunction.BuildTalliedRequirementExpression((uint)count.Value, array, tallyScope, out result))
                return false;

            CopyLocation(result, scope);
            return true;
        }
    }
}
