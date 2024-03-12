using Jamiras.Core.Tests;
using NUnit.Framework;
using RATools.Data;
using RATools.Data.Tests;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using RATools.Parser.Tests.Expressions.Trigger;

namespace RATools.Parser.Tests.Internal
{
    [TestFixture]
    class ValueBuilderContextTests
    {
        [Test]
        [TestCase("0", "v0")]
        [TestCase("1", "v1")]
        [TestCase("1 + 7", "v8")]
        [TestCase("1 + 3 * 2", "v7")]
        [TestCase("byte(0x1234)", "0xH001234")]
        [TestCase("byte(0x1234) * 10", "0xH001234*10")]
        [TestCase("byte(0x1234) * -10", "0xH001234*-10")]
        [TestCase("byte(0x1234) / 10", "M:0xH001234/10")]
        [TestCase("byte(0x1234) * 10 / 3", null)] // integer division with remainder is rejected as invalid
        [TestCase("byte(0x1234) * 10.0 / 3", "0xH001234*3.333333")]
        [TestCase("byte(0x1234) * 10 / 3.0", "0xH001234*3.333333")]
        [TestCase("byte(0x1234) + 10", "0xH001234_v10")]
        [TestCase("byte(0x1234) - 10", "0xH001234_v-10")]
        [TestCase("10 - byte(0x1234)", "0xH001234*-1_v10")]
        [TestCase("(byte(0) + byte(1)) * 10", "0xH000000*10_0xH000001*10")]
        [TestCase("(byte(0) + 2) * 10", "0xH000000*10_v20")]
        [TestCase("(byte(0) + byte(1)) / 10", "A:0xH000000/10_M:0xH000001/10")]
        [TestCase("byte(0x1234) * 2", "0xH001234*2")]
        [TestCase("byte(0x1234) / 2", "M:0xH001234/2")]
        [TestCase("byte(0x1234) / 0.5", "0xH001234*2")]
        [TestCase("byte(0x1234) * 100 / 2", "0xH001234*50")]
        [TestCase("byte(0x1234) * 2 / 100", "M:0xH001234/50")]
        [TestCase("byte(0x1234) + 100 - 2", "0xH001234_v98")]
        [TestCase("byte(0x1234) + 1 - 1", "0xH001234")]
        [TestCase("byte(0x1234) * 2 + 1", "0xH001234*2_v1")]
        [TestCase("byte(0x1234) * 2 - 1", "0xH001234*2_v-1")]
        [TestCase("byte(0x1234) * 256 + byte(0x2345) + 1", "0xH001234*256_0xH002345_v1")]
        [TestCase("(byte(0x1234) / (2 * 20)) * 100.0", "0xH001234*2.5")]
        [TestCase("byte(0x1234) + byte(0x1235)", "0xH001234_0xH001235")]
        [TestCase("byte(0x1234) + byte(0x1235) * 10", "0xH001234_0xH001235*10")]
        [TestCase("byte(0x1234) - byte(0x1235)", "0xH001234_0xH001235*-1")]
        [TestCase("byte(0x1234) * byte(0x2345)", "M:0xH001234*0xH002345")]
        [TestCase("byte(0x1234) / byte(0x2345)", "M:0xH001234/0xH002345")]
        [TestCase("byte(0x1234 + byte(0x2345))", "I:0xH002345_M:0xH001234")]
        [TestCase("byte(0x1234 + byte(0x2345)) + 1", "I:0xH002345_A:0xH001234_M:1")]
        [TestCase("byte(0x1234 + byte(0x2345)) + byte(0x1235 + byte(0x2345))", "I:0xH002345_A:0xH001234_I:0xH002345_M:0xH001235")]
        [TestCase("byte(0x1234 + byte(0x2345)) + byte(0x1235 + byte(0x2345)) + 1", "I:0xH002345_A:0xH001234_I:0xH002345_A:0xH001235_M:1")]
        [TestCase("byte(0x1234 + byte(0x2345)) + 1 + byte(0x1235 + byte(0x2345))", "I:0xH002345_A:0xH001234_I:0xH002345_A:0xH001235_M:1")]
        [TestCase("1 + byte(0x1234 + byte(0x2345))", "I:0xH002345_A:0xH001234_M:1")]
        [TestCase("byte(0x1234 + byte(0x2345)) - 1", "B:1_I:0xH002345_M:0xH001234")]
        [TestCase("byte(0x1234 + byte(0x2345)) - byte(0x1235 + byte(0x2345))", "I:0xH002345_B:0xH001235_I:0xH002345_M:0xH001234")]
        [TestCase("byte(0x1234 + byte(0x2345)) * 2", "I:0xH002345_M:0xH001234*2")]
        [TestCase("byte(0x1234 + byte(0x2345)) / 2", "I:0xH002345_M:0xH001234/2")]
        [TestCase("measured(tally(0, dword(0x1234) == 0xFFFFFFFF))", "M:0xX001234=4294967295")]
        [TestCase("measured(byte(0x1234 + byte(0x2345)) / 2)", "I:0xH002345_M:0xH001234/2")]
        [TestCase("measured(tally(0, byte(0x1234) != prev(byte(0x1234))))", "M:0xH001234!=d0xH001234")]
        [TestCase("measured(tally(0, byte(0x1234) != prev(byte(0x1234)))) && never(byte(0x2345) == 1)", "M:0xH001234!=d0xH001234_R:0xH002345=1")]
        [TestCase("tally(20, byte(0x1234) != prev(byte(0x1234))) && never(byte(0x2345) == 1)", "M:0xH001234!=d0xH001234.20._R:0xH002345=1")]
        [TestCase("byte(byte(0x1234) - 10)", "I:0xH001234_M:0xHfffffff6")]
        [TestCase("measured(repeated(10, byte(0x2345 + word(0x1234) * 4) == 6)))", "I:0x 001234*4_M:0xH002345=6.10.")]
        [TestCase("prev(byte(0x1234))", "d0xH001234")]
        [TestCase("bcd(byte(0x1234))", "b0xH001234")]
        [TestCase("measured(dword(0x1234) - dword(0x2345))", "B:0xX002345_M:0xX001234")]
        public void TestGetValueString(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse(input);

            ExpressionBase error;
            InterpreterScope scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            var serialized = ValueBuilderContext.GetValueString(clause, scope, new SerializationContext(), out error);
            Assert.That(serialized, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("byte(0x1234) * 2.5", "0xH001234*2.5")]
        [TestCase("byte(0x1234) * 10.0 / 3", "0xH001234*3.333333")]
        [TestCase("(byte(0) + byte(1)) / 1.5", "0xH000000*0.666667_0xH000001*0.666667")]
        [TestCase("byte(0x1234) * 2.0", "0xH001234*2")]
        [TestCase("byte(0x1234) / 2.0", "0xH001234*0.5")]
        [TestCase("(byte(0x1234) / (2 * 20)) * 100.0", "0xH001234*2.5")]
        [TestCase("measured(byte(0x1234 + byte(0x2345)) / 2.0)", "I:0xH002345_M:0xH001234/f2.0")]
        [TestCase("measured(byte(0x1234 + byte(0x2345)) / 2.5)", "I:0xH002345_M:0xH001234/f2.5")]
        public void TestGetValueStringCulture(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse(input);

            using (var cultureOverride = new CultureOverride("fr-FR"))
            {
                ExpressionBase error;
                InterpreterScope scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
                var serialized = ValueBuilderContext.GetValueString(clause, scope, new SerializationContext(), out error);
                Assert.That(serialized, Is.EqualTo(expected));
            }
        }
    }
}
