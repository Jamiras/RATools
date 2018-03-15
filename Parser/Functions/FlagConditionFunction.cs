using RATools.Data;
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

        protected override void ModifyRequirements(ScriptInterpreterAchievementBuilder builder)
        {
            builder.CoreRequirements.Last().Type = _type;
        }
    }
}
