using Jamiras.Components;
using RATools.Parser.Expressions;
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

            result = Evaluate(stringExpression, varargs, false, ProcessParameter);
            return (result is StringConstantExpression);
        }

        private ArrayExpression EvaluateVarArgs(InterpreterScope scope, out ExpressionBase result, ExpressionBase lastExpression)
        {
            var varargs = GetVarArgsParameter(scope, out result, lastExpression);
            if (varargs == null)
                return null;

            for (int parameterIndex = 0; parameterIndex < varargs.Entries.Count; parameterIndex++)
            {
                var value = varargs.Entries[parameterIndex] as IValueExpression;
                if (value != null)
                    varargs.Entries[parameterIndex] = value.Evaluate(scope);
            }

            var stringExpression = lastExpression as StringConstantExpression;
            if (stringExpression != null)
            {
                result = Evaluate(stringExpression, varargs, false, ProcessParameter);
                if (result is ErrorExpression)
                    return null;
            }

            return varargs;
        }

        private static ErrorExpression ProcessParameter(StringBuilder builder, int index, ExpressionBase parameter)
        {
            if (parameter is not LiteralConstantExpressionBase)
                return new ConversionErrorExpression(parameter, ExpressionType.StringConstant);

            parameter.AppendStringLiteral(builder);
            return null;
        }

        internal static ExpressionBase Evaluate(StringConstantExpression formatString, ArrayExpression parameters, bool ignoreMissing,
            Func<StringBuilder, int, ExpressionBase, ErrorExpression> processParameter)
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
                    var error = processParameter(builder, parameterIndex, parameter);
                    if (error != null)
                        return error;
                }
            }

            return new StringConstantExpression(builder.ToString())
            {
                Location = formatString.Location
            };
        }
    }
}
