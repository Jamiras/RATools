using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Tests.Parser.Expressions
{
    [TestFixture]
    class StringConstantExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new StringConstantExpression("test");

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("\"test\""));
        }

        [Test]
        [TestCase("AB", "=", "CD", ExpressionType.BooleanConstant, "false")]
        [TestCase("AB", "!=", "CD", ExpressionType.BooleanConstant, "true")]
        [TestCase("AB", "<", "CD", ExpressionType.BooleanConstant, "true")]
        [TestCase("AB", "<=", "CD", ExpressionType.BooleanConstant, "true")]
        [TestCase("AB", ">", "CD", ExpressionType.BooleanConstant, "false")]
        [TestCase("AB", ">=", "CD", ExpressionType.BooleanConstant, "false")]
        [TestCase("CD", "=", "CD", ExpressionType.BooleanConstant, "true")]
        [TestCase("CD", "!=", "CD", ExpressionType.BooleanConstant, "false")]
        [TestCase("CD", "<", "CD", ExpressionType.BooleanConstant, "false")]
        [TestCase("CD", "<=", "CD", ExpressionType.BooleanConstant, "true")]
        [TestCase("CD", ">", "CD", ExpressionType.BooleanConstant, "false")]
        [TestCase("CD", ">=", "CC", ExpressionType.BooleanConstant, "true")]
        [TestCase("CD", "=", "CZ", ExpressionType.BooleanConstant, "false")]
        [TestCase("CD", "!=", "CZ", ExpressionType.BooleanConstant, "true")]
        [TestCase("CD", "<", "CZ", ExpressionType.BooleanConstant, "true")]
        [TestCase("CD", "<=", "CZ", ExpressionType.BooleanConstant, "true")]
        [TestCase("CD", ">", "CZ", ExpressionType.BooleanConstant, "false")]
        [TestCase("CD", ">=", "CZ", ExpressionType.BooleanConstant, "false")]
        [TestCase("E", "=", "EZ", ExpressionType.BooleanConstant, "false")]
        [TestCase("E", "!=", "EZ", ExpressionType.BooleanConstant, "true")]
        [TestCase("E", "<", "EZ", ExpressionType.BooleanConstant, "true")]
        [TestCase("E", "<=", "EZ", ExpressionType.BooleanConstant, "true")]
        [TestCase("E", ">", "EZ", ExpressionType.BooleanConstant, "false")]
        [TestCase("E", ">=", "EZ", ExpressionType.BooleanConstant, "false")]
        public void TestNormalizeComparison(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            ExpressionTests.AssertNormalizeComparison('"' + left + '"', operation, '"' + right + '"', expectedType, expected);
        }
    }
}
