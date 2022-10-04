using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using System.Text;

namespace RATools.Tests.Parser.Expressions
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

        [Test]
        [TestCase("true", "=", "true", ExpressionType.BooleanConstant, "true")]
        [TestCase("true", "=", "false", ExpressionType.BooleanConstant, "false")]
        [TestCase("false", "=", "true", ExpressionType.BooleanConstant, "false")]
        [TestCase("false", "=", "false", ExpressionType.BooleanConstant, "true")]
        [TestCase("true", "!=", "true", ExpressionType.BooleanConstant, "false")]
        [TestCase("true", "!=", "false", ExpressionType.BooleanConstant, "true")]
        [TestCase("false", "!=", "true", ExpressionType.BooleanConstant, "true")]
        [TestCase("false", "!=", "false", ExpressionType.BooleanConstant, "false")]
        [TestCase("true", ">", "false", ExpressionType.Error, "Cannot perform relative comparison on boolean values")]
        [TestCase("true", "<", "false", ExpressionType.Error, "Cannot perform relative comparison on boolean values")]
        public void TestNormalizeComparison(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            ExpressionTests.AssertNormalizeComparison(left, operation, right, expectedType, expected);
        }
    }
}
