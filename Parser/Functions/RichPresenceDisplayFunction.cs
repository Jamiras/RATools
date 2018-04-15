using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Internal;
using System;
using System.Diagnostics;
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

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var stringExpression = GetStringParameter(scope, "format_string", out result);
            if (stringExpression == null)
                return false;

            var context = scope.GetContext<AchievementScriptContext>();
            Debug.Assert(context != null);
            scope.Context = new RichPresenceDisplayContext { RichPresence = context.RichPresence };

            if (!ProcessRichPresenceDisplay(stringExpression, scope, out result))
                return false;

            if (!SetDisplayString(context.RichPresence, ((StringConstantExpression)result).Value, scope, out result))
                return false;

            return true;
        }

        protected virtual bool SetDisplayString(RichPresenceBuilder richPresence, string displayString, InterpreterScope scope, out ExpressionBase result)
        {
            result = null;
            richPresence.DisplayString = displayString;
            return true;
        }

        private bool ProcessRichPresenceDisplay(StringConstantExpression displayString, InterpreterScope scope, out ExpressionBase result)
        {
            result = null;

            var varargs = GetParameter(scope, "varargs", out result) as ArrayExpression;
            if (varargs == null)
            {
                if (result == null)
                    result = new ParseErrorExpression("unexpected varargs", displayString);
                return false;
            }

            var builder = ((RichPresenceDisplayContext)scope.Context).DisplayString;

            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(displayString.Value));
            while (tokenizer.NextChar != '\0')
            {
                var token = tokenizer.ReadTo('{');
                builder.Append(token.ToString());

                if (tokenizer.NextChar == '\0')
                    break;

                var positionalTokenColumn = tokenizer.Column;
                tokenizer.Advance();
                var index = tokenizer.ReadNumber();
                if (tokenizer.NextChar != '}')
                {
                    result = new ParseErrorExpression("Invalid positional token",
                                                      displayString.Line, displayString.Column + positionalTokenColumn,
                                                      displayString.Line, displayString.Column + tokenizer.Column - 1);
                    return false;
                }
                tokenizer.Advance();

                var parameterIndex = Int32.Parse(index.ToString());
                if (parameterIndex >= varargs.Entries.Count)
                {
                    result = new ParseErrorExpression("Invalid parameter index: " + parameterIndex, 
                                                      displayString.Line, displayString.Column + positionalTokenColumn,
                                                      displayString.Line, displayString.Column + tokenizer.Column - 1);
                    return false;
                }

                var richPresenceFunction = varargs.Entries[parameterIndex] as FunctionCallExpression;
                if (richPresenceFunction == null || !richPresenceFunction.FunctionName.Name.StartsWith("rich_presence_"))
                {
                    result = new ParseErrorExpression("Parameter must be a rich_presence_ function", richPresenceFunction);
                    return false;
                }

                if (!richPresenceFunction.Evaluate(scope, out result, false))
                    return false;
            }

            result = new StringConstantExpression(builder.ToString());
            return true;
        }

        internal class RichPresenceDisplayContext
        {
            public RichPresenceDisplayContext()
            {
                DisplayString = new StringBuilder();
            }

            public RichPresenceBuilder RichPresence { get; set; }
            public StringBuilder DisplayString { get; private set; }
        }
    }
}
