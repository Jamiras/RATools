using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using System.Diagnostics;

namespace RATools.Parser.Functions
{
    internal class RichPresenceConditionalDisplayFunction : RichPresenceDisplayFunction
    {
        public RichPresenceConditionalDisplayFunction()
            : base("rich_presence_conditional_display")
        {
            Parameters.Add(new VariableDefinitionExpression("condition"));
            Parameters.Add(new VariableDefinitionExpression("format_string"));
            Parameters.Add(new VariableDefinitionExpression("..."));
        }

        protected override bool SetDisplayString(RichPresenceBuilder richPresence, string displayString, InterpreterScope scope, out ExpressionBase result)
        {
            var expression = GetRequirementParameter(scope, "condition", out result);
            if (expression == null)
                return false;

            var scriptContext = scope.GetContext<AchievementScriptContext>();
            var serializationContext = (scriptContext != null) ? scriptContext.SerializationContext : new Data.SerializationContext();

            var condition = TriggerBuilderContext.GetConditionString(expression, scope, serializationContext, out result);
            if (condition == null)
                return false;

            richPresence.AddConditionalDisplayString(condition, displayString);
            return true;
        }
    }
}
