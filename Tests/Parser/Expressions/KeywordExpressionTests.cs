using NUnit.Framework;
using RATools.Parser.Expressions;
using System.Text;

namespace RATools.Tests.Parser.Expressions
{
    [TestFixture]
    class KeywordExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new KeywordExpression("for");

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("for"));
        }
    }
}
