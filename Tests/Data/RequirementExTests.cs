using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using System.Text;

namespace RATools.Test.Data
{
    [TestFixture]
    class RequirementExTests
    {
        [Test]
        [TestCase("0xH001234=1", "byte(0x001234) == 1")]
        [TestCase("P:0xH001234=1", "unless(byte(0x001234) == 1)")]
        [TestCase("R:0xH001234=1", "never(byte(0x001234) == 1)")]
        [TestCase("0xH001234=1.1.", "once(byte(0x001234) == 1)")]
        [TestCase("0xH001234=1.99.", "repeated(99, byte(0x001234) == 1)")]
        [TestCase("0xH001234=1_0xH002345=2", "byte(0x001234) == 1|byte(0x002345) == 2")]
        [TestCase("A:0xH001234=0_0xH002345=2", "(byte(0x001234) + byte(0x002345)) == 2")]
        [TestCase("A:0xH001234=0_R:0xH002345=2", "never((byte(0x001234) + byte(0x002345)) == 2)")]
        [TestCase("I:0x 001234_0xH002345=2", "byte(word(0x001234) + 0x002345) == 2")]
        [TestCase("O:0xH001234=1_0xH002345=2", "(byte(0x001234) == 1 || byte(0x002345) == 2)")]
        [TestCase("O:0xH001234=1_0=1", "byte(0x001234) == 1")]
        [TestCase("O:0=1_0xH002345=2", "byte(0x002345) == 2")]
        [TestCase("O:0xH001234=1_0=1.15.", "repeated(15, byte(0x001234) == 1)")]
        [TestCase("O:0=1_0xH002345=2.15.", "repeated(15, byte(0x002345) == 2)")]
        [TestCase("O:0=1.12._0xH002345=2.15.", "repeated(15, byte(0x002345) == 2)")]
        [TestCase("O:0xH001234=1.15._0=1", "repeated(15, byte(0x001234) == 1)")]
        [TestCase("O:0=1.15._0xH002345=2", "byte(0x002345) == 2")]
        [TestCase("N:0xH001234=1_0xH002345=2", "(byte(0x001234) == 1 && byte(0x002345) == 2)")]
        [TestCase("N:0xH001234=1_1=1", "byte(0x001234) == 1")]
        [TestCase("N:1=1_0xH002345=2", "byte(0x002345) == 2")]
        [TestCase("N:0xH001234=1_1=1.15.", "repeated(15, byte(0x001234) == 1)")]
        [TestCase("N:1=1_0xH002345=2.15.", "repeated(15, byte(0x002345) == 2)")]
        [TestCase("N:0xH001234=1.15._1=1", "repeated(15, byte(0x001234) == 1)")]
        [TestCase("N:1=1.15._0xH002345=2", "(repeated(15, always_true()) && byte(0x002345) == 2)")]
        [TestCase("M:0xH001234>=100", "measured(byte(0x001234) >= 100)")]
        [TestCase("Z:0xH002345=2_0xH001234=1.2.", "repeated(2, byte(0x001234) == 1 && never(byte(0x002345) == 2))")]
        [TestCase("Z:0xH002345=2_P:0xH001234=1.1.", "disable_when(byte(0x001234) == 1, until=byte(0x002345) == 2)")]
        [TestCase("Z:0xH002345=2_P:0xH001234=1.2.", "disable_when(repeated(2, byte(0x001234) == 1), until=byte(0x002345) == 2)")]
        public void TestAppendString(string input, string expected)
        {
            var achievement = new AchievementBuilder();
            achievement.ParseRequirements(Tokenizer.CreateTokenizer(input));
            var groups = RequirementEx.Combine(achievement.CoreRequirements);

            var builder = new StringBuilder();
            foreach (var group in groups)
            {
                if (builder.Length > 0)
                    builder.Append('|');

                group.AppendString(builder, NumberFormat.Decimal);
            }

            Assert.That(builder.ToString(), Is.EqualTo(expected));

            // make sure we didn't modify the source requirements
            Assert.That(achievement.SerializeRequirements(), Is.EqualTo(input));
        }
    }
}
