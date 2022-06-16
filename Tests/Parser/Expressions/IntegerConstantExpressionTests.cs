using NUnit.Framework;
using RATools.Parser.Expressions;
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
    }
}
