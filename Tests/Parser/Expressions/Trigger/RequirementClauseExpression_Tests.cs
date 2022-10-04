using NUnit.Framework;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Tests.Parser.Expressions.Trigger
{
    [TestFixture]
    class RequirementClauseExpressionTests
    {
        [Test]
        [TestCase("byte(0x001234) == 3")]
        [TestCase("low4(word(0x001234)) < 5")]
        [TestCase("word(dword(0x002345) + 4660) >= 500")]
        [TestCase("byte(0x001234) > prev(byte(0x001234))")]
        [TestCase("once(dword(0x001234) <= 10000)")]
        [TestCase("repeated(6, word(0x001234) > 100)")]
        public void TestAppendString(string input)
        {
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);
            ExpressionTests.AssertAppendString(clause, input);
        }
        
        [Test]
        [TestCase("byte(0x001234) == 3", "0xH001234=3")]
        [TestCase("low4(word(0x001234)) < 5", "I:0x 001234_0xL000000<5")]
        [TestCase("word(dword(0x002345) + 4660) >= 500", "I:0xX002345_0x 001234>=500")]
        [TestCase("byte(0x001234) > prev(byte(0x001234))", "0xH001234>d0xH001234")]
        [TestCase("low4(word(0x001234)) > prior(low4(word(0x001234)))", "I:0x 001234_0xL000000>p0xL000000")]
        [TestCase("once(dword(0x001234) <= 10000)", "0xX001234<=10000.1.")]
        [TestCase("repeated(6, word(0x001234) > 100)", "0x 001234>100.6.")]
        [TestCase("byte(0x001234) * byte(0x002345) == 3", "A:0xH001234*0xH002345_0=3")]
        [TestCase("byte(0x001234) / byte(0x002345) == 3", "A:0xH001234/0xH002345_0=3")]
        [TestCase("byte(0x001234) & 7 == 3", "A:0xH001234&7_0=3")]
        [TestCase("float(0x001234) == 3.14", "fF001234=f3.14")]
        [TestCase("float(0x001234) > 2.0", "fF001234>f2.0")]
        [TestCase("float(0x001234) != 2", "fF001234!=2")]
        [TestCase("float(0x001234) < 0", "fF001234<0")]
        public void TestBuildTrigger(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);
            TriggerExpressionTests.AssertSerialize(clause, expected);
        }

        [Test]
        // bcd should be factored out
        [TestCase("bcd(byte(1)) == 24", ExpressionType.RequirementClause, "byte(0x000001) == 36")]
        [TestCase("prev(bcd(byte(1))) == 150", ExpressionType.RequirementClause, "prev(byte(0x000001)) == 336")]
        [TestCase("bcd(byte(1)) != prev(bcd(byte(1)))", ExpressionType.RequirementClause, "byte(0x000001) != prev(byte(0x000001))")]
        // bcd cannot be factored out
        [TestCase("byte(1) != bcd(byte(2))", ExpressionType.RequirementClause, "byte(0x000001) != bcd(byte(0x000002))")]
        // bcd representation of 100M doesn't fit in 32-bits
        [TestCase("bcd(dword(1)) == 100000000", ExpressionType.BooleanConstant, "false")]
        [TestCase("bcd(dword(1)) != 100000000", ExpressionType.BooleanConstant, "true")]
        [TestCase("bcd(dword(1)) < 100000000", ExpressionType.BooleanConstant, "true")]
        [TestCase("bcd(dword(1)) <= 100000000", ExpressionType.BooleanConstant, "true")]
        [TestCase("bcd(dword(1)) > 100000000", ExpressionType.BooleanConstant, "false")]
        [TestCase("bcd(dword(1)) >= 100000000", ExpressionType.BooleanConstant, "false")]
        public void TestNormalizeBCD(string input, ExpressionType expectedType, string expected)
        {
            var result = TriggerExpressionTests.Parse(input);
            Assert.That(result.Type, Is.EqualTo(expectedType));

            if (expectedType == ExpressionType.RequirementClause)
                result = ((RequirementClauseExpression)result).Normalize();

            ExpressionTests.AssertAppendString(result, expected);
        }

        [Test]
        public void TestAddAddressCompareToAddress()
        {
            var input = "byte(word(0x1234)) == word(0x2345)";
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);
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

            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);

            expected = expected.Replace("WA", "word(0x001234)");
            expected = expected.Replace("WB", "word(0x002345)");
            ExpressionTests.AssertAppendString(clause, expected);
        }
    }
}
