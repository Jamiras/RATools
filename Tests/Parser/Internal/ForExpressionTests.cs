using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Internal;
using System.Linq;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class ForExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new ForExpression(new VariableExpression("i"), new VariableExpression("dict"));

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("for i in dict"));
            // NOTE: does not output Expressions block
        }

        private ForExpression Parse(string input)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            tokenizer.Match("for");
            var expr = ForExpression.Parse(tokenizer);
            Assert.That(expr, Is.InstanceOf<ForExpression>());
            return (ForExpression)expr;
        }

        [Test]
        public void TestParse()
        {
            var expr = Parse("for i in dict { j = i }");
            Assert.That(expr.IteratorName.Name, Is.EqualTo("i"));
            Assert.That(expr.Range, Is.EqualTo(new VariableExpression("dict")));
            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            var builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j = i"));
        }
    }
}
