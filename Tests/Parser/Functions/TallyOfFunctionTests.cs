using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Functions;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class TallyOfFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new TallyOfFunction();
            Assert.That(def.Name.Name, Is.EqualTo("tally_of"));
            Assert.That(def.Parameters.Count, Is.EqualTo(3));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("inputs"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("count"));
            Assert.That(def.Parameters.ElementAt(2).Name, Is.EqualTo("predicate"));
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
            Assert.That(Evaluate("tally_of([1, 2, 3], 3, a => once(byte(a) == 9))"),
                Is.EqualTo("tally(3, once(byte(0x000001) == 9), once(byte(0x000002) == 9), once(byte(0x000003) == 9))"));
        }

        [Test]
        public void TestSingleElement()
        {
            Assert.That(Evaluate("tally_of([1], 99, a => byte(a) == 9)"),
                Is.EqualTo("repeated(99, byte(0x000001) == 9)"));
        }

        [Test]
        public void TestNoElements()
        {
            Assert.That(Evaluate("tally_of([], 4, a => byte(a) == 9) && byte(0x2345) == 6"),
                Is.EqualTo("tally requires at least one non-deducted item"));
        }

        [Test]
        public void TestLogic()
        {
            // always_false() elements returned by predicate will be optimized out by the AchievementScriptInterpreter
            Assert.That(Evaluate("tally_of([1, 2, 3], 7, (a) { if (a % 2 == 0) { return byte(a) == 9 } else { return always_false() }})"),
                Is.EqualTo("repeated(7, byte(0x000002) == 9)"));
        }

        [Test]
        public void TestRange()
        {
            Assert.That(Evaluate("tally_of(range(1,5,2), 11, a => byte(a) == 9)"),
                Is.EqualTo("tally(11, byte(0x000001) == 9, byte(0x000003) == 9, byte(0x000005) == 9)"));
        }

        [Test]
        public void TestDictionary()
        {
            Assert.That(Evaluate("tally_of({1:\"One\",2:\"Two\",3:\"Three\"}, 11, a => byte(a) == 9)"),
                Is.EqualTo("tally(11, byte(0x000001) == 9, byte(0x000002) == 9, byte(0x000003) == 9)"));
        }

        [Test]
        public void TestMissingReturn()
        {
            Assert.That(Evaluate("tally_of([1, 2, 3], 4, (a) { if (a == 2) return a })"),
                Is.EqualTo("predicate did not return a value"));
        }

        [Test]
        public void TestErrorInPredicate()
        {
            Assert.That(Evaluate("tally_of([1, 2, 3], 4, (a) { return b })"),
                Is.EqualTo("Unknown variable: b"));
        }
    }
}
