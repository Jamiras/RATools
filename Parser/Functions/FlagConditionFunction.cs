using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class FlagConditionFunction : ComparisonModificationFunction
    {
        public FlagConditionFunction(string name, RequirementType type)
            : base(name)
        {
            _type = type;
        }

        private readonly RequirementType _type;

        protected override ParseErrorExpression ModifyRequirements(AchievementBuilder builder)
        {
            builder.CoreRequirements.Last().Type = _type;
            return null;
        }
    }
}
