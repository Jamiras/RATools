using Jamiras.Components;
using Jamiras.Core.Tests;
using NUnit.Framework;
using RATools.Data.Tests;
using RATools.Parser.Expressions;
using System.Text;

namespace RATools.Parser.Tests.Expressions
{
    [TestFixture]
    class FloatConstantExpressionTests
    {
        [Test]
        [TestCase("2.0", 2.0f)]
        [TestCase("3.14", 3.14f)]
        [TestCase("-6.12345", -6.12345f)]
        public void TestParseExpression(string input, float expected)
        {
            var expr = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expr, Is.InstanceOf<FloatConstantExpression>());
            Assert.That(((FloatConstantExpression)expr).Value, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("2.0", 2.0f)]
        [TestCase("3.14", 3.14f)]
        [TestCase("-6.12345", -6.12345f)]
        public void TestParseExpressionCulture(string input, float expected)
        {
            using (var cultureOverride = new CultureOverride("fr-FR"))
            {
                var expr = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
                Assert.That(expr, Is.InstanceOf<FloatConstantExpression>());
                Assert.That(((FloatConstantExpression)expr).Value, Is.EqualTo(expected));
            }
        }

        [Test]
        [TestCase(2.0, "2.0")]
        [TestCase(3.14, "3.14")]
        [TestCase(-6.12345, "-6.12345")]
        public void TestAppendString(double value, string expected)
        {
            var expr = new FloatConstantExpression((float)value);

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(2.0, "2.0")]
        [TestCase(3.14, "3.14")]
        [TestCase(-6.12345, "-6.12345")]
        public void TestAppendStringCulture(double value, string expected)
        {
            using (var cultureOverride = new CultureOverride("fr-FR"))
            {
                var expr = new FloatConstantExpression((float)value);

                var builder = new StringBuilder();
                expr.AppendString(builder);
                Assert.That(builder.ToString(), Is.EqualTo(expected));
            }
        }

        [Test]
        [TestCase("2.5", "=", "3.5", ExpressionType.BooleanConstant, "false")]
        [TestCase("2.5", "!=", "3.5", ExpressionType.BooleanConstant, "true")]
        [TestCase("2.5", "<", "3.5", ExpressionType.BooleanConstant, "true")]
        [TestCase("2.5", "<=", "3.5", ExpressionType.BooleanConstant, "true")]
        [TestCase("2.5", ">", "3.5", ExpressionType.BooleanConstant, "false")]
        [TestCase("2.5", ">=", "3.5", ExpressionType.BooleanConstant, "false")]
        [TestCase("3.5", "=", "3.5", ExpressionType.BooleanConstant, "true")]
        [TestCase("3.5", "!=", "3.5", ExpressionType.BooleanConstant, "false")]
        [TestCase("3.5", "<", "3.5", ExpressionType.BooleanConstant, "false")]
        [TestCase("3.5", "<=", "3.5", ExpressionType.BooleanConstant, "true")]
        [TestCase("3.5", ">", "3.5", ExpressionType.BooleanConstant, "false")]
        [TestCase("3.5", ">=", "3.5", ExpressionType.BooleanConstant, "true")]
        [TestCase("3.5", "=", "4", ExpressionType.BooleanConstant, "false")]
        [TestCase("3.5", "!=", "4", ExpressionType.BooleanConstant, "true")]
        [TestCase("3.5", "<", "4", ExpressionType.BooleanConstant, "true")]
        [TestCase("3.5", "<=", "4", ExpressionType.BooleanConstant, "true")]
        [TestCase("3.5", ">", "4", ExpressionType.BooleanConstant, "false")]
        [TestCase("3.5", ">=", "4", ExpressionType.BooleanConstant, "false")]
        [TestCase("3.5", "=", "float(0x1234)", ExpressionType.Comparison, "float(0x001234) == 3.5")]
        [TestCase("3.5", "!=", "float(0x1234)", ExpressionType.Comparison, "float(0x001234) != 3.5")]
        [TestCase("3.5", "<", "float(0x1234)", ExpressionType.Comparison, "float(0x001234) > 3.5")]
        [TestCase("3.5", "<=", "float(0x1234)", ExpressionType.Comparison, "float(0x001234) >= 3.5")]
        [TestCase("3.5", ">", "float(0x1234)", ExpressionType.Comparison, "float(0x001234) < 3.5")]
        [TestCase("3.5", ">=", "float(0x1234)", ExpressionType.Comparison, "float(0x001234) <= 3.5")]
        public void TestNormalizeComparison(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            ExpressionTests.AssertNormalizeComparison(left, operation, right, expectedType, expected);
        }
    }
}
