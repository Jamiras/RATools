using NUnit.Framework;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Test.Parser.Internal
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
    }
}
