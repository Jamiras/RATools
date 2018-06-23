using RATools.Data;
using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class RichPresenceValueFunction : FunctionDefinitionExpression
    {
        public RichPresenceValueFunction()
            : base("rich_presence_value")
        {
            Parameters.Add(new VariableDefinitionExpression("name"));
            Parameters.Add(new VariableDefinitionExpression("expression"));
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var context = scope.GetContext<RichPresenceDisplayFunction.RichPresenceDisplayContext>();
            if (context == null)
            {
                result = new ParseErrorExpression(Name.Name + " has no meaning outside of a rich_presence_display call");
                return false;
            }

            var name = GetStringParameter(scope, "name", out result);
            if (name == null)
                return false;

            var expression = GetParameter(scope, "expression", out result);
            if (expression == null)
                return false;

            var value = TriggerBuilderContext.GetValueString(expression, scope, out result);
            if (value == null)
                return false;

            context.RichPresence.AddValueField(name.Value);

            context.DisplayString.Append('@');
            context.DisplayString.Append(name.Value);
            context.DisplayString.Append('(');
            context.DisplayString.Append(value);
            context.DisplayString.Append(')');
            return true;
        }
    }
}
