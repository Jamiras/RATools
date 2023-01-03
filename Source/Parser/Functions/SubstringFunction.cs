using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using System;

namespace RATools.Parser.Functions
{
    internal class SubstringFunction : FunctionDefinitionExpression
    {
        public SubstringFunction()
            : base("substring")
        {
            Parameters.Add(new VariableDefinitionExpression("string"));
            Parameters.Add(new VariableDefinitionExpression("offset"));
            Parameters.Add(new VariableDefinitionExpression("length"));

            DefaultParameters["length"] = new IntegerConstantExpression(int.MaxValue);
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var stringExpression = GetStringParameter(scope, "string", out result);
            if (stringExpression == null)
                return false;

            var offset = GetIntegerParameter(scope, "offset", out result);
            if (offset == null)
                return false;

            var length = GetIntegerParameter(scope, "length", out result);
            if (length == null)
                return false;

            var str = stringExpression.Value;
            var start = (offset.Value < 0) ? str.Length + offset.Value : offset.Value;
            var end = (length.Value == int.MaxValue) ? str.Length :
                Math.Min(str.Length, ((length.Value < 0) ? str.Length : start) + length.Value);
            start = Math.Max(0, start);

            if (start > str.Length || end <= start)
                result = new StringConstantExpression("");
            else
                result = new StringConstantExpression(str.Substring(start, end - start));

            return true;
        }
    }
}
