using Jamiras.Components;
using RATools.Parser.Internal;
using System;
using System.Text;

namespace RATools.Parser.Functions
{
    internal class FormatFunction : FunctionDefinitionExpression
    {
        public FormatFunction()
            : this("format")
        {
            Parameters.Add(new VariableDefinitionExpression("format_string"));
            Parameters.Add(new VariableDefinitionExpression("..."));
        }

        protected FormatFunction(string name)
            : base(name)
        {
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var stringExpression = GetStringParameter(scope, "format_string", out result);
            if (stringExpression == null)
                return false;

            var varargs = GetParameter(scope, "varargs", out result) as ArrayExpression;
            if (varargs == null)
            {
                if (!(result is ParseErrorExpression))
                    result = new ParseErrorExpression("unexpected varargs", stringExpression);
                return false;
            }

            var builder = new StringBuilder();

            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(stringExpression.Value));
            while (tokenizer.NextChar != '\0')
            {
                var token = tokenizer.ReadTo('{');
                builder.Append(token.ToString());

                if (tokenizer.NextChar == '\0')
                    break;

                var positionalTokenColumn = tokenizer.Column;
                tokenizer.Advance();
                if (tokenizer.NextChar == '}')
                {
                    result = new ParseErrorExpression("Empty parameter index",
                                                      stringExpression.Location.Start.Line, stringExpression.Location.Start.Column + positionalTokenColumn,
                                                      stringExpression.Location.Start.Line, stringExpression.Location.Start.Column + tokenizer.Column - 1);
                    return false;
                }
                var index = tokenizer.ReadNumber();
                if (tokenizer.NextChar != '}')
                {
                    result = new ParseErrorExpression("Invalid positional token",
                                                      stringExpression.Location.Start.Line, stringExpression.Location.Start.Column + positionalTokenColumn,
                                                      stringExpression.Location.Start.Line, stringExpression.Location.Start.Column + tokenizer.Column - 1);
                    return false;
                }
                tokenizer.Advance();

                Int32 parameterIndex;
                if (!Int32.TryParse(index.ToString(), out parameterIndex)
                    || parameterIndex < 0 || parameterIndex >= varargs.Entries.Count)
                {
                    result = new ParseErrorExpression("Invalid parameter index: " + index.ToString(),
                                                      stringExpression.Location.Start.Line, stringExpression.Location.Start.Column + positionalTokenColumn,
                                                      stringExpression.Location.Start.Line, stringExpression.Location.Start.Column + tokenizer.Column - 1);
                    return false;
                }

                result = varargs.Entries[parameterIndex];
                var functionCall = result as FunctionCallExpression;
                if (functionCall != null)
                {
                    if (!functionCall.Evaluate(scope, out result))
                        return false;
                }

                var strResult = result as StringConstantExpression;
                if (strResult != null)
                {
                    builder.Append(strResult.Value);
                }
                else
                {
                    var intResult = result as IntegerConstantExpression;
                    if (intResult != null)
                    {
                        builder.Append(intResult.Value);
                    }
                    else
                    {
                        result = new ParseErrorExpression("Cannot convert expression to string", result);
                        return false;
                    }
                }
            }

            result = new StringConstantExpression(builder.ToString());
            return true;
        }
    }
}
