using NUnit.Framework;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class FloatConstantExpressionTests
    {
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
    }
}
