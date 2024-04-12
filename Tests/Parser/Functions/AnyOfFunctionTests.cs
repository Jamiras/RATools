using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Tests.Expressions;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class AnyOfFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new AnyOfFunction();
            Assert.That(def.Name.Name, Is.EqualTo("any_of"));
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
            Assert.That(Evaluate("any_of([1, 2, 3], a => byte(0x1234) == a)"),
                Is.EqualTo("byte(0x001234) == 1 || byte(0x001234) == 2 || byte(0x001234) == 3"));
        }

        [Test]
        public void TestSingleElement()
        {
            Assert.That(Evaluate("any_of([1], a => byte(0x1234) == a)"),
                Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestNonIterable()
        {
            Assert.That(Evaluate("any_of(1, a => byte(0x1234) == a)"),
                Is.EqualTo("Cannot iterate over IntegerConstant: 1"));
        }

        [Test]
        public void TestNoElements()
        {
            Assert.That(Evaluate("any_of([], a => byte(0x1234) == a)"),
                Is.EqualTo("always_false()"));
        }

        [Test]
        public void TestLogic()
        {
            // always_false() elements returned by predicate will be optimized out by the AchievementScriptInterpreter
            Assert.That(Evaluate("any_of([1, 2, 3], (a) { if (a % 2 == 0) { return byte(0x1234) == a } else { return always_false() }})"),
                Is.EqualTo("byte(0x001234) == 2"));
        }

        [Test]
        public void TestEvaluationLogic()
        {
            var expr = ExpressionTests.Parse("any_of([1, 2, 3], a => a == 3)");
            Assert.That(expr, Is.InstanceOf<BooleanConstantExpression>());
            Assert.That(((BooleanConstantExpression)expr).Value, Is.True);

            expr = ExpressionTests.Parse("any_of([1, 2, 3], a => a == 7)");
            Assert.That(expr, Is.InstanceOf<BooleanConstantExpression>());
            Assert.That(((BooleanConstantExpression)expr).Value, Is.False);
        }

        [Test]
        public void TestRange()
        {
            Assert.That(Evaluate("any_of(range(1,5,2), a => byte(0x1234) == a)"),
                Is.EqualTo("byte(0x001234) == 1 || byte(0x001234) == 3 || byte(0x001234) == 5"));
        }

        [Test]
        public void TestDictionary()
        {
            Assert.That(Evaluate("any_of({1:\"One\",2:\"Two\",3:\"Three\"}, a => byte(0x1234) == a)"),
                Is.EqualTo("byte(0x001234) == 1 || byte(0x001234) == 2 || byte(0x001234) == 3"));
        }

        [Test]
        public void TestScopedVariable()
        {
            var script = "function ItemInInventory(id) => any_of(range(0x1200, 0x1208, step=2), addr => word(addr) == id)\n" +
                "achievement(\"title\", \"desc\", 5, ItemInInventory(17))";
            var tokenizer = Tokenizer.CreateTokenizer(script);
            var parser = new AchievementScriptInterpreter();

            if (!parser.Run(tokenizer))
                Assert.Fail(parser.ErrorMessage);

            var achievement = parser.Achievements.First();
            var builder = new AchievementBuilder(achievement);
            Assert.That(builder.RequirementsDebugString, Is.EqualTo(
                "word(0x001200) == 17 || word(0x001202) == 17 || word(0x001204) == 17 || word(0x001206) == 17 || word(0x001208) == 17"));
        }

        [Test]
        public void TestPredicateWithDefaultParameter()
        {
            var script = "function p(addr, id=17) => byte(addr) == id\n" +
                "achievement(\"title\", \"desc\", 5, any_of([1, 2, 3], p))";
            var tokenizer = Tokenizer.CreateTokenizer(script);
            var parser = new AchievementScriptInterpreter();

            if (!parser.Run(tokenizer))
                Assert.Fail(parser.ErrorMessage);

            var achievement = parser.Achievements.First();
            var builder = new AchievementBuilder(achievement);
            Assert.That(builder.RequirementsDebugString, Is.EqualTo(
                "byte(0x000001) == 17 || byte(0x000002) == 17 || byte(0x000003) == 17"));
        }

        [Test]
        public void TestPredicateWithNoParameters()
        {
            Assert.That(Evaluate("any_of([1], () => byte(0x1234) == 1)"),
                Is.EqualTo("predicate function must accept a single parameter"));
        }

        [Test]
        public void TestPredicateWithExtraParameters()
        {
            Assert.That(Evaluate("any_of([1], (a, b) => byte(0x1234) == a)"),
                Is.EqualTo("predicate function must accept a single parameter"));
        }

        [Test]
        public void TestMissingReturn()
        {
            Assert.That(Evaluate("any_of([1, 2, 3], (a) { if (a == 2) return a })"),
                Is.EqualTo("predicate did not return a value"));
        }

        [Test]
        public void TestErrorInPredicate()
        {
            Assert.That(Evaluate("any_of([1, 2, 3], (a) { return b })"),
                Is.EqualTo("Unknown variable: b"));
        }
    }
}
