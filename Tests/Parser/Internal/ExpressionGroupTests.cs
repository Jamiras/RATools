using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class ExpressionGroupTests
    {
        private ExpressionGroupCollection Parse(string input)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var parser = new ExpressionGroupCollection();
            parser.Parse(tokenizer);
            return parser;
        }

        [Test]
        public void TestAddExpression()
        {
            var group = new ExpressionGroup();
            Assert.That(group.Expressions, Is.Empty);
            Assert.That(group.ParseErrors, Is.Empty);
            Assert.That(group.IsEmpty);

            var integerConstantExpression = new IntegerConstantExpression(3);
            group.AddExpression(integerConstantExpression);
            Assert.That(!group.IsEmpty);
            Assert.That(group.Expressions.Count(), Is.EqualTo(1));
            Assert.That(group.Expressions.Contains(integerConstantExpression));
            Assert.That(group.ParseErrors, Is.Empty);

            var assignmentExpression = new AssignmentExpression(new VariableExpression("v"), new IntegerConstantExpression(6));
            group.AddExpression(assignmentExpression);
            Assert.That(!group.IsEmpty);
            Assert.That(group.Expressions.Count(), Is.EqualTo(2));
            Assert.That(group.Expressions.Contains(integerConstantExpression));
            Assert.That(group.Expressions.Contains(assignmentExpression));
            Assert.That(group.ParseErrors, Is.Empty);
        }

        [Test]
        public void TestAddParseError()
        {
            var group = new ExpressionGroup();
            Assert.That(group.Expressions, Is.Empty);
            Assert.That(group.ParseErrors, Is.Empty);
            Assert.That(group.IsEmpty);

            var parseError = new ErrorExpression("oops");
            group.AddParseError(parseError);
            Assert.That(group.ParseErrors.Count(), Is.EqualTo(1));
            Assert.That(group.ParseErrors.Contains(parseError));
            Assert.That(group.Expressions, Is.Empty);
            Assert.That(!group.IsEmpty);

            var parseError2 = new ErrorExpression("bad");
            group.AddParseError(parseError2);
            Assert.That(group.ParseErrors.Count(), Is.EqualTo(2));
            Assert.That(group.ParseErrors.Contains(parseError));
            Assert.That(group.ParseErrors.Contains(parseError2));
            Assert.That(group.Expressions, Is.Empty);
            Assert.That(!group.IsEmpty);
        }

        [Test]
        public void TestUpdateMetadata()
        {
            var group = Parse("a = 3").Groups.First(); // Parse will call UpdateMetadata
            Assert.That(group.IsEmpty, Is.False);
            Assert.That(group.FirstLine, Is.EqualTo(1));
            Assert.That(group.LastLine, Is.EqualTo(1));
            Assert.That(group.ParseErrors, Is.Empty);
            Assert.That(group.Expressions, Is.Not.Empty);
            Assert.That(group.IsDependentOn("a"), Is.False);
            Assert.That(group.Modifies.Contains("a"));
        }

        [Test]
        public void TestUpdateMetadataComment()
        {
            var group = Parse("// line 1").Groups.First();
            Assert.That(group.IsEmpty, Is.False);
            Assert.That(group.FirstLine, Is.EqualTo(1));
            Assert.That(group.LastLine, Is.EqualTo(1));
            Assert.That(group.ParseErrors, Is.Empty);
            Assert.That(group.Expressions, Is.Not.Empty);
            Assert.That(group.Modifies, Is.Empty);
        }

        [Test]
        public void TestUpdateMetadataError()
        {
            var group = Parse("a = ").Groups.First();
            Assert.That(group.IsEmpty, Is.False);
            Assert.That(group.FirstLine, Is.EqualTo(1));
            Assert.That(group.LastLine, Is.EqualTo(1));
            Assert.That(group.ParseErrors, Is.Not.Empty);
            Assert.That(group.Expressions, Is.Not.Empty);
            Assert.That(group.Modifies, Is.Empty);
        }

        [Test]
        public void TestMarkForEvaluation()
        {
            var group = Parse("a = 3").Groups.First();
            Assert.That(group.NeedsEvaluated, Is.True); // Parse will call MarkForEvaluation

            group.MarkEvaluated();
            Assert.That(group.NeedsEvaluated, Is.False);

            group.MarkForEvaluation();
            Assert.That(group.NeedsEvaluated, Is.True);
        }

        [Test]
        public void TestMarkForEvaluationComment()
        {
            var group = Parse("// line 1").Groups.First();
            Assert.That(group.NeedsEvaluated, Is.False); // comments should never be marked for evaluation

            group.MarkEvaluated();
            Assert.That(group.NeedsEvaluated, Is.False);

            group.MarkForEvaluation();
            Assert.That(group.NeedsEvaluated, Is.False);
        }

        [Test]
        public void TestMarkForEvaluationError()
        {
            var group = Parse("a = ").Groups.First();
            Assert.That(group.NeedsEvaluated, Is.False); // errors should never be marked for evaluation

            group.MarkEvaluated();
            Assert.That(group.NeedsEvaluated, Is.False);

            group.MarkForEvaluation();
            Assert.That(group.NeedsEvaluated, Is.False);
        }

        [Test]
        public void TestIsDependentOn()
        {
            var group = Parse("a = b").Groups.First(); // Parse will call UpdateMetadata
            Assert.That(group.IsDependentOn("a"), Is.False);
            Assert.That(group.IsDependentOn("b"), Is.True);

            var hashSet = new HashSet<string>();
            Assert.That(group.IsDependentOn(hashSet), Is.False);

            hashSet.Add("a");
            Assert.That(group.IsDependentOn(hashSet), Is.False);

            hashSet.Add("b");
            Assert.That(group.IsDependentOn(hashSet), Is.True);

            hashSet.Remove("a");
            Assert.That(group.IsDependentOn(hashSet), Is.True);

            hashSet.Remove("b");
            Assert.That(group.IsDependentOn(hashSet), Is.False);
        }

        [Test]
        public void GetExpressionsForLine()
        {
            var group = Parse("// a\n// b\n// c").Groups.First();
            var lines = new List<ExpressionBase>();

            Assert.That(group.GetExpressionsForLine(lines, 0), Is.False);
            Assert.That(lines.Count, Is.EqualTo(0));

            Assert.That(group.GetExpressionsForLine(lines, 4), Is.False);
            Assert.That(lines.Count, Is.EqualTo(0));

            Assert.That(group.GetExpressionsForLine(lines, 2), Is.True);
            Assert.That(lines.Count, Is.EqualTo(1));
            Assert.That(lines[0], Is.InstanceOf<CommentExpression>());
            Assert.That(((CommentExpression)lines[0]).Value, Is.EqualTo("// b"));
        }

        [Test]
        public void GetExpressionsForLineParseError()
        {
            var group = Parse("function a()\n{\n  func2()\n}\n").Groups.First();
            group.AddParseError(new ErrorExpression("Oops", 3, 8, 3, 9));
            var lines = new List<ExpressionBase>();

            Assert.That(group.GetExpressionsForLine(lines, 3), Is.True);
            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[0], Is.InstanceOf<FunctionNameExpression>());
            Assert.That(lines[1], Is.InstanceOf<ErrorExpression>());

            lines.Clear();
            Assert.That(group.GetExpressionsForLine(lines, 1), Is.True);
            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[0], Is.InstanceOf<KeywordExpression>());
            Assert.That(lines[1], Is.InstanceOf<VariableDefinitionExpression>());
        }

        [Test]
        public void GetExpressionsForLineOutOfOrder()
        {
            // dictionary items are sorted by key, so the expressions aren't in line order. make sure we can find them
            var group = Parse("a = {\n  1: \"one\",\n  3:\"three\",\n  2:\"two\"\n}\n").Groups.First();
            var lines = new List<ExpressionBase>();

            Assert.That(group.GetExpressionsForLine(lines, 4), Is.True);
            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[0], Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(lines[1], Is.InstanceOf<StringConstantExpression>());
            Assert.That(((IntegerConstantExpression)lines[0]).Value, Is.EqualTo(2));

            lines.Clear();
            Assert.That(group.GetExpressionsForLine(lines, 3), Is.True);
            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[0], Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(lines[1], Is.InstanceOf<StringConstantExpression>());
            Assert.That(((IntegerConstantExpression)lines[0]).Value, Is.EqualTo(3));
        }

        [Test]
        [TestCase("a = b", "a = b", true)]
        [TestCase("a = b", "b = a", false)]
        [TestCase("a = b", "a = b + 1", false)]
        [TestCase("a = b", "a = 3", false)]
        [TestCase("a = b", "", false)]
        [TestCase("", "a = b", false)]
        [TestCase("a = b", "if a == b { c() }", false)]
        [TestCase("a = b", "a == b", false)]
        [TestCase("a = b", "a   =   b", true)]
        [TestCase("// a", "// a", true)]
        [TestCase("// a", "// b", false)]
        [TestCase("// a", "// ab", false)]
        [TestCase("// a", "// a\n", true)]
        [TestCase("// a", "// a\n// b", false)]
        [TestCase("// a\n// b", "// a\n// b", true)]
        [TestCase("// a\n// b", "// a", false)]
        [TestCase("// a\n// b", "// a\n// b\n// c", false)]
        [TestCase("a = b", "a $ b", false)]
        [TestCase("a = b", "$a = $b", false)]
        [TestCase("$a = $b", "a = b", false)]
        public void TestExpressionsMatch(string expr1, string expr2, bool match)
        {
            var group1 = Parse(expr1).Groups.FirstOrDefault() ?? new ExpressionGroup();
            var group2 = Parse(expr2).Groups.FirstOrDefault() ?? new ExpressionGroup();

            if (match)
                Assert.That(group1.ExpressionsMatch(group2), Is.True);
            else
                Assert.That(group1.ExpressionsMatch(group2), Is.False);
        }
    }
}
