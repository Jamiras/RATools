using RATools.Parser.Internal;
using System.Diagnostics;

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
            Debug.Assert(context != null);

            scope = new InterpreterScope(scope);
            scope.Context = new RichPresenceDisplayContext
            {
                RichPresence = context.RichPresence,
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
                richPresence.Line = functionCall.Line;
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

            protected bool IsInRichPresenceDisplayClause(InterpreterScope scope, out ExpressionBase result)
            {
                var richPresence = scope.GetContext<RichPresenceDisplayContext>(); // explicitly in rich_presence_display clause
                if (richPresence == null)
                {
                    var assignment = scope.GetInterpreterContext<AssignmentExpression>(); // in generic assignment clause - may be used byte rich_presence_display - will determine later
                    if (assignment == null)
                    {
                        result = new ParseErrorExpression(Name.Name + " has no meaning outside of a rich_presence_display call");
                        return false;
                    }
                }

                result = null;
                return true;
            }

            public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
            {
                return IsInRichPresenceDisplayClause(scope, out result);
            }

            public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
            {
                var context = scope.GetContext<RichPresenceDisplayContext>();
                if (context == null)
                {
                    result = new ParseErrorExpression(Name.Name + " has no meaning outside of a rich_presence_display call");
                    return false;
                }

                return BuildMacro(context, scope, out result);
            }

            public abstract bool BuildMacro(RichPresenceDisplayContext context, InterpreterScope scope, out ExpressionBase result);
        }
    }
}
