using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using RATools.Tests.Data;
using System.Text;

namespace RATools.Tests.Parser.Expressions
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
    }
}
