using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Parser.Tests.Expressions
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
            Assert.That(builder.ToString(), Is.EqualTo("if (1) { ... }"));
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

        [Test]
        public void TestParseElseIf()
        {
            var expr = Parse("if (j == 0) { j = i } else if (j == 2) { j = 10 }");

            var builder = new StringBuilder();
            expr.Condition.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j == 0"));

            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j = i"));

            Assert.That(expr.ElseExpressions.Count, Is.EqualTo(1));

            var elseIfExpr = expr.ElseExpressions.First() as IfExpression;
            Assert.That(elseIfExpr, Is.Not.Null);

            builder = new StringBuilder();
            elseIfExpr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j = 10"));

            Assert.That(elseIfExpr.ElseExpressions.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestParseElseIfNoSpace()
        {
            var input = "if (j == 0) { j = i } elseif (j == 2) { j = 10 }";
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            tokenizer.Match("if");
            var expr = IfExpression.Parse(tokenizer) as IfExpression;
            Assert.That(expr, Is.Not.Null);

            var builder = new StringBuilder();
            expr.Condition.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j == 0"));

            Assert.That(expr.Expressions.Count, Is.EqualTo(1));

            builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j = i"));

            Assert.That(expr.ElseExpressions.Count, Is.EqualTo(0));

            tokenizer.SkipWhitespace();
            Assert.That(tokenizer.MatchSubstring("elseif"), Is.EqualTo(6));
        }

        [Test]
        public void TestParseNoParentheses()
        {
            var expr = Parse("if j == 0 { j = i }");

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
        public void TestParseNoParenthesesMultipleClauses()
        {
            var expr = Parse("if j == 0 || j == 1 { j = i }");

            var builder = new StringBuilder();
            expr.Condition.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j == 0 || j == 1"));

            Assert.That(expr.Expressions.Count, Is.EqualTo(1));
            Assert.That(expr.ElseExpressions.Count, Is.EqualTo(0));

            builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j = i"));
        }

        [Test]
        public void TestParseParenthesesMultipleClauses()
        {
            var expr = Parse("if (j == 0) || (j == 1) { j = i }");

            var builder = new StringBuilder();
            expr.Condition.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("(j == 0) || (j == 1)"));

            Assert.That(expr.Expressions.Count, Is.EqualTo(1));
            Assert.That(expr.ElseExpressions.Count, Is.EqualTo(0));

            builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j = i"));
        }

        [Test]
        public void TestParseBooleanVariable()
        {
            var expr = Parse("if (someBoolean) { j = i }");

            Assert.That(expr.Condition, Is.InstanceOf<VariableExpression>());
            Assert.That(((VariableExpression)expr.Condition).Name, Is.EqualTo("someBoolean"));

            Assert.That(expr.Expressions.Count, Is.EqualTo(1));
            Assert.That(expr.ElseExpressions.Count, Is.EqualTo(0));

            var builder = new StringBuilder();
            expr.Expressions.First().AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo("j = i"));
        }

        [Test]
        public void TestNestedExpressions()
        {
            var expr = Parse("if (a == 3) { b = c + 4 } else { d = e }");

            var nested = ((INestedExpressions)expr).NestedExpressions;

            Assert.That(nested.Count(), Is.EqualTo(5));
            Assert.That(nested.ElementAt(0), Is.InstanceOf<KeywordExpression>());    // if
            Assert.That(nested.ElementAt(1), Is.InstanceOf<ComparisonExpression>()); // a == 3
            Assert.That(nested.ElementAt(2), Is.InstanceOf<AssignmentExpression>()); // b = c + 4
            Assert.That(nested.ElementAt(3), Is.InstanceOf<KeywordExpression>());    // else
            Assert.That(nested.ElementAt(4), Is.InstanceOf<AssignmentExpression>()); // d = e
        }

        [Test]
        public void TestGetDependencies()
        {
            var expr = Parse("if (a == 3) { b = c + 4 } else { d = e }");

            var dependencies = new HashSet<string>();
            ((INestedExpressions)expr).GetDependencies(dependencies);

            Assert.That(dependencies.Count, Is.EqualTo(3));
            Assert.That(dependencies.Contains("a"));
            Assert.That(dependencies.Contains("c"));
            Assert.That(dependencies.Contains("e"));
        }

        [Test]
        public void TestGetModifications()
        {
            var expr = Parse("if (a == 3) { b = c + 4 } else { d = e }");

            var modifications = new HashSet<string>();
            ((INestedExpressions)expr).GetModifications(modifications);

            Assert.That(modifications.Count, Is.EqualTo(2));
            Assert.That(modifications.Contains("b"));
            Assert.That(modifications.Contains("d"));
        }
    }
}
