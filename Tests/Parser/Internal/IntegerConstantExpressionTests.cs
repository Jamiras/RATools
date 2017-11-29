using NUnit.Framework;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Test.Parser.Internal
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
