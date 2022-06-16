using NUnit.Framework;
using RATools.Parser.Expressions;
using System.Text;

namespace RATools.Tests.Parser.Expressions
{
    [TestFixture]
    class CommentExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new CommentExpression("// test");

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("// test"));
        }
    }
}
