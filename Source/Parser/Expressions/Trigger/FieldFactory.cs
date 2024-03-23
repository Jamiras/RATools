﻿using RATools.Data;
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
                    return CreateField(((IntegerConstantExpression)expression).Value);

                case ExpressionType.FloatConstant:
                    return CreateField(((FloatConstantExpression)expression).Value);

                case ExpressionType.MemoryAccessor:
                    var memoryAccessor = expression as MemoryAccessorExpression;
                    if (memoryAccessor != null)
                    {
                        if (memoryAccessor.PointerChain.Any())
                            break;
                        if (expression is BinaryCodedDecimalExpression)
                            return memoryAccessor.Field.ChangeType(FieldType.BinaryCodedDecimal);
                        if (expression is BitwiseInvertExpression)
                            return memoryAccessor.Field.ChangeType(FieldType.Invert);
                        return memoryAccessor.Field;
                    }
                    break;

                default:
                    break;
            }

            return new Field();
        }

        internal static Field CreateField(int value)
        {
            return new Field
            {
                Type = FieldType.Value,
                Size = FieldSize.DWord,
                Value = (uint)value
            };
        }

        internal static Field CreateField(float value)
        {
            return new Field
            {
                Type = FieldType.Float,
                Size = FieldSize.Float,
                Float = value
            };
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
                        case RequirementOperator.BitwiseXor:
                            intResult ^= right.Value;
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

                    return new Field { Type = FieldType.Float, Size = FieldSize.Float, Float = floatResult };

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

        internal static Field NegateValue(Field source)
        {
            switch (source.Type)
            {
                case FieldType.Float:
                    return new Field { Type = FieldType.Float, Size = FieldSize.Float, Float = -source.Float };

                case FieldType.Value:
                    return new Field { Type = FieldType.Value, Size = FieldSize.DWord, Value = (uint)(-(int)source.Value) };

                default:
                    return new Field();
            }
        }
    }
}
