using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Test.Parser.Internal
{
    [TestFixture]
    class ExpressionGroupCollectionTests
    {
        private ExpressionGroupCollection Parse(string input)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var parser = new ExpressionGroupCollection();
            parser.Scope = new InterpreterScope();
            parser.Parse(tokenizer);

            foreach (var group in parser.Groups)
                group.MarkEvaluated();

            return parser;
        }

        [Test]
        public void TestGetExpressionForLineComments()
        {
            var group = Parse("// line 1\n" +
                              "// line 2\n" +
                              "// line 3\n");

            var expressions = new List<ExpressionBase>();
            Assert.That(group.GetExpressionsForLine(expressions, 0), Is.False);
            Assert.That(expressions.Count, Is.EqualTo(0));

            expressions.Clear();
            Assert.That(group.GetExpressionsForLine(expressions, 1), Is.True);
            Assert.That(expressions.Count, Is.EqualTo(1));
            Assert.That(expressions[0], Is.InstanceOf<CommentExpression>());
            Assert.That(((CommentExpression)expressions[0]).Value, Is.EqualTo("// line 1"));

            expressions.Clear();
            Assert.That(group.GetExpressionsForLine(expressions, 2), Is.True);
            Assert.That(expressions.Count, Is.EqualTo(1));
            Assert.That(expressions[0], Is.InstanceOf<CommentExpression>());
            Assert.That(((CommentExpression)expressions[0]).Value, Is.EqualTo("// line 2"));

            expressions.Clear();
            Assert.That(group.GetExpressionsForLine(expressions, 3), Is.True);
            Assert.That(expressions.Count, Is.EqualTo(1));
            Assert.That(expressions[0], Is.InstanceOf<CommentExpression>());
            Assert.That(((CommentExpression)expressions[0]).Value, Is.EqualTo("// line 3"));

            expressions.Clear();
            Assert.That(group.GetExpressionsForLine(expressions, 4), Is.False);
            Assert.That(expressions.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestGetExpressionForLineAssignment()
        {
            var group = Parse("a = 3 // test1\n" +
                              "b = 4 // test2\n" +
                              "c = 5 // test3\n");

            var expressions = new List<ExpressionBase>();
            Assert.That(group.GetExpressionsForLine(expressions, 2), Is.True);
            Assert.That(expressions.Count, Is.EqualTo(3));

            var b = expressions.FirstOrDefault(e => e is VariableExpression) as VariableExpression;
            Assert.That(b, Is.Not.Null);
            Assert.That(b.Name, Is.EqualTo("b"));

            var four = expressions.FirstOrDefault(e => e is IntegerConstantExpression) as IntegerConstantExpression;
            Assert.That(four, Is.Not.Null);
            Assert.That(four.Value, Is.EqualTo(4));

            var comment = expressions.FirstOrDefault(e => e is CommentExpression) as CommentExpression;
            Assert.That(comment, Is.Not.Null);
            Assert.That(comment.Value, Is.EqualTo("// test2"));
        }

        [Test]
        public void TestExtractTrailingComments()
        {
            var group = Parse("a = 3 // test1\n" +
                              "// test2\n" +
                              "b = 4\n" +
                              "c = // test3\n" +
                              "    5\n");

            Assert.That(group.Groups.Count, Is.EqualTo(4));

            // the parser includes comments at the end of most expressions as it's looking for continuation
            // i.e. "a = 3" could be "a = 3 + 2". ExtractTrailingComments pulls any comments captured by the
            // parser after the last valid token into a separate group.

            // group0: a = 3
            var group0 = group.Groups[0];
            Assert.That(group0.Expressions.Count(), Is.EqualTo(1));
            Assert.That(group0.FirstLine, Is.EqualTo(1));
            Assert.That(group0.LastLine, Is.EqualTo(1));

            // comment extracted from group 0
            // group1: // test1\n//test2
            var group1 = group.Groups[1];
            Assert.That(group1.Expressions.Count(), Is.EqualTo(2));
            Assert.That(group1.FirstLine, Is.EqualTo(1));
            Assert.That(group1.LastLine, Is.EqualTo(2));

            // no comment
            // group2: b = 4
            var group2 = group.Groups[2];
            Assert.That(group2.Expressions.Count(), Is.EqualTo(1));
            Assert.That(group2.FirstLine, Is.EqualTo(3));
            Assert.That(group2.LastLine, Is.EqualTo(3));

            // final group spans around a comment, so that shouldn't be extracted
            // group3: c = // test3\n 5
            var group3 = group.Groups[3];
            Assert.That(group3.Expressions.Count(), Is.EqualTo(2)); // "c = 5" and "// test3"
            Assert.That(group3.FirstLine, Is.EqualTo(4));
            Assert.That(group3.LastLine, Is.EqualTo(5));
        }

        [Test]
        public void TestUpdateExtendComment()
        {
            var group = Parse("a = 3\n" +
                              "// test1\n" +
                              "// test2\n" +
                              "b = 4\n");

            Assert.That(group.Groups.Count, Is.EqualTo(3));
            Assert.That(group.Groups[0].FirstLine, Is.EqualTo(1));
            Assert.That(group.Groups[0].LastLine, Is.EqualTo(1));
            Assert.That(group.Groups[1].FirstLine, Is.EqualTo(2));
            Assert.That(group.Groups[1].LastLine, Is.EqualTo(3));
            Assert.That(group.Groups[2].FirstLine, Is.EqualTo(4));
            Assert.That(group.Groups[2].LastLine, Is.EqualTo(4));

            var updatedInput = "a = 3\n" +
                               "// test1\n" +
                               "// test2\n" +
                               "// test3\n" +
                               "b = 4\n";
            var needsEvaluated = group.Update(Tokenizer.CreateTokenizer(updatedInput), new int[] { 4 });
            Assert.That(needsEvaluated, Is.False);

            Assert.That(group.Groups.Count, Is.EqualTo(4));
            Assert.That(group.Groups[0].FirstLine, Is.EqualTo(1));
            Assert.That(group.Groups[0].LastLine, Is.EqualTo(1));
            Assert.That(group.Groups[1].FirstLine, Is.EqualTo(2));
            Assert.That(group.Groups[1].LastLine, Is.EqualTo(3));
            Assert.That(group.Groups[2].FirstLine, Is.EqualTo(4));
            Assert.That(group.Groups[2].LastLine, Is.EqualTo(4));
            Assert.That(group.Groups[3].FirstLine, Is.EqualTo(5));
            Assert.That(group.Groups[3].LastLine, Is.EqualTo(5));
        }

        [Test]
        public void TestUpdateNewVariable()
        {
            var group = Parse("a = 3\n" +
                              "// test1\n" +
                              "// test2\n" +
                              "b = 4\n");

            Assert.That(group.Groups.Count, Is.EqualTo(3));
            Assert.That(group.Groups[0].FirstLine, Is.EqualTo(1));
            Assert.That(group.Groups[0].LastLine, Is.EqualTo(1));
            Assert.That(group.Groups[1].FirstLine, Is.EqualTo(2));
            Assert.That(group.Groups[1].LastLine, Is.EqualTo(3));
            Assert.That(group.Groups[2].FirstLine, Is.EqualTo(4));
            Assert.That(group.Groups[2].LastLine, Is.EqualTo(4));

            var updatedInput = "a = 3\n" +
                               "// test1\n" +
                               "// test2\n" +
                               "c = 5\n" +
                               "b = 4\n";
            var needsEvaluated = group.Update(Tokenizer.CreateTokenizer(updatedInput), new int[] { 4 });
            Assert.That(needsEvaluated, Is.True);

            Assert.That(group.Groups.Count, Is.EqualTo(4));
            Assert.That(group.Groups[0].FirstLine, Is.EqualTo(1));
            Assert.That(group.Groups[0].LastLine, Is.EqualTo(1));
            Assert.That(group.Groups[0].NeedsEvaluated, Is.False);
            Assert.That(group.Groups[1].FirstLine, Is.EqualTo(2));
            Assert.That(group.Groups[1].LastLine, Is.EqualTo(3));
            Assert.That(group.Groups[2].FirstLine, Is.EqualTo(4));
            Assert.That(group.Groups[2].LastLine, Is.EqualTo(4));
            Assert.That(group.Groups[2].NeedsEvaluated, Is.True);
            Assert.That(group.Groups[3].FirstLine, Is.EqualTo(5));
            Assert.That(group.Groups[3].LastLine, Is.EqualTo(5));
            Assert.That(group.Groups[3].NeedsEvaluated, Is.False);
        }

        [Test]
        public void TestUpdateDependentVariables()
        {
            var group = Parse("a = 3\n" +
                              "b = a\n" +
                              "c = b\n" +
                              "d = 4\n");

            var updatedInput = "a = 4\n" +
                               "b = a\n" +
                               "c = b\n" +
                               "d = 4\n";
            var needsEvaluated = group.Update(Tokenizer.CreateTokenizer(updatedInput), new int[] { 1 });
            Assert.That(needsEvaluated, Is.True);

            Assert.That(group.Groups.Count, Is.EqualTo(4));
            Assert.That(group.Groups[0].NeedsEvaluated, Is.True); // modified
            Assert.That(group.Groups[1].NeedsEvaluated, Is.True); // depedendent on group 0
            Assert.That(group.Groups[2].NeedsEvaluated, Is.True); // depedendent on group 1
            Assert.That(group.Groups[3].NeedsEvaluated, Is.False); // not depedendent
        }

        [Test]
        public void TestUpdateSourceLine()
        {
            var input = "a = 3\n" +
                        "achievement(\"t\", \"d\", 5, byte(0x1234) == a)\n" +
                        "leaderboard(\"t\", \"d\", byte(0x1234) == a, byte(0x1234) == a + 1, byte(0x1234) == a + 2, byte(0x2345))\n";
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var group = new ExpressionGroupCollection();
            group.Scope = RATools.Parser.AchievementScriptInterpreter.GetGlobalScope();
            group.Parse(tokenizer);

            var interpreter = new RATools.Parser.AchievementScriptInterpreter();
            interpreter.Run(group, null);

            Assert.That(group.Groups.Count, Is.EqualTo(3));
            Assert.That(group.Groups[1].GeneratedAchievements.First().SourceLine, Is.EqualTo(2));
            Assert.That(group.Groups[2].GeneratedLeaderboards.First().SourceLine, Is.EqualTo(3));

            var updatedInput = "a = 3\n" +
                               "\n" +
                               "\n" +
                               "achievement(\"t\", \"d\", 5, byte(0x1234) == a)\n" +
                               "leaderboard(\"t\", \"d\", byte(0x1234) == a, byte(0x1234) == a + 1, byte(0x1234) == a + 2, byte(0x2345))\n";
            group.Update(Tokenizer.CreateTokenizer(updatedInput), new int[] { 2, 3 });

            Assert.That(group.Groups.Count, Is.EqualTo(3));
            Assert.That(group.Groups[1].GeneratedAchievements.First().SourceLine, Is.EqualTo(4));
            Assert.That(group.Groups[2].GeneratedLeaderboards.First().SourceLine, Is.EqualTo(5));

            group.Update(Tokenizer.CreateTokenizer(input), new int[] { 2, 3 });

            Assert.That(group.Groups.Count, Is.EqualTo(3));
            Assert.That(group.Groups[1].GeneratedAchievements.First().SourceLine, Is.EqualTo(2));
            Assert.That(group.Groups[2].GeneratedLeaderboards.First().SourceLine, Is.EqualTo(3));
        }

        [Test]
        public void TestUpdateErrorLine()
        {
            var input = "a = 3\n" +
                        "achievement(\"t\", \"d\", 5, \n" +
                        "    once()\n" +
                        ")\n";
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var group = new ExpressionGroupCollection();
            group.Scope = RATools.Parser.AchievementScriptInterpreter.GetGlobalScope();
            group.Parse(tokenizer);

            var interpreter = new RATools.Parser.AchievementScriptInterpreter();
            interpreter.Run(group, null);

            Assert.That(group.HasEvaluationErrors, Is.True);
            Assert.That(group.Errors.First().Location.Start.Line, Is.EqualTo(3));

            var updatedInput = "a = 3\n" +
                               "\n" +
                               "\n" +
                               "achievement(\"t\", \"d\", 5, \n" +
                               "    once()\n" +
                               ")\n";
            group.Update(Tokenizer.CreateTokenizer(updatedInput), new int[] { 2, 3 });

            Assert.That(group.HasEvaluationErrors, Is.True);
            var error = group.Errors.First();
            Assert.That(error.Location.Start.Line, Is.EqualTo(5));
            Assert.That(error.InnermostError.Location.Start.Line, Is.EqualTo(5));

            group.Update(Tokenizer.CreateTokenizer(input), new int[] { 2, 3 });

            Assert.That(group.HasEvaluationErrors, Is.True);
            error = group.Errors.First();
            Assert.That(error.Location.Start.Line, Is.EqualTo(3));
            Assert.That(error.InnermostError.Location.Start.Line, Is.EqualTo(3));
        }
    }
}
