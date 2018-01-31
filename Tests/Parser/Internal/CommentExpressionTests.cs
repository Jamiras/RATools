using NUnit.Framework;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Test.Parser.Internal
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
