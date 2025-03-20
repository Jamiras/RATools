using NUnit.Framework;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using RATools.Parser.Tests.Expressions;
using RATools.Parser.Tests.Expressions.Trigger;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class MeasuredFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new MeasuredFunction();
            Assert.That(def.Name.Name, Is.EqualTo("measured"));
            Assert.That(def.Parameters.Count, Is.EqualTo(3));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("comparison"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("when"));
            Assert.That(def.Parameters.ElementAt(2).Name, Is.EqualTo("format"));
        }

        [Test]
        [TestCase("measured(byte(0x001234) == 5)")]
        [TestCase("measured(byte(0x001234) == 5, format=\"percent\")")]
        [TestCase("measured(byte(0x001234) == 5, when=byte(0x002345) == 6)")]
        [TestCase("measured(byte(0x001234) == 5, when=byte(0x002345) == 6, format=\"percent\")")]
        [TestCase("measured(repeated(6, byte(0x001234) == 5))")]
        public void TestAppendString(string input)
        {
            var clause = TriggerExpressionTests.Parse<MeasuredRequirementExpression>(input);
            ExpressionTests.AssertAppendString(clause, input);
        }

        [Test]
        [TestCase("measured(byte(0x001234) == 5)", "M:0xH001234=5")]
        [TestCase("measured(byte(0x001234) == 5, format=\"percent\")", "G:0xH001234=5")]
        [TestCase("measured(byte(0x001234) == 5, when=byte(0x002345) == 6)", "M:0xH001234=5_Q:0xH002345=6")]
        [TestCase("measured(byte(0x001234) == 5, when=byte(0x002345) == 6 || byte(0x003456) == 7)",
            "M:0xH001234=5_O:0xH002345=6_Q:0xH003456=7")] // ORed when cannot be split
        [TestCase("measured(byte(0x001234) == 5, when=byte(0x002345) == 6 && byte(0x003456) == 7)",
            "M:0xH001234=5_Q:0xH002345=6_Q:0xH003456=7")] // ANDed when can be split
        [TestCase("measured(byte(0x001234) == 5, when=once(byte(0x002345) == 6 && byte(0x003456) == 7))",
            "M:0xH001234=5_N:0xH002345=6_Q:0xH003456=7.1.")] // complex when cannot be split
        [TestCase("measured(byte(0x001234) == 5, when=once(byte(0x002345) == 6) && once(byte(0x003456) == 7))",
            "M:0xH001234=5_Q:0xH002345=6.1._Q:0xH003456=7.1.")] // individual onces in when can be split
        [TestCase("measured(byte(0x001234) == 5, when=byte(0x002345) == 6, format=\"percent\")",
            "G:0xH001234=5_Q:0xH002345=6")]
        [TestCase("measured(repeated(6, byte(0x001234) == 5))", "M:0xH001234=5.6.")]
        [TestCase("measured(repeated(6, byte(0x001234) + byte(0x002345) == 5))",
            "A:0xH001234_M:0xH002345=5.6.")]
        [TestCase("measured(byte(0x001234) + byte(0x002345) == 5)", "A:0xH001234_M:0xH002345=5")]
        [TestCase("measured(byte(0x001234 + word(0x002345)) == 5)", "I:0x 002345_M:0xH001234=5")]
        [TestCase("measured(repeated(10, byte(0x001234) == 5 && byte(0x002345) == 6)",
            "N:0xH001234=5_M:0xH002345=6.10.")]
        [TestCase("measured(6 - byte(0x1234) == 6)", "B:0xH001234_M:6=6")]
        [TestCase("measured(300 - byte(0x1234) >= 100)", "B:0xH001234_M:300>=100")]
        [TestCase("measured(byte(0x1234) + 22 >= 100)", "A:22_M:0xH001234>=100")] // measurements include adjustment so target is unchanged
        [TestCase("measured(byte(0x1234) + 22 >= 100, format=\"percent\")", "A:22_G:0xH001234>=100")] // measurements include adjustment so target is unchanged
        [TestCase("measured(byte(0x1234) - 6 >= 100)", "B:6_M:0xH001234>=100")] // measurements include adjustment so target is unchanged
        [TestCase("measured(byte(0x1234) - 6 >= 100, format=\"percent\")", "B:6_G:0xH001234>=100")] // measurements include adjustment so target is unchanged
        [TestCase("measured(tally(2, byte(0x1234) == 120, byte(0x1234) == 126))", "C:0xH001234=120_M:0xH001234=126.2.")]
        [TestCase("measured(bitcount(0x1234) == 8)", "M:0xK001234=8")] // should not be optimized to byte=255
        [TestCase("measured(byte(0x1234) + (60 - byte(0x2345)) * 100 / 60 == 9)",
            "K:0xH002345*100_B:{recall}/60_A:100_M:0xH001234=9")]
        public void TestBuildTrigger(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<MeasuredRequirementExpression>(input);
            TriggerExpressionTests.AssertSerialize(clause, expected);
        }

        [Test]
        [TestCase("measured(byte(0x1234))", "M:0xH001234")]
        [TestCase("measured(byte(0x1234) * 2)", "M:0xH001234*2")]
        [TestCase("measured(byte(0x1234) / 2)", "M:0xH001234/2")]
        [TestCase("measured(byte(word(0x1234) + 16))", "I:0x 001234_M:0xH000010")]
        [TestCase("measured(repeated(0, byte(0x1234) == 5))", "M:0xH001234=5")]
        [TestCase("measured(tally(0, byte(0x1234) == 20, byte(0x1234) == 67))", "C:0xH001234=20_M:0xH001234=67")]
        [TestCase("measured(tally(0, byte(0x1234) == 20 && byte(0x2345) == 67))", "N:0xH001234=20_M:0xH002345=67")]
        public void TestBuildValue(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<MeasuredRequirementExpression>(input);
            TriggerExpressionTests.AssertSerializeValue(clause, expected);
        }

        [Test]
        public void TestComparisonAndNext()
        {
            TriggerExpressionTests.AssertBuildTriggerError(
                "measured(byte(0x2345) == 6 && byte(0x1234) == 120)",
                "measured comparison can only have one logical clause");
        }

        [Test]
        public void TestComparisonConstant()
        {
            var input = "measured(2, when=byte(0x2345) == 6)";

            // acceptable in a value
            var clause = TriggerExpressionTests.Parse<MeasuredRequirementExpression>(input);
            TriggerExpressionTests.AssertSerializeValue(clause, "M:2_Q:0xH002345=6");

            // not acceptable in a trigger
            TriggerExpressionTests.AssertBuildTriggerError(input, "measured value in a trigger must be a comparison");
        }

        [Test]
        public void TestRepeatedZero()
        {
            TriggerExpressionTests.AssertBuildTriggerError(
                "measured(repeated(0, byte(0x1234) == 20))",
                "Unbounded count is only supported in measured value expressions");
        }

        [Test]
        public void TestComparisonFormatUnknown()
        {
            TriggerExpressionTests.AssertParseError(
                "measured(byte(0x1234) == 120, format=\"unknown\")",
                "Unknown format: unknown");
        }

        [Test]
        public void TestTallyZero()
        {
            TriggerExpressionTests.AssertBuildTriggerError(
                "measured(tally(0, byte(0x1234) == 20, byte(0x1234) == 67))",
                "Unbounded count is only supported in measured value expressions");
        }

        [Test]
        public void TestValueComparison()
        {
            TriggerExpressionTests.AssertBuildValueError(
                "measured(byte(0x1234) == 20)",
                "Comparison must be wrapped in tally(0,...) to be used in measured value expression");
        }


        [Test]
        public void TestValueComparisonAndNext()
        {
            TriggerExpressionTests.AssertBuildValueError(
                "measured(byte(0x1234) == 20 && byte(0x2345) == 67)",
                "Comparison must be wrapped in tally(0,...) to be used in measured value expression");
        }
    }
}