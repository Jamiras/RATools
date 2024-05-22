using Jamiras.Components;
using Jamiras.Core.Tests;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using System.Linq;

namespace RATools.Parser.Tests
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

        private static InterpreterScope Evaluate(string script, string expectedError = null)
        {
            var groups = new ExpressionGroupCollection();
            groups.Scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());

            groups.Parse(Tokenizer.CreateTokenizer(script));

            foreach (var error in groups.Errors)
                Assert.Fail(error.Message);

            var interpreter = new AchievementScriptInterpreter();

            if (expectedError != null)
            {
                Assert.That(interpreter.Run(groups, null), Is.False);
                Assert.That(interpreter.ErrorMessage, Is.EqualTo(expectedError));
                return null;
            }

            if (!interpreter.Run(groups, null))
                Assert.Fail(interpreter.ErrorMessage);

            return groups.Scope;
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
        public void TestDictionaryLogic()
        {
            var parser = Parse("dict = { 1: \"T\", 2: \"D\" }\n" +
                               "function f(key) => dict[key] == \"D\"\n" +
                               "if (f(2))\n" +
                               "    achievement(dict[1], dict[2], 5, byte(0x1234) == 1)");
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
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("1:31 comparison: Cannot convert memory accessor to requirement"));
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
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH1234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValuePlusOne()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234) + 1))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH1234_v1) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueMinusOne()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234) - 1))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH1234_v-1) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueMultiply()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234) * 10 + 1))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH1234*10_v1) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueDivide()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234) / 4))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH1234/4) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueFunction()
        {
            var parser = Parse("function test() => byte(0x1234)\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", test()))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH1234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueReused()
        {
            var parser = Parse("rich_presence_conditional_display(byte(0) == 0, \"value {0} there\", rich_presence_value(\"Test\", byte(0x2345)))\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234)))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\n?0xH0000=0?value @Test(0xH2345) there\r\nvalue @Test(0xH1234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueReusedDifferingFormat()
        {
            var parser = Parse("rich_presence_conditional_display(byte(0) == 0, \"value {0} there\", rich_presence_value(\"Test\", byte(0x2345), format=\"VALUE\"))\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234), format=\"FRAMES\"))", false);
            Assert.That(parser.ErrorMessage, Is.EqualTo("1:68 Multiple rich_presence_value calls with the same name must have the same format"));
        }

        [Test]
        public void TestRichPresenceValueFloatModifier()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234) * 1.5))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH1234*1.5) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueFloatModifierLocale()
        {
            using (var cultureOverride = new CultureOverride("fr-FR"))
            {
                var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234) * 1.5))");
                Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH1234*1.5) here\r\n"));
            }
        }

        [Test]
        public void TestRichPresenceValueFloatModifierWithPointer()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(byte(0x1234) + 2) * 1.5))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(I:0xH1234_M:0xH0002*f1.5) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueFloatModifierLocaleWithPointer()
        {
            using (var cultureOverride = new CultureOverride("fr-FR"))
            {
                var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(byte(0x1234) + 2) * 1.5))");
                Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(I:0xH1234_M:0xH0002*f1.5) here\r\n"));
            }
        }

        [Test]
        public void TestRichPresenceLookup()
        {
            var parser = Parse("dict = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234), dict))");
            Assert.That(parser.RichPresence, Is.EqualTo("Lookup:Test\r\n1=Yes\r\n2=No\r\n\r\nDisplay:\r\nvalue @Test(0xH1234) here\r\n"));
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
            Assert.That(parser.RichPresence, Is.EqualTo("Lookup:Test\r\n1=Yes\r\n2=No\r\n\r\nDisplay:\r\n?0xH0000=0?value @Test(0xH2345) there\r\nvalue @Test(0xH1234) here\r\n"));
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
        public void TestRichPresenceSkippedParameter()
        {
            // {1} does not appear in the format string, so Unused should not be defined as a Format
            var parser = Parse("rich_presence_display(\"value {0} here, {2} there\",\n" +
                                 "rich_presence_value(\"Test\", byte(0x1234))," +
                                 "rich_presence_value(\"Unused\", byte(0x2345))," +
                                 "rich_presence_value(\"Third\", byte(0x3456))" +
                               ")");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nFormat:Third\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH1234) here, @Third(0xH3456) there\r\n"));
        }


        [Test]
        public void TestRichPresenceLookupReusedEquivalentDictionary()
        {
            // multiple display strings can use the same lookup only if they use the same dictionary and fallback
            var parser = Parse("dict1 = { 1:\"Yes\", 2:\"No\" }\n" +
                               "dict2 = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_conditional_display(byte(0) == 0, \"value {0} there\", rich_presence_lookup(\"Test\", byte(0x2345), dict1))\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234), dict2))");
            Assert.That(parser.RichPresence, Is.EqualTo("Lookup:Test\r\n1=Yes\r\n2=No\r\n\r\nDisplay:\r\n?0xH0000=0?value @Test(0xH2345) there\r\nvalue @Test(0xH1234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceLookupMultipleDictionaries()
        {
            // multiple display strings can use the same lookup only if they use the same dictionary and fallback
            var parser = Parse("dict1 = { 1:\"Yes\", 2:\"No\" }\n" +
                               "dict2 = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_conditional_display(byte(0) == 0, \"value {0} there\", rich_presence_lookup(\"Test1\", byte(0x2345), dict1))\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test2\", byte(0x1234), dict2))");
            Assert.That(parser.RichPresence, Is.EqualTo("Lookup:Test1\r\n1=Yes\r\n2=No\r\n\r\nLookup:Test2\r\n1=Yes\r\n2=No\r\n\r\nDisplay:\r\n?0xH0000=0?value @Test1(0xH2345) there\r\nvalue @Test2(0xH1234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceLookupPlusOne()
        {
            var parser = Parse("dict = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234) + 1, dict))");
            Assert.That(parser.RichPresence, Is.EqualTo("Lookup:Test\r\n1=Yes\r\n2=No\r\n\r\nDisplay:\r\nvalue @Test(0xH1234_v1) here\r\n"));
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
            Assert.That(parser.RichPresence, Is.EqualTo("Lookup:Test\r\n1=Yes\r\n2=No\r\n*=Maybe\r\n\r\nDisplay:\r\nvalue @Test(0xH1234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceLookupInvalidFallback()
        {
            var parser = Parse("dict = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234), dict, 1))", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("2:90 fallback: Cannot convert integer to string"));
        }

        [Test]
        public void TestRichPresenceLookupInvalidFallbackVariable()
        {
            var parser = Parse("dict = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234), dict, dict))", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("2:90 fallback: Cannot convert dictionary to string"));
        }

        /*
        [Test]
        public void TestFunctionCallInFunctionInExpression()
        {
            var input =        "function foo() {\n" +
                               "    trigger = always_false() always_true()\n" + // always_true() should be flagged as an error
                               "\n" +
                               "    for offset in [0, 1] {\n" +
                               "        trigger = trigger || byte(offset) == 10\n" +
                               "    }\n" +
                               "\n" +
                               "    return trigger\n" +
                               "}\n" +
                               "achievement(\"Title\", \"Description\", 5, foo())\n";

            var tokenizer = Tokenizer.CreateTokenizer(input);
            var parser = new AchievementScriptInterpreter();
            Assert.That(parser.Run(tokenizer), Is.False);
            Assert.That(parser.ErrorMessage, Is.EqualTo(
                "10:40 Invalid value for parameter: trigger\r\n" +
                "- 10:40 foo call failed\r\n" +
                "- 2:30 always_true call failed\r\n" +
                "- 2:30 always_true has no meaning outside of a trigger clause"));
        }
        */

        [Test]
        public void TestErrorInFunctionParameterLocation()
        {
            var parser = Parse("achievement(\"Title\", \"Description\", 5, prev(tens) == 100)\n", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("1:45 Unknown variable: tens"));
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
        [TestCase("word(0x1234) * 10 + byte(0x1235) == 10000", true, "(word(0x001234) * 10 + byte(0x001235)) == 10000")]
        [TestCase("byte(0x1235) + word(0x1234) * 10 == 10000", true, "(word(0x001234) * 10 + byte(0x001235)) == 10000")] // multiplication can't be on last condition, reorder them
        [TestCase("word(0x1234) * 10 + byte(0x1235) * 2 == 10000", true, "(word(0x001234) * 10 + byte(0x001235) * 2) == 10000")] // multiplication can't be on last condition, add an extra dummy condition
        [TestCase("(word(0x1234) - 1) * 4 < 100", true, "word(0x001234) < 26")]
        [TestCase("(word(0x1234) - 1) / 4 < 100", true, "word(0x001234) < 401")]
        [TestCase("(word(0x1234) - 1) * 4 < 99", true, "word(0x001234) <= 25")]
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

        [Test]
        public void TestRepeatedNever()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, once(byte(0x2222) == 0) && " +
                "repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2)))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement),
                Is.EqualTo("once(byte(0x002222) == 0) && repeated(2, byte(0x001234) == 1 && never(byte(0x002345) == 2))"));

            // reverse the order - never should still appear first in the definition, but be displayed last
            parser = Parse("achievement(\"T\", \"D\", 5, once(byte(0x2222) == 0) && " +
                "repeated(2, never(byte(0x2345) == 2) && byte(0x1234) == 1))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement),
                Is.EqualTo("once(byte(0x002222) == 0) && repeated(2, byte(0x001234) == 1 && never(byte(0x002345) == 2))"));
        }

        [Test]
        public void TestAssignVariableToFunctionWithNoReturn()
        {
            Evaluate(
                "function a(i) { b = i }\n" +
                "c = a(3)",

                "2:5 a did not return a value"
            );
        }

        [Test]
        public void TestAssignFunctionToVariable()
        {
            var scope = Evaluate(
                "function a(i) => i + 1\n" +
                "b = a\n" +
                "c = b\n" +
                "d = c(3)\n");

            var b = scope.GetVariable("b");
            Assert.That(b, Is.InstanceOf<FunctionReferenceExpression>());
            Assert.That(((FunctionReferenceExpression)b).Name, Is.EqualTo("a"));

            var c = scope.GetVariable("c");
            Assert.That(c, Is.InstanceOf<FunctionReferenceExpression>());
            Assert.That(((FunctionReferenceExpression)c).Name, Is.EqualTo("a"));

            var d = scope.GetVariable("d");
            Assert.That(d, Is.InstanceOf<IntegerConstantExpression>());
            var integerConstant = (IntegerConstantExpression)d;
            Assert.That(integerConstant.Value, Is.EqualTo(4));
        }

        [Test]
        public void TestPassFunctionToFunction()
        {
            var scope = Evaluate(
                "function a(i) => i + 1\n" +
                "function b(f,i) => f(i)\n" +
                "function c(i) => b(a,i)\n" +
                "d = c(3)\n");

            var d = scope.GetVariable("d");
            Assert.That(d, Is.InstanceOf<IntegerConstantExpression>());
            var integerConstant = (IntegerConstantExpression)d;
            Assert.That(integerConstant.Value, Is.EqualTo(4));
        }

        [Test]
        public void TestPassFunctionToFunctionNameConflict()
        {
            // the "b" function defines an "a" parameter within it's scope, which hides the
            // global "a" function. "c" calls "b" with the "a" function as the parameter.
            // make sure the function reference to "a" can still find the global "a" function.
            var scope = Evaluate(
                "function a(n) => n + 1\n" +
                "function b(a, n) => a(n)\n" +
                "c = b(a, 3)\n");

            var c = scope.GetVariable("c");
            Assert.That(c, Is.InstanceOf<IntegerConstantExpression>());
            var integerConstant = (IntegerConstantExpression)c;
            Assert.That(integerConstant.Value, Is.EqualTo(4));
        }

        [Test]
        public void TestDefaultParameterPassthrough()
        {
            var parser = Parse("function a(id = 0, badge = \"0\")\n" +
                               "{" +
                               "    achievement(\"T\", \"D\", 5, byte(0x1234) == 1, id=id, badge=badge)\n" +
                               "}\n" +
                               "\n" +
                               "achievement(\"T\", \"D\", 5, byte(0x1234) == 1)\n" +
                               "a()\n" +
                               "a(id = 10)\n" +
                               "a(badge = \"5555\")\n" +
                               "a(20, \"12345\")\n");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(5));

            // calling achievement() directly without a badge or id
            var achievement = parser.Achievements.ElementAt(0);
            Assert.That(achievement.Id, Is.EqualTo(0));
            Assert.That(achievement.BadgeName, Is.EqualTo("0"));

            // calling achievement() indirectly without a badge or id
            achievement = parser.Achievements.ElementAt(1);
            Assert.That(achievement.Id, Is.EqualTo(0));
            Assert.That(achievement.BadgeName, Is.EqualTo("0"));

            // calling achievement() indirectly without a badge
            achievement = parser.Achievements.ElementAt(2);
            Assert.That(achievement.Id, Is.EqualTo(10));
            Assert.That(achievement.BadgeName, Is.EqualTo("0"));

            // calling achievement() indirectly without an id
            achievement = parser.Achievements.ElementAt(3);
            Assert.That(achievement.Id, Is.EqualTo(0));
            Assert.That(achievement.BadgeName, Is.EqualTo("5555"));

            // calling achievement() indirectly
            achievement = parser.Achievements.ElementAt(4);
            Assert.That(achievement.Id, Is.EqualTo(20));
            Assert.That(achievement.BadgeName, Is.EqualTo("12345"));
        }

        [Test]
        [TestCase("b = function a(i) => i + 1\nc = b(3)")] // full function definition
        [TestCase("b = (i) => i + 1\nc = b(3)")] // anonymous function definition
        [TestCase("b = (i) { return i + 1 }\nc = b(3)")] // anonymous long-form function definition
        [TestCase("b = (i, j) => i + j\nc = b(3, 1)")] // anonymous multiple parameter function definition
        [TestCase("b = i => i + 1\nc = b(3)")] // anonymous function definition without parenthesis
        [TestCase("a = i => i + 1\nb = (f,i) => f(i)\nc = b(a, 3)")] // anonymous function passed as parameter
        [TestCase("a = f => f(1)\nb = n => a(i => i + n)\nc = b(3)")] // anonymous function captures variable
        [TestCase("b = (f,i) => f(i)\nc = b(i => i + 1, 3)")] // anonymous function defined as parameter
        [TestCase("function p(fn) { if (fn()) return 2 else return 0 }\n" +
                  "function t() => true\nfunction f() => false\nc=p(t)+p(f)+p(f)+p(t)+p(f)")]
        public void TestAnonymousFunction(string definition)
        {
            var scope = Evaluate(definition);

            var c = scope.GetVariable("c");
            Assert.That(c, Is.InstanceOf<IntegerConstantExpression>());
            var integerConstant = (IntegerConstantExpression)c;
            Assert.That(integerConstant.Value, Is.EqualTo(4));
        }

        [Test]
        public void TestNestedComparisonFunction()
        {
            Evaluate("function f() => byte(0x1234) == 2\n" +
                     "achievement(\"T\", \"D\", 5, f() == 1)\n",

                     "2:26 Invalid value for parameter: trigger\r\n" +
                     "- 2:26 Cannot chain comparisons");
        }

        [Test]
        public void TestIfFunctionResultBoolean()
        {
            var scope = Evaluate("function t() => true\n" +
                                 "function f() => false\n" +
                                 "a = 0\n" +
                                 "b = 0\n" +
                                 "c = 0\n" +
                                 "d = 0\n" +
                                 "if (t()) { a = 1 }" +
                                 "if (!t()) { b = 1 }" +
                                 "if (f()) { c = 1 }" +
                                 "if (!f()) { d = 1 }");

            var a = scope.GetVariable("a");
            Assert.That(a, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)a).Value, Is.EqualTo(1));

            var b = scope.GetVariable("b");
            Assert.That(b, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)b).Value, Is.EqualTo(0));

            var c = scope.GetVariable("c");
            Assert.That(c, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)c).Value, Is.EqualTo(0));

            var d = scope.GetVariable("d");
            Assert.That(d, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)d).Value, Is.EqualTo(1));
        }

        [Test]
        public void TestIfFunctionResultAlwaysTrueFalse()
        {
            var scope = Evaluate("function t() => always_true()\n" +
                                 "function f() => always_false()\n" +
                                 "a = 0\n" +
                                 "b = 0\n" +
                                 "c = 0\n" +
                                 "d = 0\n" +
                                 "if (t()) { a = 1 }" +
                                 "if (!t()) { b = 1 }" +
                                 "if (f()) { c = 1 }" +
                                 "if (!f()) { d = 1 }");

            var a = scope.GetVariable("a");
            Assert.That(a, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)a).Value, Is.EqualTo(1));

            var b = scope.GetVariable("b");
            Assert.That(b, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)b).Value, Is.EqualTo(0));

            var c = scope.GetVariable("c");
            Assert.That(c, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)c).Value, Is.EqualTo(0));

            var d = scope.GetVariable("d");
            Assert.That(d, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)d).Value, Is.EqualTo(1));
        }

        [Test]
        public void TestIfAny()
        {
            var scope = Evaluate("a = 0\n" +
                                 "b = 0\n" +
                                 "c = 0\n" +
                                 "d = 0\n" +
                                 "if (any_of([1,2,3], n => n == 2)) { a = 1 }" +
                                 "if (any_of([1,2,3], n => n == 5)) { b = 1 }" +
                                 "if (!any_of([1,2,3], n => n == 3)) { c = 1 }" +
                                 "if (!any_of([1,2,3], n => n == 8)) { d = 1 }");

            var a = scope.GetVariable("a");
            Assert.That(a, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)a).Value, Is.EqualTo(1));

            var b = scope.GetVariable("b");
            Assert.That(b, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)b).Value, Is.EqualTo(0));

            var c = scope.GetVariable("c");
            Assert.That(c, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)c).Value, Is.EqualTo(0));

            var d = scope.GetVariable("d");
            Assert.That(d, Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)d).Value, Is.EqualTo(1));
        }

        [Test]
        public void TestDeltaBCDComparison()
        {
            var parser = Parse("function f() => bcd(byte(0x1234))\n" +
                               "achievement(\"T\", \"D\", 5, f() != prev(f()))\n");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.ElementAt(0);
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) != prev(byte(0x001234))"));
        }

        [Test]
        public void TestRuntimeConditional()
        {
            var parser = Parse(
                "function f() {\n" +
                "  if (byte(0x1234) == 2) return byte(0x1234) else return byte(0x4321)\n" +
                "}\n" +
                "achievement(\"a\", \"b\", 5, f() == 4)\n", false
            );

            bool seen = false;
            for (var error = parser.Error; error != null; error = error.InnerError)
            {
                if (error.Message == "Comparison contains runtime logic.")
                {
                    Assert.That(error.Location.Start.Column, Is.EqualTo(7));
                    Assert.That(error.Location.End.Column, Is.EqualTo(23));

                    seen = true;
                    break;
                }
            }
            Assert.That(seen, Is.True);
        }

        [Test]
        public void TestTallyNeverComplex()
        {
            var parser = Parse(
                "achievement(\"T\", \"D\", 5,\n" +
                "  tally(8, byte(0x1234) == 1 && never(byte(0x2345) == 2 && byte(0x3456) == 3))\n"+
                ")");

            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("never((byte(0x002345) == 2 && byte(0x003456) == 3)) && repeated(8, byte(0x001234) == 1)"));
        }

        [Test]
        public void TestTallyMultipleNeverComplex()
        {
            var parser = Parse(
                "achievement(\"T\", \"D\", 5,\n" +
                "  tally(8,\n" +
                "    byte(0x1234) == 1 && never(byte(0x2345) == 2 && byte(0x3456) == 3),\n" +
                "    byte(0x1234) == 2 && never(byte(0x2345) == 3 && byte(0x3456) == 4)\n" +
                "  )\n" +
                ")");

            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("tally(8, byte(0x001234) == 1 && never(byte(0x002345) == 2 && byte(0x003456) == 3), byte(0x001234) == 2 && never(byte(0x002345) == 3 && byte(0x003456) == 4))"));
        }

        [Test]
        public void TestMinimumVersionMetaComment()
        {
            // Without meta comment, minimum version will be 0.30, which doesn't support OrNext, so 
            // (A || B) && (C || D) will have to be cross-multiplied into four alts.
            var parser = Parse(
                "// GameName\n" +
                "achievement(\"T\", \"D\", 5,\n" +
                "  byte(0x1234) == 1 && (byte(0x2345) == 5 || byte(0x2345) == 6) && (byte(0x3456) == 6 || byte(0x3456) == 7)\n" +
                ")");

            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Trigger.Serialize(parser.SerializationContext),
                Is.EqualTo("0xH1234=1S0xH2345=5_0xH3456=6S0xH2345=5_0xH3456=7S0xH2345=6_0xH3456=6S0xH2345=6_0xH3456=7"));

            // With meta comment, OrNext is supported, so alts aren't needed.
            parser = Parse(
                "// GameName\n" +
                "// #MinimumVersion=1.0\n" +
                "achievement(\"T\", \"D\", 5,\n" +
                "  byte(0x1234) == 1 && (byte(0x2345) == 5 || byte(0x2345) == 6) && (byte(0x3456) == 6 || byte(0x3456) == 7)\n" +
                ")");

            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            achievement = parser.Achievements.First();
            Assert.That(achievement.Trigger.Serialize(parser.SerializationContext),
                Is.EqualTo("0xH1234=1_O:0xH2345=5_0xH2345=6_O:0xH3456=6_0xH3456=7"));
        }
    }
}
