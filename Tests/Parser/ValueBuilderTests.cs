using Jamiras.Components;
using Jamiras.Core.Tests;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Tests.Expressions.Trigger;

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
        [TestCase("0xH1234_v-1", "(byte(0x001234) - 1)")]
        [TestCase("0xH1234=0_0xH2345", "(byte(0x001234) + byte(0x002345))")]
        [TestCase("0xH1234&7_0xH2345", "(byte(0x001234) & 0x07 + byte(0x002345))")]
        public void TestParseValue(string input, string expected)
        {
            var builder = new ValueBuilder();
            builder.ParseValue(Tokenizer.CreateTokenizer(input));
            Assert.That(builder.RequirementsDebugString, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("0", "v0")]
        [TestCase("1", "v1")]
        [TestCase("-1", "v-1")]
        [TestCase("1 + 7", "v8")]
        [TestCase("1 + 3 * 2", "v7")]
        [TestCase("byte(0x1234)", "0xH001234")]
        [TestCase("byte(0x1234) * 10", "0xH001234*10")]
        [TestCase("byte(0x1234) * -10", "0xH001234*-10")]
        [TestCase("byte(0x1234) / 10", "0xH001234/10")]
        [TestCase("byte(0x1234) * 10 / 3", "Cannot create value from mathematic expression")] // integer division with remainder is rejected as invalid
        [TestCase("byte(0x1234) * 10.0 / 3", "0xH001234*3.333333")]
        [TestCase("byte(0x1234) * 10 / 3.0", "0xH001234*3.333333")]
        [TestCase("byte(0x1234) + 10", "0xH001234_v10")]
        [TestCase("byte(0x1234) - 10", "0xH001234_v-10")]
        [TestCase("10 - byte(0x1234)", "0xH001234*-1_v10")]
        [TestCase("(byte(0) + byte(1)) * 10", "0xH000000*10_0xH000001*10")]
        [TestCase("(byte(0) + 2) * 10", "0xH000000*10_v20")]
        [TestCase("(byte(0) + byte(1)) / 10", "0xH000000/10_0xH000001/10")]
        [TestCase("byte(0x1234) * 2", "0xH001234*2")]
        [TestCase("byte(0x1234) / 2", "0xH001234/2")]
        [TestCase("byte(0x1234) / 0.5", "0xH001234*2")]
        [TestCase("byte(0x1234) * 100 / 2", "0xH001234*50")]
        [TestCase("byte(0x1234) * 2 / 100", "0xH001234/50")]
        [TestCase("byte(0x1234) + 100 - 2", "0xH001234_v98")]
        [TestCase("byte(0x1234) + 1 - 1", "0xH001234")]
        [TestCase("byte(0x1234) * 2 + 1", "0xH001234*2_v1")]
        [TestCase("byte(0x1234) * 2 - 1", "0xH001234*2_v-1")]
        [TestCase("byte(0x1234) * 256 + byte(0x2345) + 1", "0xH001234*256_0xH002345_v1")]
        [TestCase("(byte(0x1234) / (2 * 20)) * 100.0", "0xH001234*2.5")]
        [TestCase("byte(0x1234) + byte(0x1235)", "0xH001234_0xH001235")]
        [TestCase("byte(0x1234) + byte(0x1235) * 10", "0xH001235*10_0xH001234")]
        [TestCase("byte(0x1234) - byte(0x1235)", "0xH001235*-1_0xH001234")]
        [TestCase("byte(0x1234) * byte(0x2345)", "A:0xH001234*0xH002345_M:0")]
        [TestCase("byte(0x1234) / byte(0x2345)", "A:0xH001234/0xH002345_M:0")]
        [TestCase("byte(0x1234 + byte(0x2345))", "I:0xH002345_M:0xH001234")]
        [TestCase("byte(0x1234 + byte(0x2345)) + 1", "I:0xH002345_A:0xH001234_M:1")]
        [TestCase("byte(0x1234 + byte(0x2345)) + byte(0x1235 + byte(0x2345))", "I:0xH002345_A:0xH001234_I:0xH002345_M:0xH001235")]
        [TestCase("byte(0x1234 + byte(0x2345)) + byte(0x1235 + byte(0x2345)) + 1", "I:0xH002345_A:0xH001234_I:0xH002345_A:0xH001235_M:1")]
        [TestCase("byte(0x1234 + byte(0x2345)) + 1 + byte(0x1235 + byte(0x2345))", "I:0xH002345_A:0xH001234_I:0xH002345_A:0xH001235_M:1")]
        [TestCase("1 + byte(0x1234 + byte(0x2345))", "I:0xH002345_A:0xH001234_M:1")]
        [TestCase("byte(0x1234 + byte(0x2345)) - 1", "B:1_I:0xH002345_M:0xH001234")]
        [TestCase("byte(0x1234 + byte(0x2345)) - byte(0x1235 + byte(0x2345))", "I:0xH002345_B:0xH001235_I:0xH002345_M:0xH001234")]
        [TestCase("byte(0x1234 + byte(0x2345)) * 2", "I:0xH002345_A:0xH001234*2_M:0")]
        [TestCase("byte(0x1234 + byte(0x2345)) / 2", "I:0xH002345_A:0xH001234/2_M:0")]
        [TestCase("measured(tally(0, dword(0x1234) == 0xFFFFFFFF))", "M:0xX001234=4294967295")]
        [TestCase("measured(byte(0x1234 + byte(0x2345)) / 2)", "I:0xH002345_A:0xH001234/2_M:0")]
        [TestCase("measured(tally(0, byte(0x1234) != prev(byte(0x1234))))", "M:0xH001234!=d0xH001234")]
        [TestCase("measured(tally(0, byte(0x1234) != prev(byte(0x1234)))) && never(byte(0x2345) == 1)", "M:0xH001234!=d0xH001234_R:0xH002345=1")]
        [TestCase("tally(20, byte(0x1234) != prev(byte(0x1234))) && never(byte(0x2345) == 1)", "M:0xH001234!=d0xH001234.20._R:0xH002345=1")]
        [TestCase("byte(byte(0x1234) - 10)", "I:0xH001234_M:0xHfffffff6")]
        [TestCase("measured(repeated(10, byte(0x2345 + word(0x1234) * 4) == 6)))", "I:0x 001234*4_M:0xH002345=6.10.")]
        [TestCase("prev(byte(0x1234))", "d0xH001234")]
        [TestCase("bcd(byte(0x1234))", "b0xH001234")]
        [TestCase("measured(dword(0x1234) - dword(0x2345))", "0xX002345*-1_0xX001234")]
        [TestCase("measured(tally(0, byte(0x10) & 0x02 == 0x02))", "A:0xH000010&2_M:0=2")]
        [TestCase("byte(0x1234) * 10 + byte(0x1235) * 100", "0xH001234*10_0xH001235*100")]
        public void TestGetValueString(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse(input);

            ErrorExpression error;
            var value = ValueBuilder.BuildValue(clause, out error);
            if (value == null)
            {
                Assert.That(error, Is.Not.Null);
                if (error.InnerError != null)
                    Assert.That(error.InnermostError.Message, Is.EqualTo(expected));
                else
                    Assert.That(error.Message, Is.EqualTo(expected));
            }
            else
            {
                var minimumVersion = value.MinimumVersion();
                var serialized = value.Serialize(new SerializationContext { MinimumVersion = minimumVersion });
                Assert.That(serialized, Is.EqualTo(expected));
            }
        }

        [Test]
        [TestCase("0", "v0")]
        [TestCase("1", "v1")]
        [TestCase("-1", "v-1")]
        [TestCase("byte(0x1234)", "M:0xH001234")]
        [TestCase("byte(0x1234) * 10", "A:0xH001234*10_M:0")]
        [TestCase("byte(0x1234) * -10", "A:0xH001234*4294967286_M:0")]
        [TestCase("byte(0x1234) / 10", "A:0xH001234/10_M:0")]
        [TestCase("byte(0x1234) * 10.0 / 3", "A:0xH001234*f3.333333_M:0")]
        [TestCase("byte(0x1234) + 10", "A:0xH001234_M:10")]
        [TestCase("byte(0x1234) - 10", "B:10_M:0xH001234")]
        [TestCase("10 + byte(0x1234)", "A:0xH001234_M:10")]
        [TestCase("10 - byte(0x1234)", "B:0xH001234_M:10")]
        [TestCase("byte(0x1234) * 2", "A:0xH001234*2_M:0")]
        [TestCase("byte(0x1234) / 2", "A:0xH001234/2_M:0")]
        [TestCase("byte(0x1234) + byte(0x1235)", "A:0xH001234_M:0xH001235")]
        [TestCase("byte(0x1234) - byte(0x1235)", "B:0xH001235_M:0xH001234")]
        [TestCase("measured(tally(0, byte(0x1234) != prev(byte(0x1234))))", "M:0xH001234!=d0xH001234")]
        [TestCase("prev(byte(0x1234))", "M:d0xH001234")]
        [TestCase("bcd(byte(0x1234))", "M:b0xH001234")]
        public void TestGetValueStringMeasured(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse(input);

            ErrorExpression error;
            var value = ValueBuilder.BuildValue(clause, out error);
            var serialized = value.Serialize(new SerializationContext {  MinimumVersion = Version._1_0 });
            Assert.That(serialized, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("byte(0x1234) * 2.5", "0xH001234*2.5")]
        [TestCase("byte(0x1234) * 10.0 / 3", "0xH001234*3.333333")]
        [TestCase("(byte(0) + byte(1)) / 1.5", "0xH000000/1.5_0xH000001/1.5")]
        [TestCase("byte(0x1234) * 2.0", "0xH001234*2")]
        [TestCase("byte(0x1234) / 2.0", "0xH001234/2")]
        [TestCase("(byte(0x1234) / (2 * 20)) * 100.0", "0xH001234*2.5")]
        [TestCase("measured(byte(0x1234 + byte(0x2345)) / 2.0)", "I:0xH002345_A:0xH001234/f2.0_M:0")]
        [TestCase("measured(byte(0x1234 + byte(0x2345)) / 2.5)", "I:0xH002345_A:0xH001234/f2.5_M:0")]
        public void TestGetValueStringCulture(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse(input);

            using (var cultureOverride = new CultureOverride("fr-FR"))
            {
                ErrorExpression error;
                var value = ValueBuilder.BuildValue(clause, out error);
                var minimumVersion = value.MinimumVersion();
                var serialized = value.Serialize(new SerializationContext { MinimumVersion = minimumVersion });
                Assert.That(serialized, Is.EqualTo(expected));
            }
        }

    }
}
