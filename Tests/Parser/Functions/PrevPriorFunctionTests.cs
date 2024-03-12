using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Functions;
using System.Linq;
using System.Text;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class PrevPriorFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new PrevPriorFunction("prev", FieldType.PreviousValue);
            Assert.That(def.Name.Name, Is.EqualTo("prev"));
            Assert.That(def.Parameters.Count, Is.EqualTo(1));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("accessor"));
        }

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

        private string Process(string input)
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, " + input + ")");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            var builder = new AchievementBuilder(achievement);
            return builder.SerializeRequirements(new SerializationContext());
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

        [Test]
        [TestCase("prev(byte(0x1234)) == 56", "d0xH001234=56")]
        [TestCase("prev(byte(0x1234) + 6) == 10", "d0xH001234=4")] // modifier is extracted and comparison is normalized
        [TestCase("prev(byte(0x1234) - 1) / 4 == 10", "d0xH001234=41")] // modifier is extracted and comparison is normalized
        [TestCase("prev((byte(0x1234) - 1) / 4) < 10", "d0xH001234<41")] // modifier is extracted and comparison is normalized
        [TestCase("prev(byte(0x1234) * 10 + 20) == 80", "d0xH001234=6")] // modifier is extracted and comparison is normalized
        [TestCase("prev(byte(0x1234) + byte(0x2345)) == 7", "A:d0xH001234=0_d0xH002345=7")] // prev is distributed
        [TestCase("prev(byte(0x1234) - byte(0x2345)) == 7", "B:d0xH002345=0_d0xH001234=7")] // prev is distributed
        public void TestPrev(string input, string expected)
        {
            var definition = Process(input);
            Assert.That(definition, Is.EqualTo(expected));
        }

        [Test]
        public void TestPrevMalformed()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, prev(byte(0x1234) == 1))", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("1:31 accessor: Cannot convert requirement to memory accessor"));
        }

        [Test]
        public void TestPrevBCD()
        {
            // bcd() can be factored out
            var definition = Process("prev(bcd(byte(0x1234))) == 10");
            Assert.That(definition, Is.EqualTo("d0xH001234=16"));

            // bcd cannot be factored out
            var parser = Parse("achievement(\"T\", \"D\", 5, prev(bcd(byte(0x1234))) != byte(0x1234))", false);
            Assert.That(GetInnerErrorMessage(parser), Is.EqualTo("1:26 cannot apply multiple modifiers to memory accessor"));
        }

        [Test]
        public void TestPrevLargeMathematicChain()
        {
            var largeInput = new StringBuilder();
            var largeOutput = new StringBuilder();

            largeInput.Append("prev(");
            for (int i = 0; i < 100; i++)
            {
                largeInput.AppendFormat("byte(0x{0:X4}) +", i + 0x1000);
                largeOutput.AppendFormat("A:d0xH{0:x6}=0_", i + 0x1000);
            }

            largeInput.Append("byte(0x1234)) == 87");
            largeOutput.Append("d0xH001234=87");

            var definition = Process(largeInput.ToString());
            Assert.That(definition, Is.EqualTo(largeOutput.ToString()));
        }
    }
}
