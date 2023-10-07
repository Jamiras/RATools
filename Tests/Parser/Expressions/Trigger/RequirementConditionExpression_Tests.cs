using NUnit.Framework;
using RATools.Parser.Expressions.Trigger;

namespace RATools.Tests.Parser.Expressions.Trigger
{
    [TestFixture]
    class RequirementConditionExpressionTests
    {
        [Test]
        [TestCase("byte(0x001234) == 3")]
        [TestCase("low4(word(0x001234)) < 5")]
        [TestCase("word(dword(0x002345) + 4660) >= 500")]
        [TestCase("byte(0x001234) > prev(byte(0x001234))")]
        public void TestAppendString(string input)
        {
            var clause = TriggerExpressionTests.Parse<RequirementConditionExpression>(input);
            ExpressionTests.AssertAppendString(clause, input);
        }
        
        [Test]
        [TestCase("byte(0x001234) == 3", "0xH001234=3")]
        [TestCase("3 == byte(0x001234)", "0xH001234=3")] // prefer constants on right
        [TestCase("bit6(0x00627E) == 1", "0xS00627e=1")]
        [TestCase("prev(bit6(0x00627E)) == 0", "d0xS00627e=0")]
        [TestCase("high4(0x00616A) == 8", "0xU00616a=8")]
        [TestCase("word(0x000042) == 5786", "0x 000042=5786")]
        [TestCase("prior(bitcount(0x00627E)) == 0", "p0xK00627e=0")]
        [TestCase("low4(word(0x001234)) < 5", "I:0x 001234_0xL000000<5")]
        [TestCase("word(dword(0x002345) + 4660) >= 500", "I:0xX002345_0x 001234>=500")]
        [TestCase("byte(0x001234) > prev(byte(0x001234))", "0xH001234>d0xH001234")]
        [TestCase("low4(word(0x001234)) > prior(low4(word(0x001234)))", "I:0x 001234_0xL000000>p0xL000000")]
        [TestCase("byte(0x001234) * byte(0x002345) == 3", "A:0xH001234*0xH002345_0=3")]
        [TestCase("byte(0x001234) / byte(0x002345) == 3", "A:0xH001234/0xH002345_0=3")]
        [TestCase("byte(0x001234) & 7 == 3", "A:0xH001234&7_0=3")]
        [TestCase("dword(0x1234) == 12345678 * 30 / 60", "0xX001234=6172839")]
        [TestCase("byte(0x1234) * 4 + byte(0x2345) == 6", "A:0xH001234*4_0xH002345=6")]
        [TestCase("byte(0x1234) / 4 + byte(0x2345) == 6", "A:0xH001234/4_0xH002345=6")]
        [TestCase("byte(0x1234) - prev(byte(0x1234)) + byte(0x2345) == 6", "A:0xH001234=0_B:d0xH001234=0_0xH002345=6")]
        [TestCase("byte(word(0x1111)) - prev(byte(word(0x1111))) > 1", "A:1_I:0x 001111_d0xH000000<0xH000000")]
        [TestCase("float(0x1111) == 3.14", "fF001111=f3.14")]
        [TestCase("prev(float(0x1111)) > 3.14", "dfF001111>f3.14")]
        [TestCase("float(0x1111) < prev(float(0x1111))", "fF001111<dfF001111")]
        [TestCase("mbf32(0x2345) == -0.5", "fM002345=f-0.5")]
        [TestCase("byte(0x1234) & 7 == 1", "A:0xH001234&7_0=1")]
        [TestCase("word(byte(0x1234) & 7) == 1", "I:0xH001234&7_0x 000000=1")]
        [TestCase("word((byte(0x1234) & 7) + 17) == 1", "I:0xH001234&7_0x 000011=1")]
        [TestCase("dword((dword(0x12345) & 0x1ffffff) + 0xff7f6255) == 60", "I:0xX012345&33554431_0xXff7f6255=60")]
        [TestCase("byte(0x001234) * 100 + byte(0x1235) > prev(byte(0x002345))", "A:0xH001234*100_0xH001235>d0xH002345")]
        [TestCase("byte(0x001234) * 100 + byte(0x1235) > prev(byte(0x002345)) * 100", "A:25500_A:0xH001234*100_B:d0xH002345*100_0xH001235>25500")]
        [TestCase("byte(0x001234) * 100 + byte(0x1235) > prev(byte(0x002345)) * 100 + prev(byte(0x2346))", "A:25755_A:0xH001234*100_B:d0xH002345*100_B:d0xH002346_0xH001235>25755")]
        [TestCase("low4(0x001234) * 10000000 + byte(0x1235) > prev(low4(0x002345)) * 10000000 + prev(byte(0x2346))", "A:150000255_A:0xL001234*10000000_B:d0xL002345*10000000_B:d0xH002346_0xH001235>150000255")]
        [TestCase("byte(0x001234) * 10000000 + byte(0x1235) > prev(byte(0x002345)) * 10000000 + prev(byte(0x2346))", "A:0xH001234*10000000_B:d0xH002345*10000000_0xH001235>d0xH002346")] // underflow adjustment exceeds MAX_INT, don't apply one
        [TestCase("byte(0x001234) * 10000000 + byte(0x1235) != prev(byte(0x002345)) * 10000000 + prev(byte(0x2346))", "A:0xH001234*10000000_B:d0xH002345*10000000_0xH001235!=d0xH002346")] // don't need underflow adjustment for inequality
        [TestCase("low4(0x001234) * 10000000 + byte(0x1235) != prev(low4(0x002345)) * 10000000 + prev(byte(0x2346))", "A:0xL001234*10000000_B:d0xL002345*10000000_0xH001235!=d0xH002346")] // don't need underflow adjustment for inequality
        [TestCase("dword(dword(0x001234) + 8) - dword(dword(0x001234) + 12) > 100000", "A:100000_I:0xX001234_0xX00000c<0xX000008")] // AddAddress can be shared
        [TestCase("dword(dword(0x001234) + 8) - dword(dword(0x001234) + 12) * 4 > 100000", "I:0xX001234_B:0xX00000c*4_I:0xX001234_0xX000008>100000")] // AddAddress cannot be shared
        [TestCase("word(0x18294A) * 10 - word(0x182946) * 8 < 0x80000000", "A:0x 18294a*10_B:0x 182946*8_0<2147483648")]
        [TestCase("byte(0x1234) - byte(0x2345) == 1", "B:0xH002345=0_0xH001234=1")]
        [TestCase("byte(0x1234) - byte(0x2345) == -1", "B:0xH001234=0_0xH002345=1")] // invert to eliminate negative value
        [TestCase("byte(0x1234) - byte(0x2345) == 4294967295", "B:0xH002345=0_0xH001234=4294967295")] // don't invert very high positive value
        [TestCase("byte(0x1234) - byte(0x2345) <= -1", "A:1=0_0xH001234<=0xH002345")]
        [TestCase("dword(0x1234) - prev(dword(0x1234)) >= 0x1000", "A:4096=0_d0xX001234<=0xX001234")]
        public void TestBuildTrigger(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<RequirementConditionExpression>(input);
            TriggerExpressionTests.AssertSerialize(clause, expected);
        }

        [Test]
        [TestCase("byte(0x001234) == 0", "byte(0x001234) == 0")]
        [TestCase("byte(0x001234) != 0","byte(0x001234) != 0")]
        [TestCase("byte(0x001234) < 0", "always_false()")]
        [TestCase("byte(0x001234) <= 0", "byte(0x001234) == 0")]
        [TestCase("byte(0x001234) > 0", "byte(0x001234) > 0")]
        [TestCase("byte(0x001234) >= 0", "always_true()")]
        [TestCase("byte(0x001234) == 255", "byte(0x001234) == 255")]
        [TestCase("byte(0x001234) != 255", "byte(0x001234) != 255")]
        [TestCase("byte(0x001234) < 255", "byte(0x001234) < 255")]
        [TestCase("byte(0x001234) <= 255", "always_true()")]
        [TestCase("byte(0x001234) > 255", "always_false()")]
        [TestCase("byte(0x001234) >= 255", "byte(0x001234) == 255")]
        [TestCase("word(0x001234) == 65535", "word(0x001234) == 65535")]
        [TestCase("word(0x001234) != 65535", "word(0x001234) != 65535")]
        [TestCase("word(0x001234) < 65535", "word(0x001234) < 65535")]
        [TestCase("word(0x001234) <= 65535", "always_true()")]
        [TestCase("word(0x001234) > 65535", "always_false()")]
        [TestCase("word(0x001234) >= 65535", "word(0x001234) == 65535")]
        [TestCase("tbyte(0x001234) == 16777215", "tbyte(0x001234) == 16777215")]
        [TestCase("tbyte(0x001234) != 16777215", "tbyte(0x001234) != 16777215")]
        [TestCase("tbyte(0x001234) < 16777215", "tbyte(0x001234) < 16777215")]
        [TestCase("tbyte(0x001234) <= 16777215", "always_true()")]
        [TestCase("tbyte(0x001234) > 16777215", "always_false()")]
        [TestCase("tbyte(0x001234) >= 16777215", "tbyte(0x001234) == 16777215")]
        [TestCase("dword(0x001234) == 4294967295", "dword(0x001234) == 4294967295U")]
        [TestCase("dword(0x001234) != 4294967295", "dword(0x001234) != 4294967295U")]
        [TestCase("dword(0x001234) < 4294967295", "dword(0x001234) < 4294967295U")]
        [TestCase("dword(0x001234) <= 4294967295", "always_true()")]
        [TestCase("dword(0x001234) > 4294967295", "always_false()")]
        [TestCase("dword(0x001234) >= 4294967295", "dword(0x001234) == 4294967295U")]
        [TestCase("bit3(0x001234) == 0", "bit3(0x001234) == 0")]
        [TestCase("bit3(0x001234) != 0", "bit3(0x001234) == 1")]
        [TestCase("bit3(0x001234) < 0", "always_false()")]
        [TestCase("bit3(0x001234) <= 0", "bit3(0x001234) == 0")]
        [TestCase("bit3(0x001234) > 0", "bit3(0x001234) == 1")]
        [TestCase("bit3(0x001234) >= 0", "always_true()")]
        [TestCase("bit5(0x001234) == 1", "bit5(0x001234) == 1")]
        [TestCase("bit5(0x001234) != 1", "bit5(0x001234) == 0")]
        [TestCase("bit5(0x001234) < 1", "bit5(0x001234) == 0")]
        [TestCase("bit5(0x001234) <= 1", "always_true()")]
        [TestCase("bit5(0x001234) > 1", "always_false()")]
        [TestCase("bit5(0x001234) >= 1", "bit5(0x001234) == 1")]
        public void TestNormalizeLimits(string input, string expected)
        {
            var result = TriggerExpressionTests.Parse(input);

            var condition = result as RequirementConditionExpression;
            if (condition != null)
                result = condition.Normalize();

            ExpressionTests.AssertAppendString(result, expected);
        }

        [Test]
        // bcd should be factored out
        [TestCase("bcd(byte(1)) == 24", "byte(0x000001) == 36")]
        [TestCase("prev(bcd(byte(1))) == 15", "prev(byte(0x000001)) == 21")]
        [TestCase("bcd(byte(1)) != prev(bcd(byte(1)))", "byte(0x000001) != prev(byte(0x000001))")]
        // bcd cannot be factored out
        [TestCase("byte(1) != bcd(byte(2))", "byte(0x000001) != bcd(byte(0x000002))")]
        // bcd representation of 100M doesn't fit in 32-bits
        [TestCase("bcd(dword(1)) == 100000000", "always_false()")]
        [TestCase("bcd(dword(1)) != 100000000", "always_true()")]
        [TestCase("bcd(dword(1)) < 100000000", "always_true()")]
        [TestCase("bcd(dword(1)) <= 100000000", "always_true()")]
        [TestCase("bcd(dword(1)) > 100000000", "always_false()")]
        [TestCase("bcd(dword(1)) >= 100000000", "always_false()")]
        [TestCase("prev(bcd(byte(1)) - 1) >= 12", "prev(byte(0x000001)) >= 19")]
        [TestCase("prev(bcd(byte(1)) - 1) == 14", "prev(byte(0x000001)) == 21")]
        [TestCase("prev(bcd(byte(1)) - 1) == 19", "prev(byte(0x000001)) == 32")]
        public void TestNormalizeBCD(string input, string expected)
        {
            var result = TriggerExpressionTests.Parse(input);

            var condition = result as RequirementConditionExpression;
            if (condition != null)
                result = condition.Normalize();

            ExpressionTests.AssertAppendString(result, expected);
        }

        [Test]
        // invert should be factored out
        [TestCase("~byte(1) == 24", "byte(0x000001) == 231")]
        [TestCase("~bit0(1) == 1", "bit0(0x000001) == 0")]
        [TestCase("~word(1) == 1234", "word(0x000001) == 64301")]
        [TestCase("~dword(1) == 1234567890", "dword(0x000001) == 3060399405")]
        [TestCase("prev(~byte(1)) == 15", "prev(byte(0x000001)) == 240")]
        [TestCase("~prev(byte(1)) == 15", "prev(byte(0x000001)) == 240")]
        [TestCase("~byte(1) != prev(~byte(1))", "byte(0x000001) != prev(byte(0x000001))")]
        [TestCase("byte(1) & ~0x3F == 0x80", "byte(0x000001) & 0x000000C0 == 128")]
        // invert cannot be factored out
        [TestCase("~byte(1) != byte(2)", "~byte(0x000001) != byte(0x000002)")]
        [TestCase("byte(1) != ~byte(2)", "byte(0x000001) != ~byte(0x000002)")]
        public void TestNormalizeInvert(string input, string expected)
        {
            var result = TriggerExpressionTests.Parse(input);

            var condition = result as RequirementConditionExpression;
            if (condition != null)
                result = condition.Normalize();

            ExpressionTests.AssertAppendString(result, expected);
        }

        [Test]
        public void TestAddAddressCompareToAddress()
        {
            var input = "byte(word(0x1234)) == word(0x2345)";
            var clause = TriggerExpressionTests.Parse<RequirementConditionExpression>(input);
            ExpressionTests.AssertAppendString(clause, "byte(word(0x001234)) == word(0x002345)");
        }

        [Test]
        [TestCase("byte(WA) > prev(byte(WA))", "byte(WA) > prev(byte(WA))")] // simple compare to prev of same address
        [TestCase("byte(WA + 10) > prev(byte(WA + 10))", "byte(WA + 10) > prev(byte(WA + 10))")] // simple compare to prev of same address with offset
        [TestCase("byte(WA + 10) > byte(WA + 11)", "byte(WA + 10) > byte(WA + 11)")] // same base, different offsets
        [TestCase("byte(WA + 10) > byte(WB + 10)", "byte(WA + 10) - byte(WB + 10) + 255 > 255")] // different bases, same offset
        [TestCase("byte(WA) == byte(WB)", "byte(WA) - byte(WB) == 0")] // becomes B-A==0
        [TestCase("byte(WA) != byte(WB)", "byte(WA) - byte(WB) != 0")] // becomes B-A!=0
        [TestCase("byte(WA) > byte(WB)", "byte(WA) - byte(WB) + 255 > 255")] // becomes B-A>M
        [TestCase("byte(WA) >= byte(WB)", "byte(WA) - byte(WB) + 255 >= 255")] // becomes B-A-1>=M
        [TestCase("byte(WA) < byte(WB)", "byte(WA) - byte(WB) + 255 < 255")] // becomes B-A+M>M
        [TestCase("byte(WA) <= byte(WB)", "byte(WA) - byte(WB) + 255 <= 255")] // becomes B-A+M>=M
        [TestCase("byte(WA + 10) + 20 > byte(WB)", "byte(word(0x002345)) - byte(word(0x001234) + 10) + 255 < 275")]
        [TestCase("byte(WA + 10) > byte(WB) - 20", "byte(word(0x002345)) - byte(word(0x001234) + 10) + 255 < 275")]
        [TestCase("byte(WA + 10) - 20 > byte(WB)", "byte(word(0x001234) + 10) - byte(word(0x002345)) + 255 > 275")]
        [TestCase("byte(WA + 10) > byte(WB) + 20", "byte(word(0x001234) + 10) - byte(word(0x002345)) + 255 > 275")]
        [TestCase("word(WA) > word(WB)", "word(WA) - word(WB) + 65535 > 65535")] // different addresses (16-bit)
        [TestCase("byte(WA) > word(WB)", "byte(WA) - word(WB) + 65535 > 65535")] // different sizes and addresses
        [TestCase("word(WA) > byte(WB)", "word(WA) - byte(WB) + 255 > 255")] // different sizes and addresses
        [TestCase("tbyte(WA) > tbyte(WB)", "tbyte(WA) - tbyte(WB) + 16777215 > 16777215")] // different addresses (24-bit)
        [TestCase("byte(WA) > tbyte(WB)", "byte(WA) - tbyte(WB) + 16777215 > 16777215")] // different sizes and addresses
        [TestCase("tbyte(WA) > word(WB)", "tbyte(WA) - word(WB) + 65535 > 65535")] // different sizes and addresses
        [TestCase("byte(WA) > dword(WB)", "byte(WA) - dword(WB) > 0")] // no underflow adjustment for 32-bit reads
        [TestCase("byte(word(WA + 10) + 2) > prev(byte(word(WA + 10) + 2)", // simple compare to prev of same address (double indirect)
            "byte(word(WA + 10) + 2) > prev(byte(word(WA + 10) + 2))")]
        [TestCase("bit(18, WA + 10) > prev(bit(18, WA + 10))", "bit2(WA + 12) > prev(bit2(WA + 12))")] // simple compare to prev of same address with offset
        public void TestAddAddressAcrossCondition(string input, string expected)
        {
            input = input.Replace("WA", "word(0x1234)");
            input = input.Replace("WB", "word(0x2345)");

            var clause = TriggerExpressionTests.Parse<RequirementConditionExpression>(input);

            expected = expected.Replace("WA", "word(0x001234)");
            expected = expected.Replace("WB", "word(0x002345)");
            ExpressionTests.AssertAppendString(clause, expected);
        }

        [Test]
        public void TestAddressOffsetInWrongLocation()
        {
            string input = "prev(byte(0x1234) + 10) == 0";
            // becomes "prev(byte(0x1234)) == -10", which can never be true
            // user meant "prev(byte(0x1234 + 10)) == 0"

            TriggerExpressionTests.Parse<AlwaysFalseExpression>(input);
        }
    }
}
