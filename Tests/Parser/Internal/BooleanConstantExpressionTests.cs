using NUnit.Framework;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class BooleanConstantExpressionTests
    {
        [Test]
        public void TestAppendStringTrue()
        {
            var expr = new BooleanConstantExpression(true);

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("true"));
        }

        [Test]
        public void TestAppendStringFalse()
        {
            var expr = new BooleanConstantExpression(false);

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("false"));
        }

        [Test]
        public void TestIsTrueTrue()
        {
            var expr = new BooleanConstantExpression(true);
            ErrorExpression error;
            Assert.That(expr.IsTrue(new InterpreterScope(), out error), Is.True);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void TestIsTrueFalse()
        {
            var expr = new BooleanConstantExpression(false);
            ErrorExpression error;
            Assert.That(expr.IsTrue(new InterpreterScope(), out error), Is.False);
            Assert.That(error, Is.Null);
        }
    }
}
