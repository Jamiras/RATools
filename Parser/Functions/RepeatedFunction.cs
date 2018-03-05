using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class RepeatedFunction : ComparisonModificationFunction
    {
        public RepeatedFunction()
            : base("repeated")
        {
            Parameters.Clear();
            Parameters.Add(new VariableExpression("count"));
            Parameters.Add(new VariableExpression("comparison"));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var count = GetIntegerParameter(scope, "count", out result);
            if (count == null)
                return false;

            if (!base.Evaluate(scope, out result))
                return false;

            var context = scope.GetContext<TriggerBuilderContext>();
            context.LastRequirement.HitCount = (ushort)count.Value;
            return true;
        }

        protected override void ModifyRequirements(ScriptInterpreterAchievementBuilder builder)
        {
            // we want to set the HitCount on the last requirement, but don't know what to set it to. will modify back in Evaluate
        }
    }
}
