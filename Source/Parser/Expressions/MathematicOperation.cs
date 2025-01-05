using RATools.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RATools.Parser.Expressions
{

    /// <summary>
    /// Specifies how the two sides of the <see cref="MathematicExpression"/> should be combined.
    /// </summary>
    public enum MathematicOperation
    {
        /// <summary>
        /// Unspecified
        /// </summary>
        None = 0,

        /// <summary>
        /// Add the two values.
        /// </summary>
        Add,

        /// <summary>
        /// Subtract the second value from the first.
        /// </summary>
        Subtract,

        /// <summary>
        /// Multiply the two values.
        /// </summary>
        Multiply,

        /// <summary>
        /// Divide the first value by the second.
        /// </summary>
        Divide,

        /// <summary>
        /// Get the remainder from dividing the first value by the second.
        /// </summary>
        Modulus,

        /// <summary>
        /// Gets the bits that are set in both the first value and the second.
        /// </summary>
        BitwiseAnd,

        /// <summary>
        /// Gets the bits that are set in either the first or and the second, but not both.
        /// </summary>
        BitwiseXor,

        /// <summary>
        /// Toggles all bits in the first value (effectively XOR 0xFFFFFFFF)
        /// </summary>
        BitwiseInvert,
    }

    public static class MathematicOperatorExtension
    {
        public static string ToOperatorString(this MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Add: return "+";
                case MathematicOperation.Subtract: return "-";
                case MathematicOperation.Multiply: return "*";
                case MathematicOperation.Divide: return "/";
                case MathematicOperation.Modulus: return "%";
                case MathematicOperation.BitwiseAnd: return "&";
                case MathematicOperation.BitwiseXor: return "^";
                case MathematicOperation.BitwiseInvert: return "~";
                default: return "[?]";
            }
        }

        public static string ToOperatorTypeString(this MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Add: return "addition";
                case MathematicOperation.Subtract: return "subtraction";
                case MathematicOperation.Multiply: return "multiplication";
                case MathematicOperation.Divide: return "division";
                case MathematicOperation.Modulus: return "modulus";
                case MathematicOperation.BitwiseAnd: return "bitwise and";
                case MathematicOperation.BitwiseXor: return "bitwise xor";
                case MathematicOperation.BitwiseInvert: return "bitwise invert";
                default: return "mathematic";
            }
        }


        public static string ToOperatorVerbString(this MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Add: return "add";
                case MathematicOperation.Subtract: return "subtract";
                case MathematicOperation.Multiply: return "multiply";
                case MathematicOperation.Divide: return "divide";
                case MathematicOperation.Modulus: return "modulus";
                case MathematicOperation.BitwiseAnd: return "bitwise and";
                case MathematicOperation.BitwiseXor: return "bitwise xor";
                case MathematicOperation.BitwiseInvert: return "bitwise invert";
                default: return "mathematic";
            }
        }

        public static MathematicOperation OppositeOperation(this MathematicOperation op)
        {
            switch (op)
            {
                case MathematicOperation.Add: return MathematicOperation.Subtract;
                case MathematicOperation.Subtract: return MathematicOperation.Add;
                case MathematicOperation.Multiply: return MathematicOperation.Divide;
                case MathematicOperation.Divide: return MathematicOperation.Multiply;
                default: return MathematicOperation.None;
            }
        }

        public static bool IsCommutative(this MathematicOperation op)
        {
            switch (op)
            {
                case MathematicOperation.Multiply:
                case MathematicOperation.Add:
                case MathematicOperation.BitwiseXor:
                case MathematicOperation.BitwiseAnd:
                    return true;

                default:
                    return false;
            }
        }

        public static RequirementOperator ToRequirementOperator(this MathematicOperation operation)
        {
            switch (operation)
            {
                case MathematicOperation.Add: return RequirementOperator.Add;
                case MathematicOperation.Subtract: return RequirementOperator.Subtract;
                case MathematicOperation.Multiply: return RequirementOperator.Multiply;
                case MathematicOperation.Divide: return RequirementOperator.Divide;
                case MathematicOperation.Modulus: return RequirementOperator.Modulus;
                case MathematicOperation.BitwiseAnd: return RequirementOperator.BitwiseAnd;
                case MathematicOperation.BitwiseXor: return RequirementOperator.BitwiseXor;
                default: return RequirementOperator.None;
            }
        }

        public static MathematicOperation ToMathematicOperator(this RequirementOperator operation)
        {
            switch (operation)
            {
                case RequirementOperator.Add: return MathematicOperation.Add;
                case RequirementOperator.Subtract: return MathematicOperation.Subtract;
                case RequirementOperator.Multiply: return MathematicOperation.Multiply;
                case RequirementOperator.Divide: return MathematicOperation.Divide;
                case RequirementOperator.Modulus: return MathematicOperation.Modulus;
                case RequirementOperator.BitwiseAnd: return MathematicOperation.BitwiseAnd;
                case RequirementOperator.BitwiseXor: return MathematicOperation.BitwiseXor;
                default: return MathematicOperation.None;
            }
        }
    }
}
