using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System;

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

            DefaultParameters["length"] = new IntegerConstantExpression(int.MaxValue);
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

                FieldSize size;
                switch (remaining)
                {
                    case 1: size = FieldSize.Byte; break;
                    case 2: size = FieldSize.Word; break;
                    case 3: size = FieldSize.TByte; break;
                    default: size = FieldSize.DWord; break;
                }

                var scan = address.Clone();
                scan.Field = new Field { Type = address.Field.Type, Size = size, Value = address.Field.Value + (uint)offset };

                var condition = new RequirementConditionExpression()
                {
                    Left = scan,
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
