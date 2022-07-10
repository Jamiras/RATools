using RATools.Data;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Parser.Expressions.Trigger
{
    internal static class FieldFactory
    {
        internal static Field CreateField(ExpressionBase expression)
        {
            switch (expression.Type)
            {
                case ExpressionType.IntegerConstant:
                    return new Field 
                    { 
                        Type = FieldType.Value, 
                        Size = FieldSize.DWord, 
                        Value = (uint)((IntegerConstantExpression)expression).Value
                    };

                case ExpressionType.FloatConstant:
                    return new Field
                    {
                        Type = FieldType.Float,
                        Size = FieldSize.Float,
                        Float = ((FloatConstantExpression)expression).Value
                    };

                case ExpressionType.MemoryAccessor:
                    var memoryAccessor = (MemoryAccessorExpression)expression;
                    if (memoryAccessor.PointerChain.Any())
                        goto default;
                    return memoryAccessor.Field;

                default:
                    return new Field();
            }
        }

        internal static Field ApplyMathematic(Field left, RequirementOperator operation, Field right)
        {
            switch (right.Type)
            {
                case FieldType.Value:
                    if (left.Type == FieldType.Float)
                        right = ConvertToFloat(right);
                    break;

                case FieldType.Float:
                    if (left.Type == FieldType.Value)
                        left = ConvertToFloat(left);
                    break;

                default:
                    return new Field();
            }

            switch (left.Type)
            {
                case FieldType.Value:
                    var intResult = left.Value;
                    switch (operation)
                    {
                        case RequirementOperator.Multiply:
                            intResult *= right.Value;
                            break;
                        case RequirementOperator.Divide:
                            intResult /= right.Value;
                            break;
                        case RequirementOperator.BitwiseAnd:
                            intResult &= right.Value;
                            break;
                        default:
                            return new Field();
                    }

                    return new Field { Type = FieldType.Value, Size = FieldSize.DWord, Value = intResult };

                case FieldType.Float:
                    var floatResult = left.Float;
                    switch (operation)
                    {
                        case RequirementOperator.Multiply:
                            floatResult *= right.Float;
                            break;
                        case RequirementOperator.Divide:
                            floatResult /= right.Float;
                            break;
                        default:
                            return new Field();
                    }

                    return new Field { Type = FieldType.Float, Size = FieldSize.DWord, Float = floatResult };

                default:
                    return new Field();
            }
        }

        internal static Field ConvertToFloat(Field source)
        {
            switch (source.Type)
            {
                case FieldType.Float:
                    return source;

                case FieldType.Value:
                    return new Field { Type = FieldType.Float, Size = FieldSize.Float, Float = (float)(int)source.Value };

                default:
                    return new Field();
            }
        }
    }
}
