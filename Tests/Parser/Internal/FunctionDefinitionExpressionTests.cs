using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Internal;
using System.Linq;
using System.Text;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class FunctionDefinitionExpressionTests
    {
        [Test]
        public void TestAppendString()
        {
            var expr = new FunctionDefinitionExpression("func");
            expr.Parameters.Add(new VariableExpression("p1"));
            expr.Parameters.Add(new VariableExpression("p2"));

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("function func(p1, p2)"));
            // NOTE: does not output Expressions block
        }

        [Test]
        public void TestAppendStringNoParameters()
        {
            var expr = new FunctionDefinitionExpression("func");

            var builder = new StringBuilder();
            expr.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("function func()"));
            // NOTE: does not output Expressions block
        }

        private FunctionDefinitionExpression Parse(string input)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            tokenizer.Match("function");
            var expr = FunctionDefinitionExpression.Parse(tokenizer);
            Assert.That(expr, Is.InstanceOf<FunctionDefinitionExpression>());
            return (FunctionDefinitionExpression)expr;
        }

        [Test]
        public void TestParseNoParameters()
        {
            var expr = Parse("function func() { j = i }");
            Assert.That(expr.Name.Name, Is.EqualTo("func"));
            Assert.That(expr.Parameters.Count, Is.EqualTo(0));
            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            var builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j = i"));
        }

        [Test]
        public void TestParseParameters()
        {
            var expr = Parse("function func(i) { j = i }");
            Assert.That(expr.Name.Name, Is.EqualTo("func"));
            Assert.That(expr.Parameters.Count, Is.EqualTo(1));
            Assert.That(expr.Parameters.First().Name, Is.EqualTo("i"));
            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            var builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j = i"));
        }

        [Test]
        public void TestParseShorthand()
        {
            var expr = Parse("function func(i) => i");
            Assert.That(expr.Name.Name, Is.EqualTo("func"));
            Assert.That(expr.Parameters.Count, Is.EqualTo(1));
            Assert.That(expr.Parameters.First().Name, Is.EqualTo("i"));
            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            var builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("return i"));
        }

        [Test]
        public void TestParseErrorInsideDefinition()
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer("function func() { if (j) { j = i } }"));
            tokenizer.Match("function");
            var expr = FunctionDefinitionExpression.Parse(tokenizer);

            Assert.That(expr, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)expr).Message, Is.EqualTo("Expected conditional statement following if"));
        }
    }
}
