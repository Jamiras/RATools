﻿using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Functions;
using RATools.Parser.Tests.Expressions;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class SumOfFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new SumOfFunction();
            Assert.That(def.Name.Name, Is.EqualTo("sum_of"));
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

            if (parser.Error.InnerError == null)
                return parser.Error.Message;

            return parser.Error.InnermostError.Message;
        }

        [Test]
        public void TestSimple()
        {
            Assert.That(Evaluate("sum_of([1, 2, 3], a => byte(a)) == 9"),
                Is.EqualTo("(byte(0x000001) + byte(0x000002) + byte(0x000003)) == 9"));
        }

        [Test]
        public void TestSingleElement()
        {
            Assert.That(Evaluate("sum_of([1], a => byte(a)) == 9"),
                Is.EqualTo("byte(0x000001) == 9"));
        }

        [Test]
        public void TestNoElements()
        {
            Assert.That(Evaluate("sum_of([], a => byte(a)) == byte(9)"),
                Is.EqualTo("byte(0x000009) == 0"));
        }

        [Test]
        public void TestLogic()
        {
            // always_false() elements returned by predicate will be optimized out by the AchievementScriptInterpreter
            Assert.That(Evaluate("sum_of([1, 2, 3], (a) { if (a % 2 == 0) { return byte(a) } else { return 0 }}) == 9"),
                Is.EqualTo("byte(0x000002) == 9"));
        }

        [Test]
        public void TestRange()
        {
            Assert.That(Evaluate("sum_of(range(1,5,2), a => byte(a)) == 9"),
                Is.EqualTo("(byte(0x000001) + byte(0x000003) + byte(0x000005)) == 9"));
        }

        [Test]
        public void TestDictionary()
        {
            Assert.That(Evaluate("sum_of({1:\"One\",2:\"Two\",3:\"Three\"}, a => byte(a)) == 9"),
                Is.EqualTo("(byte(0x000001) + byte(0x000002) + byte(0x000003)) == 9"));
        }

        [Test]
        public void TestMissingReturn()
        {
            Assert.That(Evaluate("sum_of([1, 2, 3], (a) { if (a == 2) return a })"),
                Is.EqualTo("predicate did not return a value"));
        }

        [Test]
        public void TestErrorInPredicate()
        {
            Assert.That(Evaluate("sum_of([1, 2, 3], (a) { return b })"),
                Is.EqualTo("Unknown variable: b"));
        }

        [Test]
        public void TestSumOfUnrepresentableLogic()
        {
            Assert.That(Evaluate("sum_of([1, 2, 3], (a) => bit1(0x1234+a) * word(0x2345+a*2) / 5)"),
                Is.EqualTo("Cannot convert mathematic expression to requirement"));
        }
    }
}
