using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Test.Parser.Functions
{
    [TestFixture]
    class AllOfFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new AllOfFunction();
            Assert.That(def.Name.Name, Is.EqualTo("all_of"));
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

            return parser.ErrorMessage;
        }

        [Test]
        public void TestSimple()
        {
            Assert.That(Evaluate("all_of([1, 2, 3], a => byte(0x1234) != a)"),
                Is.EqualTo("byte(0x001234) != 1 && byte(0x001234) != 2 && byte(0x001234) != 3"));
        }

        [Test]
        public void TestSingleElement()
        {
            Assert.That(Evaluate("all_of([1], a => byte(0x1234) != a)"),
                Is.EqualTo("byte(0x001234) != 1"));
        }

        [Test]
        public void TestNoElements()
        {
            Assert.That(Evaluate("all_of([], a => byte(0x1234) != a)"),
                Is.EqualTo("always_true()"));
        }

        [Test]
        public void TestLogic()
        {
            // always_true() elements returned by predicate will be optimized out by the AchievementScriptInterpreter
            Assert.That(Evaluate("all_of([1, 2, 3], (a) { if (a % 2 == 0) { return byte(0x1234) != a } else { return always_true() }})"),
                Is.EqualTo("byte(0x001234) != 2"));
        }

        [Test]
        public void TestRange()
        {
            Assert.That(Evaluate("all_of(range(1,5,2), a => byte(0x1234) != a)"),
                Is.EqualTo("byte(0x001234) != 1 && byte(0x001234) != 3 && byte(0x001234) != 5"));
        }

        [Test]
        public void TestDictionary()
        {
            Assert.That(Evaluate("all_of({1:\"One\",2:\"Two\",3:\"Three\"}, a => byte(0x1234) != a)"),
                Is.EqualTo("byte(0x001234) != 1 && byte(0x001234) != 2 && byte(0x001234) != 3"));
        }
    }
}
