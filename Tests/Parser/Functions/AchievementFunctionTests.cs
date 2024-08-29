using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using System.Linq;
using System.Text;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class AchievementFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new AchievementFunction();
            Assert.That(def.Name.Name, Is.EqualTo("achievement"));
            Assert.That(def.Parameters.Count, Is.EqualTo(9));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("title"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("description"));
            Assert.That(def.Parameters.ElementAt(2).Name, Is.EqualTo("points"));
            Assert.That(def.Parameters.ElementAt(3).Name, Is.EqualTo("trigger"));
            Assert.That(def.Parameters.ElementAt(4).Name, Is.EqualTo("id"));
            Assert.That(def.Parameters.ElementAt(5).Name, Is.EqualTo("published"));
            Assert.That(def.Parameters.ElementAt(6).Name, Is.EqualTo("modified"));
            Assert.That(def.Parameters.ElementAt(7).Name, Is.EqualTo("badge"));
            Assert.That(def.Parameters.ElementAt(8).Name, Is.EqualTo("type"));    

            Assert.That(def.DefaultParameters.Count(), Is.EqualTo(5));
            Assert.That(def.DefaultParameters["id"], Is.InstanceOf<IntegerConstantExpression>());
            Assert.That(((IntegerConstantExpression)def.DefaultParameters["id"]).Value, Is.EqualTo(0));
            Assert.That(def.DefaultParameters["published"], Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)def.DefaultParameters["published"]).Value, Is.EqualTo(""));
            Assert.That(def.DefaultParameters["modified"], Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)def.DefaultParameters["modified"]).Value, Is.EqualTo(""));
            Assert.That(def.DefaultParameters["badge"], Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)def.DefaultParameters["badge"]).Value, Is.EqualTo("0"));
            Assert.That(def.DefaultParameters["type"], Is.InstanceOf<StringConstantExpression>());
            Assert.That(((StringConstantExpression)def.DefaultParameters["type"]).Value, Is.EqualTo(""));
        }

        private Achievement Evaluate(string input, string expectedError = null)
        {
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var parser = new AchievementScriptInterpreter();

            if (expectedError == null)
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
                Assert.That(parser.ErrorMessage, Is.Not.Null.And.EqualTo(expectedError));
            }

            return parser.Achievements.FirstOrDefault();
        }

        [Test]
        public void TestSimple()
        {
            var achievement = Evaluate("achievement(\"T\", \"D\", 5, byte(0x1234) == 1)");
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));

            var builder = new AchievementBuilder(achievement);
            Assert.That(builder.SerializeRequirements(new SerializationContext()), Is.EqualTo("0xH001234=1"));
        }

        [Test]
        public void TestConstructAlts()
        {
            var achievement = Evaluate("trigger = always_false()\n" +
                                       "for i in range(0, 10)\n" +
                                       "    trigger = trigger || word(0x1000+i) == 10 && prev(word(0x1000+i)) < 10\n" +
                                       "achievement(\"A\", \"B\", 10, trigger)");

            var expected = new StringBuilder();
            expected.Append("1=1");
            for (int i = 0; i <= 10; i++)
                expected.AppendFormat("S0x {0:x6}=10_d0x {0:x6}<10", i + 0x1000);

            var builder = new AchievementBuilder(achievement);
            Assert.That(builder.SerializeRequirements(new SerializationContext()), Is.EqualTo(expected.ToString()));
        }

        [Test]
        public void TestConstructAltsExcessive()
        {
            // don't call Evaluate() on this as the AchievementBuilder.Optimize call takes over 10 seconds
            var achievement = Evaluate("trigger = always_false()\n" +
                                       "for i in range(0, 9000)\n" +
                                       "    trigger = trigger || word(0x1000+i) == 10 && prev(word(0x1000+i)) < 10" +
                                       "achievement(\"A\", \"B\", 10, trigger)");

            var expected = new StringBuilder();
            expected.Append("1=1");
            for (int i = 0; i <= 9000; i++)
                expected.AppendFormat("S0x {0:x6}=10_d0x {0:x6}<10", i + 0x1000);

            var builder = new AchievementBuilder(achievement);
            Assert.That(builder.SerializeRequirements(new SerializationContext()), Is.EqualTo(expected.ToString()));
        }

        [Test]
        public void TestInvalidComparisonInTrigger()
        {
            var input = "function a() => byte(0x1234)\n" +
                        "achievement(\"T\", \"D\", 5, a == 1)";

            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var parser = new AchievementScriptInterpreter();
            Assert.That(parser.Run(tokenizer), Is.False);
            Assert.That(parser.ErrorMessage, Is.EqualTo(
                "2:26 Invalid value for parameter: trigger\r\n" +
                "- 2:26 Cannot compare function reference and integer"));
        }

        [Test]
        public void TestInvalidComparisonInHelperFunction()
        {
            var input = "function a() => byte(0x1234)\n" +
                        "function b() => a == 1\n" +
                        "achievement(\"T\", \"D\", 5, b())";

            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var parser = new AchievementScriptInterpreter();
            Assert.That(parser.Run(tokenizer), Is.False);
            Assert.That(parser.ErrorMessage, Is.EqualTo(
                "3:26 Invalid value for parameter: trigger\r\n" +
                "- 3:26 b call failed\r\n" +
                "- 2:17 Cannot compare function reference and integer"));
        }

        [Test]
        public void TestIncompleteComparison()
        {
            var input = "function a() => byte(0x1234)\n" +
                        "achievement(\"T\", \"D\", 5, a())";

            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var parser = new AchievementScriptInterpreter();
            Assert.That(parser.Run(tokenizer), Is.False);
            Assert.That(parser.ErrorMessage, Is.EqualTo(
                "2:1 achievement call failed\r\n" +
                "- 2:26 trigger: Cannot convert memory accessor to requirement"));
        }

        [Test]
        public void TestIncompleteComparisonInHelperFunction()
        {
            var input = "function a() => byte(0x1234)\n" +
                        "function b() => a() + 1\r\n" +
                        "achievement(\"T\", \"D\", 5, b())";

            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var parser = new AchievementScriptInterpreter();
            Assert.That(parser.Run(tokenizer), Is.False);
            Assert.That(parser.ErrorMessage, Is.EqualTo(
                "3:1 achievement call failed\r\n" +
                "- 3:26 trigger: Cannot convert memory accessor to requirement"));
        }

        [Test]
        public void TestIncompleteComparisonInHelperFunctionLogical()
        {
            var input = "function a() => byte(0x1234)\n" +
                        "function b() => a() && byte(0x2345) == 6\r\n" +
                        "achievement(\"T\", \"D\", 5, b())";

            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var parser = new AchievementScriptInterpreter();
            Assert.That(parser.Run(tokenizer), Is.False);
            Assert.That(parser.ErrorMessage, Is.EqualTo(
                "3:1 achievement call failed\r\n" +
                "- 3:26 trigger: Cannot convert conditional expression to requirement\r\n" +
                "- 2:17 Cannot convert memory accessor to requirement"));
        }

        [Test]
        [TestCase("", AchievementType.Standard)]
        [TestCase("progression", AchievementType.Progression)]
        [TestCase("win_condition", AchievementType.WinCondition)]
        [TestCase("missable", AchievementType.Missable)]
        public void TestType(string type, AchievementType expectedType)
        {
            var achievement = Evaluate("achievement(\"T\", \"D\", 5, byte(0x1234) == 1, type=\"" + type + "\")");
            Assert.That(achievement.Type, Is.EqualTo(expectedType));

            var builder = new AchievementBuilder(achievement);
            Assert.That(builder.Type, Is.EqualTo(expectedType));
        }
    }
}
