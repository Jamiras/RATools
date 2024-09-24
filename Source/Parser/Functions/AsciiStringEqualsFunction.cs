using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using System;
using System.Linq;

namespace RATools.Parser.Functions
{
    internal class AsciiStringEqualsFunction : FunctionDefinitionExpression
    {
        public AsciiStringEqualsFunction()
            : base("ascii_string_equals")
        {
            Parameters.Add(new VariableDefinitionExpression("address"));
            Parameters.Add(new VariableDefinitionExpression("string"));
            Parameters.Add(new VariableDefinitionExpression("length"));
            Parameters.Add(new VariableDefinitionExpression("transform"));

            DefaultParameters["length"] = new IntegerConstantExpression(int.MaxValue);
            DefaultParameters["transform"] = new FunctionReferenceExpression("identity_transform");
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            return Evaluate(scope, out result);
        }

        public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
        {
            var address = GetMemoryAddressParameter(scope, "address", out result);
            if (address == null)
                return false;

            var stringExpression = GetStringParameter(scope, "string", out result);
            if (stringExpression == null)
                return false;

            var length = GetIntegerParameter(scope, "length", out result);
            if (length == null)
                return false;

            if (length.Value <= 0)
            {
                result = new ErrorExpression("length must be greater than 0", length);
                return false;
            }

            var transform = GetFunctionParameter(scope, "transform", out result);
            if (transform == null)
                return false;

            if ((transform.Parameters.Count - transform.DefaultParameters.Count) != 1)
            {
                result = new ErrorExpression("transform function must accept a single parameter");
                return false;
            }

            var transformScope = transform.CreateCaptureScope(scope);

            var transformParameter = new VariableExpression(transform.Parameters.First().Name);
            foreach (var kvp in transform.DefaultParameters)
                transformScope.AssignVariable(new VariableExpression(kvp.Key), kvp.Value);

            var str = stringExpression.Value;
            var remaining = (length.Value == int.MaxValue) ? str.Length : length.Value;
            var offset = 0;

            var clause = new RequirementClauseExpression() { Operation = ConditionalOperation.And };
            while (remaining > 0)
            {
                var value = 0;
                switch (remaining)
                {
                    default:
                        var c4 = (offset + 3 < str.Length) ? str[offset + 3] : 0;
                        if (c4 > 127)
                        {
                            result = new ErrorExpression(String.Format("Character {0} of string ({1}) cannot be converted to ASCII", offset + 3, (char)c4), stringExpression);
                            return false;
                        }
                        value |= (c4 << 24);
                        goto case 3;

                    case 3:
                        var c3 = (offset + 2 < str.Length) ? str[offset + 2] : 0;
                        if (c3 > 127)
                        {
                            result = new ErrorExpression(String.Format("Character {0} of string ({1}) cannot be converted to ASCII", offset + 2, (char)c3), stringExpression);
                            return false;
                        }
                        value |= (c3 << 16);
                        goto case 2;

                    case 2:
                        var c2 = (offset + 1 < str.Length) ? str[offset + 1] : 0;
                        if (c2 > 127)
                        {
                            result = new ErrorExpression(String.Format("Character {0} of string ({1}) cannot be converted to ASCII", offset + 1, (char)c2), stringExpression);
                            return false;
                        }
                        value |= (c2 << 8);
                        goto case 1;

                    case 1:
                        var c1 = offset < str.Length ? str[offset] : 0;
                        if (c1 > 127)
                        {
                            result = new ErrorExpression(String.Format("Character {0} of string ({1}) cannot be converted to ASCII", offset, (char)c1), stringExpression);
                            return false;
                        }
                        value |= c1;
                        break;
                }

                FieldSize size = Field.SizeForBytes(remaining);

                var scan = address.Clone();
                scan.Field = new Field { Type = address.Field.Type, Size = size, Value = address.Field.Value + (uint)offset };

                transformScope.AssignVariable(transformParameter, scan);
                if (!transform.Evaluate(transformScope, out result))
                    return false;

                var condition = new RequirementConditionExpression()
                {
                    Left = result,
                    Comparison = ComparisonOperation.Equal,
                    Right = new IntegerConstantExpression(value),
                };
                clause.AddCondition(condition);

                offset += 4;
                remaining -= 4;
            }

            result = clause;
            return true;
        }
    }
}
