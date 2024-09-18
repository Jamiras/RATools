using RATools.Parser.Expressions;

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

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var context = scope.GetContext<AchievementScriptContext>();
            if (context == null)
            {
                result = new ErrorExpression(Name.Name + " has no meaning outside of an achievement script");
                return false;
            }

            var condition = GetRequirementParameter(scope, "condition", out result);
            if (condition == null)
                return false;

            ErrorExpression error;
            var trigger = TriggerBuilder.BuildTrigger(condition, context.SerializationContext, out error);
            if (trigger == null)
            {
                result = error;
                return false;
            }

            var formatString = GetStringParameter(scope, "format_string", out result);
            if (formatString == null)
                return false;

            var richPresenceContext = new RichPresenceDisplayContext
            {
                RichPresence = context.RichPresence,
                DisplayString = context.RichPresence.AddDisplayString(trigger, formatString),
                SerializationContext = context.SerializationContext
            };
            var richPresenceScope = new InterpreterScope(scope)
            {
                Context = richPresenceContext
            };

            var parameters = EvaluateVarArgs(richPresenceScope, out result, formatString);
            if (parameters == null)
                return false;

            var functionCall = scope.GetContext<FunctionCallExpression>();
            if (functionCall != null && functionCall.FunctionName.Name == this.Name.Name)
                context.RichPresence.Line = functionCall.Location.Start.Line;

            return true;
        }
    }
}
