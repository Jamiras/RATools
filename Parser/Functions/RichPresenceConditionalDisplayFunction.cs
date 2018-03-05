﻿using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class RichPresenceConditionalDisplayFunction : FunctionDefinitionExpression
    {
        public RichPresenceConditionalDisplayFunction()
            : base("rich_presence_conditional_display")
        {
            Parameters.Clear();
            Parameters.Add(new VariableExpression("condition"));
            Parameters.Add(new VariableExpression("format_string"));
            Parameters.Add(new VariableExpression("..."));
        }

        protected virtual bool SetDisplayString(RichPresenceBuilder richPresence, string displayString, InterpreterScope scope, out ExpressionBase result)
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
