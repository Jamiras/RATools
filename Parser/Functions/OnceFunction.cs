using System.Linq;

namespace RATools.Parser.Functions
{
    internal class OnceFunction : ComparisonModificationFunction
    {
        public OnceFunction()
            : base("once")
        {
        }

        protected override void ModifyRequirements(ScriptInterpreterAchievementBuilder builder)
        {
            builder.CoreRequirements.Last().HitCount = 1;
        }
    }
}
