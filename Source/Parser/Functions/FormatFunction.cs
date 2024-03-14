using Jamiras.Components;
using RATools.Parser.Expressions;
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

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var stringExpression = GetStringParameter(scope, "format_string", out result);
            if (stringExpression == null)
                return false;

            var varargs = EvaluateVarArgs(scope, out result, stringExpression);
            if (varargs == null)
                return false;

            result = Evaluate(stringExpression, varargs, false);
            return (result is StringConstantExpression);
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
            }

            var stringExpression = lastExpression as StringConstantExpression;
            if (stringExpression != null)
            {
                result = Evaluate(stringExpression, varargs, false);
                if (result != null)
                    return null;
            }

            return varargs;
        }

        internal static ExpressionBase Evaluate(StringConstantExpression formatString, ArrayExpression parameters, bool ignoreMissing)
        {
            var builder = new StringBuilder();

            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(formatString.Value));
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
                    return new ErrorExpression("Empty parameter index",
                                               formatString.Location.Start.Line, formatString.Location.Start.Column + positionalTokenColumn,
                                               formatString.Location.Start.Line, formatString.Location.Start.Column + tokenizer.Column - 1);
                }
                var index = tokenizer.ReadNumber();
                if (tokenizer.NextChar != '}')
                {
                    return new ErrorExpression("Invalid positional token",
                                               formatString.Location.Start.Line, formatString.Location.Start.Column + positionalTokenColumn,
                                               formatString.Location.Start.Line, formatString.Location.Start.Column + tokenizer.Column - 1);
                }
                tokenizer.Advance();

                Int32 parameterIndex;
                if (!Int32.TryParse(index.ToString(), out parameterIndex)
                    || parameterIndex < 0 || parameterIndex >= parameters.Entries.Count)
                {
                    if (ignoreMissing)
                    {
                        builder.Append('{');
                        builder.Append(parameterIndex);
                        builder.Append('}');
                        continue;
                    }

                    return new ErrorExpression("Invalid parameter index: " + index.ToString(),
                                               formatString.Location.Start.Line, formatString.Location.Start.Column + positionalTokenColumn,
                                               formatString.Location.Start.Line, formatString.Location.Start.Column + tokenizer.Column - 1);
                }

                var parameter = parameters.Entries[parameterIndex];
                if (parameter != null)
                {
                    if (!parameter.IsLiteralConstant)
                        return new ConversionErrorExpression(parameter, ExpressionType.StringConstant);

                    parameter.AppendStringLiteral(builder);
                }
            }

            return new StringConstantExpression(builder.ToString())
            {
                Location = formatString.Location
            };
        }
    }
}
