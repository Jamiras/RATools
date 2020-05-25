using RATools.Data;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class TallyFunction : RepeatedFunction
    {
        public TallyFunction()
            : base("tally")
        {
            Parameters.Clear();
            Parameters.Add(new VariableDefinitionExpression("count"));
            Parameters.Add(new VariableDefinitionExpression("comparison"));

            // explicitly use AddHits to join clauses
            _orNextFlag = RequirementType.AddHits;
        }
    }
}
