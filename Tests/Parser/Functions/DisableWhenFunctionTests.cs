using NUnit.Framework;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Tests.Expressions;
using RATools.Parser.Tests.Expressions.Trigger;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class DisableWhenFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = AchievementScriptInterpreter.GetGlobalScope().GetFunction("disable_when");
            Assert.That(def, Is.Not.Null);
            Assert.That(def.Name.Name, Is.EqualTo("disable_when"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("comparison"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("until"));
        }

        [Test]
        [TestCase("disable_when(byte(0x001234) == 5)")]
        [TestCase("disable_when(byte(0x001234) == 5, until=byte(0x002345) == 6)")]
        [TestCase("disable_when(repeated(6, byte(0x001234) == 5))")]
        public void TestAppendString(string input)
        {
            var clause = TriggerExpressionTests.Parse<DisableWhenRequirementExpression>(input);
            ExpressionTests.AssertAppendString(clause, input);
        }

        [Test]
        [TestCase("disable_when(byte(0x1234) == 5)", "P:0xH001234=5.1.")]
        [TestCase("disable_when(byte(0x1234) == 5, until=byte(0x2345) == 6)", "Z:0xH002345=6_P:0xH001234=5.1.")]
        [TestCase("disable_when(repeated(6, byte(0x001234) == 5))", "P:0xH001234=5.6.")]
        [TestCase("disable_when(repeated(6, byte(0x001234) == 5), until=byte(0x2345) == 6)", "Z:0xH002345=6_P:0xH001234=5.6.")]
        [TestCase("disable_when(byte(0x1234) == 56 && byte(0x1235) == 55, until=byte(0x2345) == 67 && byte(0x2346) == 66)", "N:0xH002345=67_Z:0xH002346=66_N:0xH001234=56_P:0xH001235=55.1.")]
        [TestCase("disable_when(byte(0x1234) == 56 || byte(0x1235) == 55, until=byte(0x2345) == 67 || byte(0x2346) == 66)", "O:0xH002345=67_Z:0xH002346=66_O:0xH001234=56_P:0xH001235=55.1.")]
        [TestCase("disable_when(tally(3, once(byte(0x1234) == 56), byte(0x1235) == 55, repeated(2, byte(0x1236) == 54)), until=byte(0x1234) == 1)",
            "Z:0xH001234=1_C:0xH001234=56.1._Z:0xH001234=1_C:0xH001236=54.2._Z:0xH001234=1_P:0xH001235=55.3.")] // ResetNextIf has to proceed each clause of tally
        public void TestBuildTrigger(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<DisableWhenRequirementExpression>(input);
            TriggerExpressionTests.AssertSerialize(clause, expected);
        }
    }
}
