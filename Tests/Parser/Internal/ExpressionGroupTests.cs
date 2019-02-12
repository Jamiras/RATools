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
        private ExpressionGroup Parse(string input)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var parser = new AchievementScriptParser();
            return parser.Parse(tokenizer);
        }

        [Test]
        public void TestGetExpressionForLineComments()
        {
            var group = Parse("// line 1\n" +
                              "// line 2\n" +
                              "// line 3\n");

            var expressions = new List<ExpressionBase>();
            Assert.That(group.GetExpressionsForLine(expressions, 0), Is.True);
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
            Assert.That(group.GetExpressionsForLine(expressions, 4), Is.True);
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

            var b = expressions.FirstOrDefault(e => e is VariableDefinitionExpression) as VariableDefinitionExpression;
            Assert.That(b, Is.Not.Null);
            Assert.That(b.Name, Is.EqualTo("b"));

            var four = expressions.FirstOrDefault(e => e is IntegerConstantExpression) as IntegerConstantExpression;
            Assert.That(four, Is.Not.Null);
            Assert.That(four.Value, Is.EqualTo(4));

            var comment = expressions.FirstOrDefault(e => e is CommentExpression) as CommentExpression;
            Assert.That(comment, Is.Not.Null);
            Assert.That(comment.Value, Is.EqualTo("// test2"));
        }
    }
}
