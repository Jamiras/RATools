using Jamiras.Components;
using NUnit.Framework;

namespace RATools.Parser.Tests
{
    [TestFixture]
    class ValueBuilderTests
    {
        [Test]
        [TestCase("0xS00627e", "bit6(0x00627E)")]
        [TestCase("d0xS00627e", "prev(bit6(0x00627E))")]
        [TestCase("p0xK00627e", "prior(bitcount(0x00627E))")]
        [TestCase("0xS00627e_d0xS00627e", "(bit6(0x00627E) + prev(bit6(0x00627E)))")]
        [TestCase("0xN20770f*6_0xO20770f", "(bit1(0x20770F) * 6 + bit2(0x20770F))")]
        [TestCase("0xN20770f*6_0xO20770f*-1", "(bit1(0x20770F) * 6 - bit2(0x20770F))")]
        [TestCase("0xO20770f*-1_0xN20770f*6", "(bit1(0x20770F) * 6 - bit2(0x20770F))")]
        [TestCase("0xN20770f*6_0xO20770f*-5", "(bit1(0x20770F) * 6 - bit2(0x20770F) * 5)")]
        [TestCase("0xO20770f*-5_0xN20770f*6", "(bit1(0x20770F) * 6 - bit2(0x20770F) * 5)")]
        public void TestParseValue(string input, string expected)
        {
            var builder = new ValueBuilder();
            builder.ParseValue(Tokenizer.CreateTokenizer(input));
            Assert.That(builder.RequirementsDebugString, Is.EqualTo(expected));
        }
    }
}
