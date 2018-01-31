using NUnit.Framework;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Test.Parser.Internal
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
