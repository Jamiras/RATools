using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Tests.Parser.Expressions
{
    [TestFixture]
    class IntegerConstantExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new IntegerConstantExpression(99);

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("99"));
        }

        [Test]
        public void TestAppendStringNegative()
        {
            var expr = new IntegerConstantExpression(-1);

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("-1"));
        }


        [Test]
        [TestCase("2", "+", "3", ExpressionType.IntegerConstant, "5")]
        [TestCase("2", "-", "3", ExpressionType.IntegerConstant, "-1")]
        [TestCase("2", "*", "3", ExpressionType.IntegerConstant, "6")]
        [TestCase("7", "/", "3", ExpressionType.IntegerConstant, "2")]
        [TestCase("5", "%", "3", ExpressionType.IntegerConstant, "2")]
        [TestCase("10", "&", "6", ExpressionType.IntegerConstant, "2")]
        [TestCase("2", "/", "0", ExpressionType.Error, "Division by zero")]
        [TestCase("2", "%", "0", ExpressionType.Error, "Division by zero")]
        [TestCase("2", "&", "0", ExpressionType.IntegerConstant, "0")]
        [TestCase("2", "+", "3.5", ExpressionType.FloatConstant, "5.5")]
        [TestCase("2", "-", "3.5", ExpressionType.FloatConstant, "-1.5")]
        [TestCase("2", "*", "3.5", ExpressionType.FloatConstant, "7.0")]
        [TestCase("7", "/", "3.5", ExpressionType.FloatConstant, "2.0")]
        [TestCase("5", "%", "3.5", ExpressionType.FloatConstant, "1.5")]
        [TestCase("1", "+", "\"A\"", ExpressionType.StringConstant, "\"1A\"")]
        [TestCase("5", "+", "byte(0x1234)", ExpressionType.MemoryValue, "byte(0x001234) + 5")]
        public void TestCombine(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            ExpressionTests.AssertCombine(left, operation, right, expectedType, expected);
        }

        [Test]
        [TestCase("2", "=", "3", ExpressionType.BooleanConstant, "false")]
        [TestCase("2", "!=", "3", ExpressionType.BooleanConstant, "true")]
        [TestCase("2", "<", "3", ExpressionType.BooleanConstant, "true")]
        [TestCase("2", "<=", "3", ExpressionType.BooleanConstant, "true")]
        [TestCase("2", ">", "3", ExpressionType.BooleanConstant, "false")]
        [TestCase("2", ">=", "3", ExpressionType.BooleanConstant, "false")]
        [TestCase("3", "=", "3", ExpressionType.BooleanConstant, "true")]
        [TestCase("3", "!=", "3", ExpressionType.BooleanConstant, "false")]
        [TestCase("3", "<", "3", ExpressionType.BooleanConstant, "false")]
        [TestCase("3", "<=", "3", ExpressionType.BooleanConstant, "true")]
        [TestCase("3", ">", "3", ExpressionType.BooleanConstant, "false")]
        [TestCase("3", ">=", "3", ExpressionType.BooleanConstant, "true")]
        [TestCase("5", "=", "byte(0x1234)", ExpressionType.Comparison, "byte(0x001234) == 5")]
        [TestCase("5", "!=", "byte(0x1234)", ExpressionType.Comparison, "byte(0x001234) != 5")]
        [TestCase("5", "<", "byte(0x1234)", ExpressionType.Comparison, "byte(0x001234) > 5")]
        [TestCase("5", "<=", "byte(0x1234)", ExpressionType.Comparison, "byte(0x001234) >= 5")]
        [TestCase("5", ">", "byte(0x1234)", ExpressionType.Comparison, "byte(0x001234) < 5")]
        [TestCase("5", ">=", "byte(0x1234)", ExpressionType.Comparison, "byte(0x001234) <= 5")]
        public void TestNormalizeComparison(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            ExpressionTests.AssertNormalizeComparison(left, operation, right, expectedType, expected);
        }

    }
}
