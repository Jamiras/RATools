﻿using RATools.Parser.Internal;
using System;

namespace RATools.Parser.Functions
{
    internal class RichPresenceLookupFunction : RichPresenceDisplayFunction.FunctionDefinition
    {
        public RichPresenceLookupFunction()
            : base("rich_presence_lookup")
        {
            Parameters.Add(new VariableDefinitionExpression("name"));
            Parameters.Add(new VariableDefinitionExpression("expression"));
            Parameters.Add(new VariableDefinitionExpression("dictionary"));

            Parameters.Add(new VariableDefinitionExpression("fallback"));
            DefaultParameters["fallback"] = new StringConstantExpression("");
        }

        public override bool BuildMacro(RichPresenceDisplayFunction.RichPresenceDisplayContext context, InterpreterScope scope, out ExpressionBase result)
        {
            var name = GetStringParameter(scope, "name", out result);
            if (name == null)
                return false;

            var expression = GetParameter(scope, "expression", out result);
            if (expression == null)
                return false;

            var dictionary = GetDictionaryParameter(scope, "dictionary", out result);
            if (dictionary == null)
                return false;

            var fallback = GetStringParameter(scope, "fallback", out result);
            if (fallback == null)
                return false;

            var value = TriggerBuilderContext.GetValueString(expression, scope, out result);
            if (value == null)
                return false;

            var functionCall = scope.GetContext<FunctionCallExpression>();

            var error = context.RichPresence.AddLookupField(functionCall, name.Value, dictionary, fallback);
            if (error != null)
            {
                result = error;
                return false;
            }

            result = new StringConstantExpression(String.Format("@{0}({1})", name.Value, value));
            return true;
        }
    }
}
