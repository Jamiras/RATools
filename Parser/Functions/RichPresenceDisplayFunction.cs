using Jamiras.Components;
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
            scope = new InterpreterScope(scope);
            scope.Context = new RichPresenceDisplayContext { RichPresence = context.RichPresence };

            string displayString;
            result = ProcessRichPresenceDisplay(stringExpression, scope, out displayString);
            if (result != null)
                return false;

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

        private ParseErrorExpression ProcessRichPresenceDisplay(StringConstantExpression stringExpression, InterpreterScope scope, out string displayString)
        {
            displayString = null;

            ExpressionBase result;
            var varargs = GetParameter(scope, "varargs", out result) as ArrayExpression;
            if (varargs == null)
            {
                var error = result as ParseErrorExpression;
                if (error == null)
                    error = new ParseErrorExpression("unexpected varargs", stringExpression);
                return error;
            }

            var context = scope.GetContext<RichPresenceDisplayContext>();
            var builder = context.DisplayString;

            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(stringExpression.Value));
            while (tokenizer.NextChar != '\0')
            {
                var token = tokenizer.ReadTo('{');
                builder.Append(token.ToString());

                if (tokenizer.NextChar == '\0')
                    break;

                var positionalTokenColumn = tokenizer.Column;
                tokenizer.Advance();
                var index = tokenizer.ReadNumber();
                if (index.IsEmpty)
                {
                    return new ParseErrorExpression("Empty parameter index",
                                                    stringExpression.Line, stringExpression.Column + positionalTokenColumn,
                                                    stringExpression.Line, stringExpression.Column + tokenizer.Column - 1);
                }
                if (tokenizer.NextChar != '}')
                {
                    return new ParseErrorExpression("Invalid positional token", 
                                                    stringExpression.Line, stringExpression.Column + positionalTokenColumn,
                                                    stringExpression.Line, stringExpression.Column + tokenizer.Column - 1);
                }
                tokenizer.Advance();

                Int32 parameterIndex;
                if (!Int32.TryParse(index.ToString(), out parameterIndex)
                    || parameterIndex < 0 || parameterIndex >= varargs.Entries.Count)
                {
                    return new ParseErrorExpression("Invalid parameter index: " + index.ToString(),
                                                    stringExpression.Line, stringExpression.Column + positionalTokenColumn,
                                                    stringExpression.Line, stringExpression.Column + tokenizer.Column - 1);
                }

                var functionCall = varargs.Entries[parameterIndex] as FunctionCallExpression;
                if (functionCall == null)
                    return new ParseErrorExpression("Invalid parameter, expected function call",
                                                    varargs.Entries[parameterIndex]);

                var functionDefinition = scope.GetFunction(functionCall.FunctionName.Name);
                if (functionDefinition == null)
                    return new ParseErrorExpression("Unknown function: " + functionCall.FunctionName.Name);

                var richPresenceFunction = functionDefinition as FunctionDefinition;
                if (richPresenceFunction == null)
                    return new ParseErrorExpression(functionCall.FunctionName.Name + " cannot be called as a rich_presence_display parameter", functionCall);

                var error = richPresenceFunction.BuildMacro(context, scope, functionCall);
                if (error != null)
                    return new ParseErrorExpression(error, functionCall);
            }

            displayString = builder.ToString();
            return null;
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
                result = new ParseErrorExpression(Name.Name + " has no meaning outside of a rich_presence_display call");
                return false;
            }

            public abstract ParseErrorExpression BuildMacro(RichPresenceDisplayContext context, InterpreterScope scope, FunctionCallExpression functionCall);
        }
    }
}
