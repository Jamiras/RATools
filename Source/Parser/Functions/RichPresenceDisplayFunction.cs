using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Parser.Functions
{
    internal class RichPresenceDisplayFunction : FunctionDefinitionExpression
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

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var context = scope.GetContext<AchievementScriptContext>();
            if (context == null)
            {
                result = new ErrorExpression(Name.Name + " has no meaning outside of an achievement script");
                return false;
            }

            var formatString = GetStringParameter(scope, "format_string", out result);
            if (formatString == null)
                return false;
            if (formatString.Value == "")
            {
                result = new ErrorExpression("Empty format string not allowed", formatString);
                return false;
            }

            var richPresenceContext = new RichPresenceDisplayContext
            {
                RichPresence = context.RichPresence,
                DisplayString = context.RichPresence.AddDisplayString(null, formatString)
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

        protected ArrayExpression EvaluateVarArgs(InterpreterScope scope, out ExpressionBase result, ExpressionBase lastExpression)
        {
            var varargs = GetVarArgsParameter(scope, out result, lastExpression);
            if (varargs == null)
                return null;

            var stringExpression = lastExpression as StringConstantExpression;
            if (stringExpression != null)
            {
                var richPresenceContext = scope.GetContext<RichPresenceDisplayContext>();

                result = FormatFunction.Evaluate(stringExpression, varargs, false,
                    (StringBuilder builder, int index, ExpressionBase parameter) =>
                    {
                        // keep the placeholder - we'll need it when we serialize
                        builder.Append('{');
                        builder.Append(index);
                        builder.Append('}');

                        return ProcessParameter(parameter, index, richPresenceContext);
                    });

                if (result is ErrorExpression)
                    return null;
            }

            return varargs;
        }

        static ErrorExpression ProcessParameter(ExpressionBase parameter, int index, RichPresenceDisplayContext richPresenceContext)
        {
            var richPresenceMacro = parameter as RichPresenceMacroExpressionBase;
            if (richPresenceMacro != null)
            {
                var error = richPresenceMacro.Attach(richPresenceContext.RichPresence);
                if (error != null)
                    return error;

                var value = ValueBuilder.BuildValue(richPresenceMacro.Parameter, out error);
                if (error != null)
                    return new ErrorExpression(richPresenceMacro.FunctionName + " call failed", richPresenceMacro) { InnerError = error };

                richPresenceContext.DisplayString.AddParameter(index, richPresenceMacro, value);
                return null;
            }

            var stringValue = parameter as StringConstantExpression;
            if (stringValue == null)
            {
                var combine = parameter as IMathematicCombineExpression;
                if (combine != null)
                {
                    var result = combine.Combine(new StringConstantExpression(""), MathematicOperation.Add);
                    stringValue = result as StringConstantExpression;
                }

                if (stringValue == null)
                    stringValue = new StringConstantExpression("{" + index + "}");
            }

            richPresenceContext.DisplayString.AddParameter(index, stringValue);
            return null;
        }

        internal class RichPresenceDisplayContext
        {
            public RichPresenceBuilder RichPresence { get; set; }
            public RichPresenceBuilder.ConditionalDisplayString DisplayString { get; set; }
        }
    }
}
