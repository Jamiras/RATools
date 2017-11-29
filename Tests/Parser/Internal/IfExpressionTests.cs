using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Internal;
using System.Linq;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class IfExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new IfExpression(new IntegerConstantExpression(1));

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("if (1)"));
            // NOTE: does not output Expressions block
        }

        private IfExpression Parse(string input)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            tokenizer.Match("if");
            var expr = IfExpression.Parse(tokenizer);
            Assert.That(expr, Is.InstanceOf<IfExpression>());
            return (IfExpression)expr;
        }

        [Test]
        public void TestParse()
        {
            var expr = Parse("if (j == 0) { j = i }");

            var builder = new StringBuilder();
            expr.Condition.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j == 0"));

            Assert.That(expr.Expressions.Count, Is.EqualTo(1));
            Assert.That(expr.ElseExpressions.Count, Is.EqualTo(0));

            builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j = i"));
        }

        [Test]
        public void TestParseElse()
        {
            var expr = Parse("if (j == 0) { j = i } else { j = 10 }");

            var builder = new StringBuilder();
            expr.Condition.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j == 0"));

            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j = i"));

            Assert.That(expr.ElseExpressions.Count, Is.EqualTo(1));

            builder = new StringBuilder();
            expr.ElseExpressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j = 10"));
        }
    }
}
