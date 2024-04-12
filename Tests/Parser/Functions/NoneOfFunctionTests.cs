using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Tests.Expressions;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class NoneOfFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new NoneOfFunction();
            Assert.That(def.Name.Name, Is.EqualTo("none_of"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("inputs"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("predicate"));
        }

        private static string Evaluate(string input)
        {
            var script = "achievement(\"title\", \"desc\", 5,\n" + input + "\n)";
            var tokenizer = Tokenizer.CreateTokenizer(script);
            var parser = new AchievementScriptInterpreter();

            if (parser.Run(tokenizer))
            {
                var achievement = parser.Achievements.First();
                var builder = new AchievementBuilder(achievement);
                return builder.RequirementsDebugString;
            }

            return parser.Error.InnermostError.Message;
        }

        [Test]
        public void TestSimple()
        {
            Assert.That(Evaluate("none_of([1, 2, 3], a => byte(0x1234) == a)"),
                Is.EqualTo("byte(0x001234) != 1 && byte(0x001234) != 2 && byte(0x001234) != 3"));
        }

        [Test]
        public void TestSingleElement()
        {
            Assert.That(Evaluate("none_of([1], a => byte(0x1234) == a)"),
                Is.EqualTo("byte(0x001234) != 1"));
        }

        [Test]
        public void TestNoElements()
        {
            Assert.That(Evaluate("none_of([], a => byte(0x1234) == a)"),
                Is.EqualTo("always_true()"));
        }

        [Test]
        public void TestLogic()
        {
            // always_false() elements returned by predicate will be optimized out by the AchievementScriptInterpreter
            Assert.That(Evaluate("none_of([1, 2, 3], (a) { if (a % 2 == 0) { return byte(0x1234) == a } else { return always_false() }})"),
                Is.EqualTo("byte(0x001234) != 2"));
        }

        [Test]
        public void TestEvaluationLogic()
        {
            var expr = ExpressionTests.Parse("none_of([1, 2, 3], a => a > 4)");
            Assert.That(expr, Is.InstanceOf<BooleanConstantExpression>());
            Assert.That(((BooleanConstantExpression)expr).Value, Is.True);

            expr = ExpressionTests.Parse("none_of([1, 2, 3], a => a < 2)");
            Assert.That(expr, Is.InstanceOf<BooleanConstantExpression>());
            Assert.That(((BooleanConstantExpression)expr).Value, Is.False);
        }

        [Test]
        public void TestRange()
        {
            Assert.That(Evaluate("none_of(range(1,5,2), a => byte(0x1234) == a)"),
                Is.EqualTo("byte(0x001234) != 1 && byte(0x001234) != 3 && byte(0x001234) != 5"));
        }

        [Test]
        public void TestDictionary()
        {
            Assert.That(Evaluate("none_of({1:\"One\",2:\"Two\",3:\"Three\"}, a => byte(0x1234) == a)"),
                Is.EqualTo("byte(0x001234) != 1 && byte(0x001234) != 2 && byte(0x001234) != 3"));
        }

        [Test]
        public void TestMissingReturn()
        {
            Assert.That(Evaluate("none_of([1, 2, 3], (a) { if (a == 2) return a })"),
                Is.EqualTo("predicate did not return a value"));
        }

        [Test]
        public void TestErrorInPredicate()
        {
            Assert.That(Evaluate("none_of([1, 2, 3], (a) { return b })"),
                Is.EqualTo("Unknown variable: b"));
        }
    }
}
