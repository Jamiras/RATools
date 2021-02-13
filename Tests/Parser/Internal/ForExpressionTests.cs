using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Internal;
using System.Collections.Generic;
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
            var expr = new ForExpression(new VariableDefinitionExpression("i"), new VariableExpression("dict"));

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

        [Test]
        public void TestNestedExpressions()
        {
            var expr = Parse("for a in [b,c] { d = a }");

            var nested = ((INestedExpressions)expr).NestedExpressions;

            Assert.That(nested.Count(), Is.EqualTo(5));
            Assert.That(nested.ElementAt(0), Is.InstanceOf<KeywordExpression>()); // for
            Assert.That(nested.ElementAt(1), Is.InstanceOf<VariableDefinitionExpression>()); // a
            Assert.That(nested.ElementAt(2), Is.InstanceOf<KeywordExpression>()); // in
            Assert.That(nested.ElementAt(3), Is.InstanceOf<ArrayExpression>()); // [b,c]
            Assert.That(nested.ElementAt(4), Is.InstanceOf<AssignmentExpression>()); // d = a
        }

        [Test]
        public void TestGetDependencies()
        {
            var expr = Parse("for a in [b,c] { d = a }");

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(2));
            Assert.That(dependencies.Contains("a"), Is.False); // iterator is self-contained
            Assert.That(dependencies.Contains("b"));
            Assert.That(dependencies.Contains("c"));
            Assert.That(dependencies.Contains("d"), Is.False); // assignment, not read
        }

        [Test]
        public void TestGetModifications()
        {
            var expr = Parse("for a in [b,c] { d = a }");

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(1));
            Assert.That(modifications.Contains("a"), Is.False); // iterator is self-contained
            Assert.That(modifications.Contains("b"), Is.False); // read, not modified
            Assert.That(modifications.Contains("c"), Is.False); // read, not modified
            Assert.That(modifications.Contains("d"));
        }
    }
}
