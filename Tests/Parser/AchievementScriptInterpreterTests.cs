﻿using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using System.Linq;

namespace RATools.Test.Parser
{
    [TestFixture]
    class AchievementScriptInterpreterTests
    {
        private AchievementScriptInterpreter Parse(string input, bool expectedSuccess = true)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var parser = new AchievementScriptInterpreter();

            if (expectedSuccess)
            {
                if (!parser.Run(tokenizer))
                {
                    Assert.That(parser.ErrorMessage, Is.Null);
                    Assert.Fail("AchievementScriptInterpreter.Run failed with no error message");
                }
            }
            else
            {
                Assert.That(parser.Run(tokenizer), Is.False);
                Assert.That(parser.ErrorMessage, Is.Not.Null);
            }

            return parser;
        }

        private static string GetInnerErrorMessage(AchievementScriptInterpreter parser)
        {
            if (parser.Error == null)
                return null;

            var err = parser.Error;
            while (err.InnerError != null)
                err = err.InnerError;

            return string.Format("{0}:{1} {2}", err.Location.Start.Line, err.Location.Start.Column, err.Message);
        }

        private static string GetRequirements(Achievement achievement)
        {
            var builder = new AchievementBuilder(achievement);
            return builder.RequirementsDebugString;
        }

        [Test]
        public void TestTitleAndGameId()
        {
            var parser = Parse("// Title\n// #ID=1234");
            Assert.That(parser.GameTitle, Is.EqualTo("Title"));
            Assert.That(parser.GameId, Is.EqualTo(1234));
        }

        [Test]
        public void TestAchievementFunction()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, byte(0x1234) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestVariables()
        {
            var parser = Parse("title = \"T\"\n" +
                               "desc = \"D\"\n" +
                               "points = 5\n" +
                               "trigger = byte(0x1234) == 1\n" +
                               "achievement(title, desc, points, trigger)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestNamedParameters()
        {
            var parser = Parse("title = \"T\"\n" +
                               "desc = \"D\"\n" +
                               "points = 5\n" +
                               "trigger = byte(0x1234) == 1\n" +
                               "achievement(points = points, trigger = trigger, title = title, description = desc)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestParameterizedFunction()
        {
            var parser = Parse("function trigger(i) => byte(0x1233 + i) == i\n" +
                               "achievement(\"T\", \"D\", 5, trigger(1))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestAchievementNoTrigger()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5)", false);
            Assert.That(parser.ErrorMessage, Is.EqualTo("1:1 Required parameter 'trigger' not provided"));
        }

        [Test]
        public void TestEmptyTrigger()
        {
            // tally() of empty array will result in to conditions for the trigger
            var parser = Parse("achievement(\"T\", \"D\", 5, tally(4, []))", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("1:26 Incomplete trigger condition"));
        }

        [Test]
        [TestCase("byte(0x1234) - 1 == 4", "byte(0x001234) == 5")]
        [TestCase("byte(0x1234) + 1 == 4", "byte(0x001234) == 3")]
        [TestCase("byte(0x1234) * 2 == 4", "byte(0x001234) == 2")]
        [TestCase("byte(0x1234) / 2 == 4", "byte(0x001234) == 8")]
        [TestCase("byte(0x1234) * 2 + 1 == byte(0x4321) * 2 + 1", "byte(0x001234) == byte(0x004321)")]
        [TestCase("byte(0x1234) + 2 - 1 == byte(0x4321) + 1", "byte(0x001234) == byte(0x004321)")]
        [TestCase("byte(0x1234) + 3 == prev(byte(0x1234))", "(3 + byte(0x001234)) == prev(byte(0x001234))")] // value decreases by 3
        [TestCase("byte(0x1234) == prev(byte(0x1234)) - 3", "(3 + byte(0x001234)) == prev(byte(0x001234))")] // value decreases by 3
        [TestCase("prev(byte(0x1234)) - byte(0x1234) == 3", "(prev(byte(0x001234)) - byte(0x001234)) == 3")] // value decreases by 3
        [TestCase("byte(0x1234) - 3 == prev(byte(0x1234))", "(byte(0x001234) - 3) == prev(byte(0x001234))")] // value increases by 3
        [TestCase("byte(0x1234) == prev(byte(0x1234)) + 3", "(byte(0x001234) - 3) == prev(byte(0x001234))")] // value increases by 3
        [TestCase("byte(0x1234) - prev(byte(0x1234)) == 3", "(byte(0x001234) - prev(byte(0x001234))) == 3")] // value increases by 3
        [TestCase("byte(0x1234) + 1 == byte(0x4321) - 1", "(2 + byte(0x001234)) == byte(0x004321)")] // modifiers on different addresses
        [TestCase("(word(0x1234) - 1) * 4 > (prev(word(0x1234)) - 1) * 4", "word(0x001234) > prev(word(0x001234))")]
        [TestCase("(word(0x1234) - 1) / 4 > (prev(word(0x1234)) - 1) / 4", "word(0x001234) > prev(word(0x001234))")]
        [TestCase("bit1(0x1234) + bit2(0x1234) > bit3(0x1234) + bit4(0x1234)", "(4 + bit1(0x001234) + bit2(0x001234) - bit4(0x001234) - bit3(0x001234)) > 4")]
        [TestCase("bit1(0x1234) + bit2(0x1234) > bit3(0x1234) - bit4(0x1234)", "(bit1(0x001234) + bit2(0x001234) + bit4(0x001234)) > bit3(0x001234)")]
        [TestCase("bit1(0x1234) + bit2(0x1234) > bit3(0x1234) + bit4(0x1234) + 1", "(4 + bit1(0x001234) + bit2(0x001234) - bit4(0x001234) - bit3(0x001234)) > 5")]
        [TestCase("bit1(0x1234) + bit2(0x1234) + 3 > bit3(0x1234) + bit4(0x1234) + 5", "(4 + bit1(0x001234) + bit2(0x001234) - bit4(0x001234) - bit3(0x001234)) > 6")]
        [TestCase("bit1(0x1234) + bit2(0x1234) < bit3(0x1234) + bit4(0x1234) + 1", "(4 + bit1(0x001234) + bit2(0x001234) - bit4(0x001234) - bit3(0x001234)) < 5")]
        [TestCase("bit1(0x1234) + bit2(0x1234) - bit3(0x1234) - bit4(0x1234) < 1", "(2 + bit1(0x001234) + bit2(0x001234) - bit4(0x001234) - bit3(0x001234)) < 3")]
        [TestCase("bit1(0x1234) + bit2(0x1234) + 2 - bit3(0x1234) - bit4(0x1234) < 3", "(2 + bit1(0x001234) + bit2(0x001234) - bit4(0x001234) - bit3(0x001234)) < 3")]
        [TestCase("byte(0x1234) + 1 - byte(0x2345) >= 2", "(255 + byte(0x001234) - byte(0x002345)) >= 256")] // 254 added to both sides to prevent underflow
        [TestCase("byte(0x1234) + 1 - byte(0x2345) < 2", "(1 + byte(0x001234) - byte(0x002345)) < 2")]
        public void TestTransitiveCondition(string trigger, string expectedRequirement)
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, " + trigger + ")");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo(expectedRequirement));
        }

        [Test]
        public void TestDictionaryLookup()
        {
            var parser = Parse("dict = { 1: \"T\", 2: \"D\" }\n" +
                               "achievement(dict[1], dict[2], 5, byte(0x1234) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestArrayLookup()
        {
            var parser = Parse("array = [ \"A\", \"B\", \"C\" ]\n" +
                               "achievement(array[1], array[2], 5, byte(0x1234) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("B"));
            Assert.That(achievement.Description, Is.EqualTo("C"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestIf()
        {
            var parser = Parse("n = 1\n" +
                               "t = \"S\"\n" +
                               "if (n == 1) t = \"T\"\n" +
                               "achievement(t, \"D\", 5, byte(0x1234) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestElse()
        {
            var parser = Parse("n = 1\n" +
                               "if (n == 0) t = \"S\" else t = \"T\"\n" +
                               "achievement(t, \"D\", 5, byte(0x1234) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestElseBraces()
        {
            var parser = Parse("n = 1\n" +
                               "if (n == 0) { t = \"S\" } else { t = \"T\" }\n" +
                               "achievement(t, \"D\", 5, byte(0x1234) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestForDict()
        {
            var parser = Parse("dict = { 1: \"T\", 2: \"T2\" }\n" +
                               "for k in dict {\n" +
                               "    achievement(dict[k], \"D\", 5, byte(0x1234) == 1)\n" +
                               "}");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(2));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));

            achievement = parser.Achievements.Last();
            Assert.That(achievement.Title, Is.EqualTo("T2"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestForArray()
        {
            var parser = Parse("array = [ \"T\", \"T2\" ]\n" +
                               "for k in array {\n" +
                               "    achievement(k, \"D\", 5, byte(0x1234) == 1)\n" +
                               "}");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(2));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));

            achievement = parser.Achievements.Last();
            Assert.That(achievement.Title, Is.EqualTo("T2"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestForRange()
        {
            var parser = Parse("for k in range(1, 2) {\n" +
                               "    achievement(\"T\", \"D\", 5, byte(0x1234) == k)\n" +
                               "}");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(2));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));

            achievement = parser.Achievements.Last();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 2"));
        }

        [Test]
        public void TestForRangeStep()
        {
            var parser = Parse("for k in range(1, 5, 3) {\n" +
                               "    achievement(\"T\", \"D\", 5, byte(0x1234) == k)\n" +
                               "}");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(2));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));

            achievement = parser.Achievements.Last();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 4"));
        }

        [Test]
        public void TestForRangeReverse()
        {
            var parser = Parse("for k in range(2, 1, -1) {\n" +
                               "    achievement(\"T\", \"D\", 5, byte(0x1234) == k)\n" +
                               "}");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(2));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 2"));

            achievement = parser.Achievements.Last();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestForRangeReverseNoStep()
        {
            var parser = Parse("for k in range(2, 1) {\n" +
                               "    achievement(\"T\", \"D\", 5, byte(0x1234) == k)\n" +
                               "}", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("1:19 step must be negative if start is after stop"));
        }

        [Test]
        public void TestForRangeZeroStep()
        {
            var parser = Parse("for k in range(1, 2, 0) {\n" +
                               "    achievement(\"T\", \"D\", 5, byte(0x1234) == k)\n" +
                               "}", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("1:22 step must not be 0"));
        }

        [Test]
        public void TestReturnFromFunction()
        {
            var parser = Parse("function f(i) {\n" +
                               "   if (i == 1)\n" +
                               "       return byte(0x1234) == 1\n" +
                               "   return byte(0x4567) == 1\n" +
                               "}\n" +
                               "achievement(\"T\", \"D\", 5, f(1))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestReturnFromLoopInFunction()
        {
            var parser = Parse("dict = { 1: \"T\", 2: \"T2\" }\n" +
                               "function f(i) {\n" +
                               "   for k in dict {\n" +
                               "       if (i == k)\n" +
                               "           return byte(0x1234) == 1\n" +
                               "   }\n" +
                               "   return byte(0x4567) == 1\n" +
                               "}\n" +
                               "achievement(\"T\", \"D\", 5, f(1))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestPrev()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, prev(byte(0x1234)) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("prev(byte(0x001234)) == 1"));
        }

        [Test]
        public void TestPrevMalformed()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, prev(byte(0x1234) == 1))", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("1:31 accessor did not evaluate to a memory accessor"));
        }

        [Test]
        public void TestPrevMathematic()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, prev(byte(0x1234) + 6) == 10)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("prev(byte(0x001234)) == 4"));

            parser = Parse("achievement(\"T\", \"D\", 5, prev(byte(0x1234) * 10 + 20) == 80)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("prev(byte(0x001234)) == 6"));

            parser = Parse("achievement(\"T\", \"D\", 5, prev(byte(0x1234) + byte(0x2345)) == 7)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("(prev(byte(0x001234)) + prev(byte(0x002345))) == 7"));
        }

        [Test]
        public void TestOnce()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, once(byte(0x1234) == 1))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("once(byte(0x001234) == 1)"));
        }

        [Test]
        public void TestOnceMalformed()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, once(byte(0x1234)) == 1)", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("1:31 Incomplete trigger condition"));
        }

        [Test]
        public void TestRepeated()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, repeated(4, byte(0x1234) == 1))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("repeated(4, byte(0x001234) == 1)"));
        }

        [Test]
        public void TestNever()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, once(byte(0x4567) == 1) && never(byte(0x1234) == 1))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("once(byte(0x004567) == 1) && never(byte(0x001234) == 1)"));
        }

        [Test]
        public void TestNeverWithOrs()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, once(byte(0x2345) == 0) && never(byte(0x1234) == 0 || byte(0x1234) == 2 || byte(0x1234) == 5))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("once(byte(0x002345) == 0) && never(byte(0x001234) == 0) && never(byte(0x001234) == 2) && never(byte(0x001234) == 5)"));
        }

        [Test]
        public void TestUnless()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, once(byte(0x4567) == 1) && unless(byte(0x1234) == 1))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("once(byte(0x004567) == 1) && unless(byte(0x001234) == 1)"));
        }

        [Test]
        public void TestUnlessWithOrs()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, once(byte(0x2345) == 0) && unless(byte(0x1234) == 0 || byte(0x1234) == 2 || byte(0x1234) == 5))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("once(byte(0x002345) == 0) && unless(byte(0x001234) == 0) && unless(byte(0x001234) == 2) && unless(byte(0x001234) == 5)"));
        }

        [Test]
        public void TestVariableScopeGlobal()
        {
            var parser = Parse("p = 5\n" +
                               "function test() { p = 6 }\n" +
                               "test()\n" +
                               "achievement(\"T\", \"D\", p, prev(byte(0x1234)) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(achievement.Points, Is.EqualTo(6));
        }

        [Test]
        public void TestVariableScopeParameter()
        {
            var parser = Parse("p = 5\n" +
                               "function test(p) { p = 6 }\n" +
                               "test(p)\n" +
                               "achievement(\"T\", \"D\", p, prev(byte(0x1234)) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(achievement.Points, Is.EqualTo(5));
        }

        [Test]
        public void TestVariableScopeLocal()
        {
            var parser = Parse("function test() { p = 6 }\n" +
                               "test()\n" +
                               "achievement(\"T\", \"D\", p, prev(byte(0x1234)) == 1)", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("3:23 Unknown variable: p"));
        }

        [Test]
        public void TestVariableScopeNested()
        {
            var parser = Parse(
                "function foo2(a)\n" +                            // a = 1
                "{\n" +
                "    a = a + 1\n" +                               // a = 2
                "    b = a + 1\n" +                               // b = 3 (should not update b in foo)
                "    return byte(0x0000) == b\n" +                // byte(0x0000) == 3
                "}\n" +
                "function foo(a)\n" +                             // a = 1
                "{\n" +
                "    b = a + 1\n" +                               // b = 2
                "    return foo2(a) && byte(a) == b\n" +          // foo2(1) && byte(0x0001) == 2 (a and b should not have been updated)
                "}\n" +
                "achievement(\"Test\", \"Description\", 5, foo(1))\n");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x000000) == 3 && byte(0x000001) == 2"));
        }

        [Test]
        public void TestVariableScopeGlobalLocation()
        {
            var parser = Parse(
                "c = 1\n" +
                "function foo(a)\n" +
                "{\n" +
                "    b = a + 1\n" +                     // global variable declared after function definition
                "    c = a + 1\n" +                     // global variable declared before function definition
                "    return byte(0x0000) == b\n" +
                "}\n" +
                "\n" +
                "b = 1\n" +
                "achievement(\"Test\", \"Description\", 5, foo(1) && byte(b) == c)\n");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x000000) == 2 && byte(0x000002) == 2"));

            parser = Parse(
                "function foo(a)\n" +
                "{\n" +
                "    b = a + 1\n" +                     // global variable declared after function called - becomes local
                "    return byte(0x0000) == b\n" +
                "}\n" +
                "\n" +
                "achievement(\"Test\", \"Description\", 5, foo(1) && byte(0x0001) == b)\n" + // global b not defined yet, should error
                "b = 1\n", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("7:65 Unknown variable: b"));
        }

        [Test]
        public void TestAddSource()
        {
            var parser = Parse("function f() => byte(0x1234) + byte(0x1235)" +
                               "achievement(\"T\", \"D\", 5, f() == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("(byte(0x001234) + byte(0x001235)) == 1"));
        }

        [Test]
        public void TestAddSourceMultiple()
        {
            var parser = Parse("function f() => byte(0x1234) + byte(0x1235) + byte(0x1236) + byte(0x1237)" +
                               "achievement(\"T\", \"D\", 5, f() == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("(byte(0x001234) + byte(0x001235) + byte(0x001236) + byte(0x001237)) == 1"));
        }

        [Test]
        public void TestSubSource()
        {
            var parser = Parse("function f() => byte(0x1234) - byte(0x1235)" +
                               "achievement(\"T\", \"D\", 5, f() == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("(byte(0x001234) - byte(0x001235)) == 1"));
        }

        [Test]
        [TestCase("byte(0x1234) + 1 - byte(0x1235) == 3", "(byte(0x001234) - byte(0x001235)) == 2")] // no underflow on direct comparison
        [TestCase("byte(0x1234) + 1 - byte(0x1235) != 3", "(byte(0x001234) - byte(0x001235)) != 2")]
        [TestCase("byte(0x1234) + 1 - byte(0x1235) >= 3", "(255 + byte(0x001234) - byte(0x001235)) >= 257")] // potential underflow of 255, move +1 to other side and adjust both
        [TestCase("byte(0x1234) + 1 - byte(0x1235) >  3", "(255 + byte(0x001234) - byte(0x001235)) > 257")]
        [TestCase("byte(0x1234) + 1 - byte(0x1235) <= 3", "(1 + byte(0x001234) - byte(0x001235)) <= 3")] // explicit offset supercedes underflow for less than comparisons
        [TestCase("byte(0x1234) + 1 - byte(0x1235) <  3", "(1 + byte(0x001234) - byte(0x001235)) < 3")]
        [TestCase("byte(0x1234) - byte(0x1235) <= 3", "(255 + byte(0x001234) - byte(0x001235)) <= 258")] // potential underflow of 255, no offset
        [TestCase("byte(0x1234) - byte(0x1235) <  3", "(255 + byte(0x001234) - byte(0x001235)) < 258")]
        [TestCase("5 - byte(0x1234) < 2", "(5 - byte(0x001234)) < 2")] // only 4 and 5 are valid values - don't modify the expression
        [TestCase("5 - byte(0x1234) == 2", "byte(0x001234) == 3")] // only 3 is a valid value, automatically normalize the expression
        [TestCase("300 - byte(0x1234) < 100", "byte(0x001234) > 200")] // no underflow, expression can be inverted
        [TestCase("byte(0x1234) - byte(0x2345) - byte(0x3456) < 100", "(510 + byte(0x001234) - byte(0x003456) - byte(0x002345)) < 610")] // double underflow - add 255*2 to both sides
        [TestCase("700 + byte(0x1234) - byte(0x2345) - byte(0x3456) < 100", "(byte(0x002345) + byte(0x003456) - byte(0x001234)) > 600")] // suffient modifier to prevent underflow, just rearrange the operations
        [TestCase("byte(0x1234) - byte(0x2345) - byte(0x3456) < -600", "(byte(0x002345) + byte(0x003456) - byte(0x001234)) > 600")] // comparison to negative, underflow 
        public void TestUnderflowAdjustment(string input, string expected)
        {
            // SubSource(mem) can cause wraparound, so if modifiers are present when doing a
            // less than comparison, assume they're there to prevent the wraparound and don't
            // transfer them to the right side.
            var parser = Parse("achievement(\"T\", \"D\", 5, " + input + ")");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo(expected));
        }

        [Test]
        public void TestUnderflowAdjustmentImpossible()
        {
            var input = "5 + byte(0x1234) == 2";

            var parser = Parse("achievement(\"T\", \"D\", 5, " + input + ")", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("1:26 Expression can never be true"));
        }

        [Test]
        public void TestMeasuredMultipleValue()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, measured(byte(0x1234) == 10) || measured(byte(0x2345) == 10))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("measured(byte(0x001234) == 10) || measured(byte(0x002345) == 10)"));
        }

        [Test]
        public void TestMeasuredMultipleHits()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, measured(repeated(6, byte(0x1234) == 10)) || measured(repeated(6, byte(0x2345) == 4)))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("measured(repeated(6, byte(0x001234) == 10)) || measured(repeated(6, byte(0x002345) == 4))"));
        }

        [Test]
        public void TestMeasuredMultipleDiffering()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, measured(byte(0x1234) == 10) && measured(byte(0x2345) == 1))", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("1:26 Multiple measured() conditions must have the same target."));
        }

        [Test]
        public void TestMeasuredMultipleHitsWhen()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, measured(repeated(6, byte(0x1234) == 10), when=byte(0x2345)==7) || measured(repeated(6, byte(0x2345) == 4), when=byte(0x2346)==7))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("(measured(repeated(6, byte(0x001234) == 10), when=byte(0x002345) == 7)) || (measured(repeated(6, byte(0x002345) == 4), when=byte(0x002346) == 7))"));
        }

        [Test]
        public void TestTransitiveOrClause()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, (byte(0x1234) == 1 || byte(0x2345) == 2) && byte(0x3456) == 3)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x003456) == 3 && (byte(0x001234) == 1 || byte(0x002345) == 2)"));
        }

        [Test]
        public void TestTransitiveMath()
        {
            var parser = Parse("function f(n) => byte(n) + 1\n" +
                               "achievement(\"T\", \"D\", 5, f(0x1234) - f(0x2345) == 3)\n");

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("(byte(0x001234) - byte(0x002345)) == 3"));
        }

        [Test]
        public void TestRichPresenceDisplay()
        {
            var parser = Parse("rich_presence_display(\"simple string\")");
            Assert.That(parser.RichPresence, Is.EqualTo("Display:\r\nsimple string\r\n"));
        }

        [Test]
        public void TestRichPresenceValue()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234)))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH001234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValuePlusOne()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234) + 1))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH001234_v1) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueMinusOne()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234) - 1))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH001234_v-1) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueMultiply()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234) * 10 + 1))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH001234*10_v1) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueDivide()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234) / 4))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH001234*0.25) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueFunction()
        {
            var parser = Parse("function test() => byte(0x1234)\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", test()))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH001234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueReused()
        {
            var parser = Parse("rich_presence_conditional_display(byte(0) == 0, \"value {0} there\", rich_presence_value(\"Test\", byte(0x2345)))\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234)))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\n?0xH000000=0?value @Test(0xH002345) there\r\nvalue @Test(0xH001234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueReusedDifferingFormat()
        {
            var parser = Parse("rich_presence_conditional_display(byte(0) == 0, \"value {0} there\", rich_presence_value(\"Test\", byte(0x2345), format=\"VALUE\"))\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234), format=\"FRAMES\"))", false);
            Assert.That(parser.ErrorMessage, Is.EqualTo("1:68 Multiple rich_presence_value calls with the same name must have the same format"));
        }

        [Test]
        public void TestRichPresenceLookup()
        {
            var parser = Parse("dict = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234), dict))");
            Assert.That(parser.RichPresence, Is.EqualTo("Lookup:Test\r\n1=Yes\r\n2=No\r\n\r\nDisplay:\r\nvalue @Test(0xH001234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceLookupNoDict()
        {
            var parser = Parse("dict = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234)))", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("2:41 Required parameter 'dictionary' not provided"));
        }

        [Test]
        public void TestRichPresenceLookupReused()
        {
            // multiple display strings can use the same lookup only if they use the same dictionary and fallback
            var parser = Parse("dict = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_conditional_display(byte(0) == 0, \"value {0} there\", rich_presence_lookup(\"Test\", byte(0x2345), dict))\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234), dict))");
            Assert.That(parser.RichPresence, Is.EqualTo("Lookup:Test\r\n1=Yes\r\n2=No\r\n\r\nDisplay:\r\n?0xH000000=0?value @Test(0xH002345) there\r\nvalue @Test(0xH001234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceLookupReusedDifferingFallback()
        {
            // multiple display strings can use the same lookup only if they use the same dictionary and fallback
            var parser = Parse("dict = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_conditional_display(byte(0) == 0, \"value {0} there\", rich_presence_lookup(\"Test\", byte(0x2345), dict, fallback=\"x\"))\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234), dict, fallback=\"y\"))", false);
            Assert.That(parser.ErrorMessage, Is.EqualTo("3:99 Multiple rich_presence_lookup calls with the same name must have the same fallback"));
        }

        [Test]
        public void TestRichPresenceLookupReusedDifferingDictionary()
        {
            // multiple display strings can use the same lookup only if they use the same dictionary and fallback
            var parser = Parse("dict1 = { 1:\"Yes\", 2:\"No\" }\n" +
                               "dict2 = { 1:\"Yes\", 2:\"No\", 3:\"Maybe\" }\n" +
                               "rich_presence_conditional_display(byte(0) == 0, \"value {0} there\", rich_presence_lookup(\"Test\", byte(0x2345), dict1))\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234), dict2))", false);
            Assert.That(parser.ErrorMessage, Is.EqualTo("4:41 Multiple rich_presence_lookup calls with the same name must have the same dictionary"));
        }

        [Test]
        public void TestRichPresenceLookupReusedEquivalentDictionary()
        {
            // multiple display strings can use the same lookup only if they use the same dictionary and fallback
            var parser = Parse("dict1 = { 1:\"Yes\", 2:\"No\" }\n" +
                               "dict2 = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_conditional_display(byte(0) == 0, \"value {0} there\", rich_presence_lookup(\"Test\", byte(0x2345), dict1))\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234), dict2))");
            Assert.That(parser.RichPresence, Is.EqualTo("Lookup:Test\r\n1=Yes\r\n2=No\r\n\r\nDisplay:\r\n?0xH000000=0?value @Test(0xH002345) there\r\nvalue @Test(0xH001234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceLookupMultipleDictionaries()
        {
            // multiple display strings can use the same lookup only if they use the same dictionary and fallback
            var parser = Parse("dict1 = { 1:\"Yes\", 2:\"No\" }\n" +
                               "dict2 = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_conditional_display(byte(0) == 0, \"value {0} there\", rich_presence_lookup(\"Test1\", byte(0x2345), dict1))\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test2\", byte(0x1234), dict2))");
            Assert.That(parser.RichPresence, Is.EqualTo("Lookup:Test1\r\n1=Yes\r\n2=No\r\n\r\nLookup:Test2\r\n1=Yes\r\n2=No\r\n\r\nDisplay:\r\n?0xH000000=0?value @Test1(0xH002345) there\r\nvalue @Test2(0xH001234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceLookupPlusOne()
        {
            var parser = Parse("dict = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234) + 1, dict))");
            Assert.That(parser.RichPresence, Is.EqualTo("Lookup:Test\r\n1=Yes\r\n2=No\r\n\r\nDisplay:\r\nvalue @Test(0xH001234_v1) here\r\n"));
        }

        [Test]
        public void TestRichPresenceInvalidIndex()
        {
            var parser = Parse("rich_presence_display(\"value {1} here\", rich_presence_value(\"Test\", byte(0x1234)))", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("1:30 Invalid parameter index: 1"));
        }

        [Test]
        public void TestRichPresenceLookupFallback()
        {
            var parser = Parse("dict = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234), dict, \"Maybe\"))");
            Assert.That(parser.RichPresence, Is.EqualTo("Lookup:Test\r\n1=Yes\r\n2=No\r\n*=Maybe\r\n\r\nDisplay:\r\nvalue @Test(0xH001234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceLookupInvalidFallback()
        {
            var parser = Parse("dict = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234), dict, 1))", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("2:90 Fallback value is not a string"));
        }

        [Test]
        public void TestLeaderboard()
        {
            var parser = Parse("leaderboard(\"T\", \"D\", byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, byte(0x4567))");
            Assert.That(parser.Leaderboards.Count(), Is.EqualTo(1));
            var leaderboard = parser.Leaderboards.First();
            Assert.That(leaderboard.Title, Is.EqualTo("T"));
            Assert.That(leaderboard.Description, Is.EqualTo("D"));
            Assert.That(leaderboard.Start, Is.EqualTo("0xH001234=1"));
            Assert.That(leaderboard.Cancel, Is.EqualTo("0xH001234=2"));
            Assert.That(leaderboard.Submit, Is.EqualTo("0xH001234=3"));
            Assert.That(leaderboard.Value, Is.EqualTo("0xH004567"));
        }

        [Test]
        public void TestFunctionCallInFunctionInExpression()
        {
            var parser = Parse("function foo() {\n" +
                               "    trigger = always_false() always_true()\n" + // always_true() should be flagged as an error
                               "\n" +
                               "    for offset in [0, 1] {\n" +
                               "        trigger = trigger || byte(offset) == 10\n" +
                               "    }\n" +
                               "\n" +
                               "    return trigger\n" +
                               "}\n" +
                               "achievement(\"Title\", \"Description\", 5, foo())\n", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("2:30 always_true has no meaning outside of a trigger clause"));
        }

        [Test]
        public void TestErrorInFunctionInExpression()
        {
            var parser = Parse("function foo() => byte(1)\n" +
                               "achievement(\"Title\", \"Description\", 5, once(foo()))\n", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("2:45 Incomplete trigger condition"));
        }

        [Test]
        public void TestErrorInFunctionParameterLocation()
        {
            var parser = Parse("tens = word(0x1234) == 3\n" +
                               "achievement(\"Title\", \"Description\", 5, prev(tens) == 100)\n", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("2:45 accessor did not evaluate to a memory accessor"));
        }

        [TestCase("word(0x1234) * 10 == 10000", true, "word(0x001234) == 1000")]
        [TestCase("word(0x1234) * 10 == 9999", false, "1:26 Result can never be true using integer math")]
        [TestCase("word(0x1234) * 10 != 9999", false, "1:26 Result is always true using integer math")]
        [TestCase("word(0x1234) * 10 == 9990", true, "word(0x001234) == 999")]
        [TestCase("word(0x1234) * 10 >= 9999", true, "word(0x001234) > 999")]
        [TestCase("word(0x1234) * 10 <= 9999", true, "word(0x001234) <= 999")]
        [TestCase("word(0x1234) * 10 * 2 == 10000", true, "word(0x001234) == 500")]
        [TestCase("2 * word(0x1234) * 10 == 10000", true, "word(0x001234) == 500")]
        [TestCase("word(0x1234) * 10 / 2 == 10000", true, "word(0x001234) == 2000")]
        [TestCase("word(0x1234) * 10 + 10 == 10000", true, "word(0x001234) == 999")]
        [TestCase("word(0x1234) * 10 - 10 == 10000", true, "word(0x001234) == 1001")]
        [TestCase("word(0x1234) * 10 + byte(0x1235) == 10000", false, "1:26 Cannot normalize expression to eliminate multiplication")]
        [TestCase("byte(0x1235) + word(0x1234) * 10 == 10000", false, "1:26 Cannot normalize expression to eliminate multiplication")]
        [TestCase("word(0x1234) * 10 + byte(0x1235) * 2 == 10000", false, "1:26 Cannot normalize expression to eliminate multiplication")]
        [TestCase("(word(0x1234) - 1) * 4 < 100", true, "word(0x001234) < 26")]
        [TestCase("(word(0x1234) - 1) / 4 < 100", true, "word(0x001234) < 401")]
        [TestCase("(word(0x1234) - 1) * 4 < 99", true, "word(0x001234) < 25")]
        public void TestMultiplicationInExpression(string input, bool expectedResult, string output)
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, " + input + ")\n", expectedResult);

            if (expectedResult)
            {
                var achievement = parser.Achievements.First();
                Assert.That(GetRequirements(achievement), Is.EqualTo(output));
            }
            else
            {
                Assert.That(GetInnerErrorMessage(parser), Is.EqualTo(output));
            }
        }

        [Test]
        public void TestUnknownVariableInIfInFunction()
        {
            var parser = Parse("function foo(param) {\n" +
                               "    if param == 1\n" +
                               "        return AREA\n" +
                               "\n" +
                               "    return byte(0x1234) == 0\n" +
                               "}\n" +
                               "achievement(\"Title\", \"Description\", 5, foo(1))\n", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("3:16 Unknown variable: AREA"));
        }

        [Test]
        public void TestFunctionWithCommonConditionPromotedToCore()
        {
            var parser = Parse("function test(x) => byte(0x1234) == 1 && byte(0x2345) == x\n" +
                               "achievement(\"Title\", \"Description\", 5, test(3) || test(4))\n");

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1 && (byte(0x002345) == 3 || byte(0x002345) == 4)"));
        }

        [Test]
        public void TestNeverRepeatedNestedAlwaysFalse()
        {
            var parser = Parse("function trigger(i) => always_false() || byte(0x1233 + i) == i\n" +
                               "achievement(\"T\", \"D\", 5, never(repeated(2, trigger(1) || trigger(2))))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("never(repeated(2, byte(0x001234) == 1 || byte(0x001235) == 2))"));
        }

        [Test]
        public void TestNeverRepeatedAlwaysFalse()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, never(repeated(2, always_false() || byte(0x1233 + 1) == 1 || always_false() || byte(0x1233 + 2) == 2)))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("never(repeated(2, byte(0x001234) == 1 || byte(0x001235) == 2))"));
        }

        [Test]
        public void TestNeverRepeatedNestedAlwaysTrue()
        {
            var parser = Parse("function trigger(i) => always_true() && byte(0x1233 + i) == i\n" +
                               "achievement(\"T\", \"D\", 5, never(repeated(2, trigger(1) && trigger(2))))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("never(repeated(2, byte(0x001234) == 1 && byte(0x001235) == 2))"));
        }
    }
}
