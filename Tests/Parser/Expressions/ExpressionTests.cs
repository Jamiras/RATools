using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using System;
using System.Text;

namespace RATools.Tests.Parser.Expressions
{
    internal static class ExpressionTests
    {
        public static ExpressionBase Parse(string input)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());

            ExpressionBase result;
            if (!expr.ReplaceVariables(scope, out result))
                Assert.Fail(result.ToString());

            return result;
        }

        public static T Parse<T>(string input)
            where T : ExpressionBase
        {
            var result = Parse(input);
            Assert.That(result, Is.InstanceOf<T>());
            return (T)result;
        }

        public static void AssertAppendString(ExpressionBase expression, string expected)
        {
            var builder = new StringBuilder();
            expression.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        public static string ReplacePlaceholders(string input)
        {
            var builder = new StringBuilder();
            var size = FieldSize.Byte;

            foreach (char c in input)
            {
                if (!char.IsLetter(c))
                {
                    builder.Append(c);
                }
                else if (char.IsUpper(c))
                {
                    builder.AppendFormat("{0}(0x{1:X6})", Field.GetSizeFunction(size), (uint)(c - 'A') + 1);
                    size = FieldSize.Byte; // reset to default size
                }
                else
                {
                    // lowercase letter is size prefix (i.e. u = upper, o = bit2)
                    size = Field.Deserialize(Tokenizer.CreateTokenizer(string.Format("0x{0}0", c))).Size;
                }
            }

            return builder.ToString();
        }

        public static MathematicOperation GetMathematicOperation(string operation)
        {
            switch (operation)
            {
                case "+": return MathematicOperation.Add;
                case "-": return MathematicOperation.Subtract;
                case "*": return MathematicOperation.Multiply;
                case "/": return MathematicOperation.Divide;
                case "%": return MathematicOperation.Modulus;
                case "&": return MathematicOperation.BitwiseAnd;
                default: Assert.Fail("Unknown operation: " + operation); break;
            }

            return MathematicOperation.None;
        }

        public static void AssertCombine(string left, string operation, 
            string right, ExpressionType expectedType, string expected)
        {
            var op = GetMathematicOperation(operation);

            var leftExpr = Parse(left);
            var rightExpr = Parse(right);

            Assert.That(leftExpr, Is.InstanceOf<IMathematicCombineExpression>());
            var combining = (IMathematicCombineExpression)leftExpr;

            var result = combining.Combine(rightExpr, op);
            if (expectedType == ExpressionType.None)
            {
                Assert.That(result, Is.Null);
            }
            else
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Type, Is.EqualTo(expectedType));

                AssertAppendString(result, expected);
            }
        }

        public static void SplitComparison(string input, out string left, out string comparison, out string right)
        {
            var index = input.IndexOfAny(new char[] { '<', '>', '=', '!' });
            left = input.Substring(0, index).TrimEnd();
            var index2 = input.IndexOf(' ', index);
            comparison = input.Substring(index, index2 - index);
            right = input.Substring(index2 + 1).TrimStart();
        }

        public static ComparisonOperation GetComparisonOperation(string operation)
        {
            switch (operation)
            {
                case "=":
                case "==": return ComparisonOperation.Equal;
                case "!=": return ComparisonOperation.NotEqual;
                case "<": return ComparisonOperation.LessThan;
                case "<=": return ComparisonOperation.LessThanOrEqual;
                case ">": return ComparisonOperation.GreaterThan;
                case ">=": return ComparisonOperation.GreaterThanOrEqual;
                default: Assert.Fail("Unknown operation: " + operation); break;
            }

            return ComparisonOperation.None;
        }

        public static void AssertNormalizeComparison(string left, string operation,
            string right, ExpressionType expectedType, string expected)
        {
            var op = GetComparisonOperation(operation);

            var leftExpr = Parse(left);
            var rightExpr = Parse(right);

            AssertNormalizeComparison(
                new ComparisonExpression(leftExpr, op, rightExpr), 
                expectedType, expected);
        }

        public static void AssertNormalizeComparison(ComparisonExpression comparison, ExpressionType expectedType, string expected)
        {
            Assert.That(comparison.Left, Is.InstanceOf<IComparisonNormalizeExpression>());
            var normalizing = (IComparisonNormalizeExpression)comparison.Left;

            var result = normalizing.NormalizeComparison(comparison.Right, comparison.Operation);
            if (expectedType == ExpressionType.None)
            {
                Assert.That(result, Is.Null);
            }
            else
            {
                if (result == null)
                    result = comparison;
                Assert.That(result.Type, Is.EqualTo(expectedType));

                AssertAppendString(result, expected);
            }
        }

        public static void AssertError(ExpressionBase expression, string message)
        {
            Assert.That(expression, Is.InstanceOf<ErrorExpression>());
            var error = (ErrorExpression)expression;
            if (error.InnerError != null)
                Assert.That(error.InnermostError.Message, Is.EqualTo(message));
            else
                Assert.That(error.Message, Is.EqualTo(message));
        }
    }
}
