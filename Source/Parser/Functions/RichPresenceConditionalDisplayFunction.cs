using RATools.Parser.Expressions;
using RATools.Parser.Internal;

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
            var expression = GetParameter(scope, "condition", out result);
            if (expression == null)
                return false;

            var condition = TriggerBuilderContext.GetConditionString(expression, scope, out result);
            if (condition == null)
                return false;

            richPresence.AddConditionalDisplayString(condition, displayString);
            return true;
        }
    }
}
