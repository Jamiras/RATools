using RATools.Parser.Expressions;

namespace RATools.Parser.Functions
{
    internal class RichPresenceDisplayFunction : FormatFunction
    {
        public RichPresenceDisplayFunction()
            : this("rich_presence_display")
        {
            Parameters.Add(new VariableDefinitionExpression("format_string"));
            Parameters.Add(new VariableDefinitionExpression("..."));
        }

        protected RichPresenceDisplayFunction(string name)
            : base(name)
        {
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var context = scope.GetContext<AchievementScriptContext>();
            if (context == null)
            {
                result = new ErrorExpression(Name.Name + " has no meaning outside of an achievement script");
                return false;
            }

            scope = new InterpreterScope(scope)
            {
                Context = new RichPresenceDisplayContext
                {
                    RichPresence = context.RichPresence,
                }
            };

            if (!base.Evaluate(scope, out result))
                return false;

            var displayString = ((StringConstantExpression)result).Value;
            if (!SetDisplayString(context.RichPresence, displayString, scope, out result))
                return false;

            return true;
        }

        protected virtual bool SetDisplayString(RichPresenceBuilder richPresence, string displayString, InterpreterScope scope, out ExpressionBase result)
        {
            result = null;
            richPresence.DisplayString = displayString;
            var functionCall = scope.GetContext<FunctionCallExpression>();
            if (functionCall != null && functionCall.FunctionName.Name == this.Name.Name)
                richPresence.Line = functionCall.Location.Start.Line;
            return true;
        }

        internal class RichPresenceDisplayContext
        {
            public RichPresenceBuilder RichPresence { get; set; }
        }

        internal abstract class FunctionDefinition : FunctionDefinitionExpression
        {
            public FunctionDefinition(string name)
                : base(name)
            {
            }

            public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
            {
                var context = scope.GetContext<RichPresenceDisplayContext>();
                if (context == null)
                {
                    result = new ErrorExpression(Name.Name + " has no meaning outside of a rich_presence_display call");
                    return false;
                }

                return BuildMacro(context, scope, out result);
            }

            public abstract bool BuildMacro(RichPresenceDisplayContext context, InterpreterScope scope, out ExpressionBase result);
        }
    }
}
