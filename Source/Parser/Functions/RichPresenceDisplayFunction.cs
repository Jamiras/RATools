using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using System.Collections.Generic;

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

            for (int parameterIndex = 0; parameterIndex < varargs.Entries.Count; parameterIndex++)
            {
                result = varargs.Entries[parameterIndex];
                var functionCall = result as FunctionCallExpression;
                if (functionCall != null)
                {
                    if (!functionCall.Evaluate(scope, out result))
                        return null;

                    varargs.Entries[parameterIndex] = result;
                }
                else
                {
                    var stringValue = result as StringConstantExpression;
                    if (stringValue == null)
                    {
                        var combine = result as IMathematicCombineExpression;
                        if (combine != null)
                        {
                            result = combine.Combine(new StringConstantExpression(""), MathematicOperation.Add);
                            varargs.Entries[parameterIndex] = result;

                            stringValue = result as StringConstantExpression;
                        }

                        if (stringValue == null)
                            stringValue = new StringConstantExpression("{" + parameterIndex + "}");
                    }

                    var richPresenceContext = scope.GetContext<RichPresenceDisplayContext>();
                    richPresenceContext.DisplayString.AddParameter(stringValue);
                }
            }

            var stringExpression = lastExpression as StringConstantExpression;
            if (stringExpression != null)
            {
                result = FormatFunction.Evaluate(stringExpression, varargs, false);
                if (result is ErrorExpression)
                    return null;
            }

            return varargs;
        }

        internal class RichPresenceDisplayContext
        {
            public RichPresenceBuilder RichPresence { get; set; }
            public RichPresenceBuilder.ConditionalDisplayString DisplayString { get; set; }
        }

        internal abstract class FunctionDefinition : FunctionDefinitionExpression
        {
            public FunctionDefinition(string name)
                : base(name)
            {
            }

            public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
            {
                var richPresenceContext = scope.GetContext<RichPresenceDisplayContext>();
                if (richPresenceContext == null)
                {
                    result = new ErrorExpression(Name.Name + " has no meaning outside of a rich_presence_display call");
                    return false;
                }

                return BuildMacro(richPresenceContext, scope, out result);
            }

            protected abstract bool BuildMacro(RichPresenceDisplayContext context, InterpreterScope scope, out ExpressionBase result);

            protected static Value GetExpressionValue(InterpreterScope scope, out ExpressionBase result)
            {
                var expression = GetParameter(scope, "expression", out result);
                if (expression == null)
                    return null;

                var requirements = new List<Requirement>();
                var context = new ValueBuilderContext { Trigger = requirements };
                var triggerBuilderScope = new InterpreterScope(scope) { Context = context };
                if (!expression.ReplaceVariables(triggerBuilderScope, out expression))
                {
                    result = expression;
                    return null;
                }

                ErrorExpression error;
                var value = ValueBuilder.BuildValue(expression, out error);
                if (value == null)
                {
                    result = error;
                    return null;
                }

                return value;
            }
        }
    }
}
