using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;

namespace RATools.Parser.Tests.Expressions.Trigger
{
    [TestFixture]
    class MemoryAccessorExpressionTests
    {
        [Test]
        [TestCase("byte(0x001234)")]
        [TestCase("low4(word(0x001234))")]
        [TestCase("word(dword(0x002345) + 4660)")]
        [TestCase("bit6(word(0x002345) * 106 + 4660)")]
        [TestCase("byte(word(dword(0x002345) + 10) + 4660)")]
        [TestCase("word((dword(0x002345) & 0x1FFFFFF) + 4660)")]
        [TestCase("prev(high4(0x001234))")]
        [TestCase("prior(bit0(0x001234))")]
        public void TestAppendString(string input)
        {
            var accessor = TriggerExpressionTests.Parse<MemoryAccessorExpression>(input);
            ExpressionTests.AssertAppendString(accessor, input);
        }

        [Test]
        [TestCase("byte(0x001234)", "0xH001234")]
        [TestCase("low4(word(0x001234))", "I:0x 001234_0xL000000")]
        [TestCase("word(dword(0x002345) + 4660)", "I:0xX002345_0x 001234")]
        [TestCase("bit6(word(0x002345) * 106 + 4660)", "I:0x 002345*106_0xS001234")]
        [TestCase("byte(word(dword(0x002345) + 10) + 4660)", "I:0xX002345_I:0x 00000a_0xH001234")]
        [TestCase("byte(4660 + word(dword(0x002345) + 10))", "I:0xX002345_I:0x 00000a_0xH001234")]
        [TestCase("word((dword(0x002345) & 0x1FFFFFF) + 4660)", "I:0xX002345&33554431_0x 001234")]
        [TestCase("byte(0x001234 + byte(0x2345))", "I:0xH002345_0xH001234")]
        [TestCase("byte(0x001234 - byte(0x2345))", "I:0xH002345*4294967295_0xH001234")]
        [TestCase("byte(0x001234 + byte(0x2345) * 0x10)", "I:0xH002345*16_0xH001234")]
        [TestCase("byte(0x001234 - byte(0x2345) * 0x10)", "I:0xH002345*4294967280_0xH001234")]
        [TestCase("byte(0x001234 + (byte(0x2345) - 1) * 0x10)", "I:0xH002345*16_0xH001224")]
        [TestCase("byte(0x001234 + 0x10 * byte(0x2345))", "I:0xH002345*16_0xH001234")]
        [TestCase("prev(high4(0x001234))", "d0xU001234")]
        [TestCase("prior(bit0(0x001234))", "p0xM001234")]
        public void TestBuildTrigger(string input, string expected)
        {
            var accessor = TriggerExpressionTests.Parse<MemoryAccessorExpression>(input);
            TriggerExpressionTests.AssertSerialize(accessor, expected);
        }

        [Test]
        [TestCase("byte(0x001234)", "+", "2",
            ExpressionType.MemoryAccessor, "byte(0x001234) + 2")]
        [TestCase("byte(0x001234)", "-", "2",
            ExpressionType.MemoryAccessor, "byte(0x001234) - 2")]
        [TestCase("byte(0x001234)", "*", "2",
            ExpressionType.MemoryAccessor, "byte(0x001234) * 2")]
        [TestCase("byte(0x001234)", "/", "2",
            ExpressionType.MemoryAccessor, "byte(0x001234) / 2")]
        [TestCase("byte(0x001234)", "&", "2",
            ExpressionType.MemoryAccessor, "byte(0x001234) & 0x00000002")]
        [TestCase("byte(0x001234)", "%", "2",
            ExpressionType.MemoryAccessor, "byte(0x001234) % 2")]
        [TestCase("byte(0x001234)", "+", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x001234) + byte(0x002345)")]
        [TestCase("byte(0x001234)", "-", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x001234) - byte(0x002345)")]
        [TestCase("byte(0x001234)", "*", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x001234) * byte(0x002345)")]
        [TestCase("byte(0x001234)", "/", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x001234) / byte(0x002345)")]
        [TestCase("byte(0x001234)", "&", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x001234) & byte(0x002345)")]
        [TestCase("byte(0x001234)", "%", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x001234) % byte(0x002345)")]
        [TestCase("float(0x001234)", "&", "10",
            ExpressionType.Error, "Cannot perform bitwise operations on floating point values")]
        [TestCase("float(0x001234)", "^", "10",
            ExpressionType.Error, "Cannot perform bitwise operations on floating point values")]
        [TestCase("byte(0x001234)", "&", "2.5",
            ExpressionType.Error, "Cannot perform bitwise operations on floating point values")]
        [TestCase("byte(0x001234)", "^", "2.5",
            ExpressionType.Error, "Cannot perform bitwise operations on floating point values")]
        [TestCase("byte(0x001234)", "&", "float(0x002345)",
            ExpressionType.Error, "Cannot perform bitwise operations on floating point values")]
        [TestCase("byte(0x001234)", "^", "float(0x002345)",
            ExpressionType.Error, "Cannot perform bitwise operations on floating point values")]
        [TestCase("dword(0x001234)", "&", "0xFFFFFFFF",
            ExpressionType.MemoryAccessor, "dword(0x001234)")]
        [TestCase("dword(0x001234)", "&", "0x7FFFFFFF",
            ExpressionType.MemoryAccessor, "dword(0x001234) & 0x7FFFFFFF")]
        [TestCase("dword(0x001234)", "&", "0x00FFFFFF",
            ExpressionType.MemoryAccessor, "tbyte(0x001234)")]
        [TestCase("dword(0x001234)", "&", "0x000FFFFF",
            ExpressionType.MemoryAccessor, "tbyte(0x001234) & 0x000FFFFF")]
        [TestCase("dword(0x001234)", "&", "0x0000FFFF",
            ExpressionType.MemoryAccessor, "word(0x001234)")]
        [TestCase("dword(0x001234)", "&", "0x00000FFF",
            ExpressionType.MemoryAccessor, "word(0x001234) & 0x00000FFF")]
        [TestCase("dword(0x001234)", "&", "0x000000FF",
            ExpressionType.MemoryAccessor, "byte(0x001234)")]
        [TestCase("dword(0x001234)", "&", "0x0000000F",
            ExpressionType.MemoryAccessor, "low4(0x001234)")]
        [TestCase("dword(0x001234)", "&", "0x00000001",
            ExpressionType.MemoryAccessor, "bit0(0x001234)")]
        [TestCase("byte(0x001234)", "&", "0x0000FFFF",
            ExpressionType.MemoryAccessor, "byte(0x001234)")]
        [TestCase("byte(0x001234)", "&", "0x000001FF",
            ExpressionType.MemoryAccessor, "byte(0x001234)")]
        [TestCase("byte(0x001234)", "&", "0x000000FF",
            ExpressionType.MemoryAccessor, "byte(0x001234)")]
        [TestCase("byte(0x001234)", "&", "0x000000F7",
            ExpressionType.MemoryAccessor, "byte(0x001234) & 0x000000F7")]
        [TestCase("byte(0x001234)", "&", "0x0000007F",
            ExpressionType.MemoryAccessor, "byte(0x001234) & 0x0000007F")]
        public void TestCombine(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            // MemoryAccessorExpression.Combine just converts to a ModifiedMemoryAccessor and
            // calls its Combine method. These tests are mostly redundant.
            ExpressionTests.AssertCombine(left, operation, right, expectedType, expected);
        }

        [Test]
        [TestCase("2", "+", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x002345) + 2")]
        [TestCase("2", "-", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "- byte(0x002345) + 2")]
        [TestCase("2", "*", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x002345) * 2")]
        [TestCase("2", "/", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "2 / byte(0x002345)")]
        [TestCase("2", "&", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x002345) & 0x00000002")]
        [TestCase("2", "%", "byte(0x002345)",
            ExpressionType.Error, "Cannot modulus using a runtime value")]
        [TestCase("byte(0x001234)", "+", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x001234) + byte(0x002345)")]
        [TestCase("byte(0x001234)", "-", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x001234) - byte(0x002345)")]
        [TestCase("byte(0x001234)", "*", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x001234) * byte(0x002345)")]
        [TestCase("byte(0x001234)", "/", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x001234) / byte(0x002345)")]
        [TestCase("byte(0x001234)", "&", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x001234) & byte(0x002345)")]
        [TestCase("byte(0x001234)", "%", "byte(0x002345)",
            ExpressionType.Error, "Cannot modulus using a runtime value")]
        public void TestCombineInverse(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            var op = ExpressionTests.GetMathematicOperation(operation);

            var leftExpr = ExpressionTests.Parse(left);
            var rightExpr = ExpressionTests.Parse<MemoryAccessorExpression>(right);

            var result = rightExpr.CombineInverse(leftExpr, op);
            Assert.That(result.Type, Is.EqualTo(expectedType));
            ExpressionTests.AssertAppendString(result, expected);
        }

        [Test]
        [TestCase("byte(0x001234)", "=", "4.2",
            ExpressionType.Error, "Result can never be true using integer math")]
        [TestCase("byte(0x001234)", "!=", "4.2",
            ExpressionType.Error, "Result is always true using integer math")]
        [TestCase("byte(0x001234)", "<", "4.2",
            ExpressionType.Comparison, "byte(0x001234) <= 4")]
        [TestCase("byte(0x001234)", "<=", "4.2",
            ExpressionType.Comparison, "byte(0x001234) <= 4")]
        [TestCase("byte(0x001234)", ">", "4.2",
            ExpressionType.Comparison, "byte(0x001234) > 4")]
        [TestCase("byte(0x001234)", ">=", "4.2",
            ExpressionType.Comparison, "byte(0x001234) > 4")]
        [TestCase("float(0x001234)", "=", "4.2",
            ExpressionType.None, "")] // valid, no normalization applied
        [TestCase("byte(0x001234)", "=", "byte(0x002345) * 2",
            ExpressionType.Comparison, "byte(0x002345) * 2 == byte(0x001234)")] // move modified operation to left side of comparison
        [TestCase("byte(0x001234)", "=", "byte(0x002345) + 2",
            ExpressionType.Comparison, "byte(0x002345) + 2 == byte(0x001234)")] // prefer positive modifier, swap sides
        [TestCase("byte(0x001234)", "=", "byte(0x002345) - 2",
            ExpressionType.Comparison, "byte(0x001234) + 2 == byte(0x002345)")] // constant moved to left side
        [TestCase("byte(0x001234)", "=", "prev(byte(0x002345)) - 3",
            ExpressionType.Comparison, "byte(0x001234) + 3 == prev(byte(0x002345))")] // constant moved to left side
        [TestCase("byte(0x001234)", ">", "byte(0x002345) - 2",
            ExpressionType.Comparison, "byte(0x001234) + 2 > byte(0x002345)")] // constant moved to left side
        [TestCase("byte(0x001234)", ">", "byte(0x002345) + 2",
            ExpressionType.Comparison, "byte(0x002345) + 2 < byte(0x001234)")] // prefer positive modifier, swap sides
        [TestCase("300 - byte(0x001234)", ">=", "100",
            ExpressionType.Comparison, "byte(0x001234) <= 200")] // constant moved to right, then whole thing inverted
        public void TestNormalizeComparison(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            ExpressionTests.AssertNormalizeComparison(left, operation, right, expectedType, expected);
        }

        [Test]
        public void TestWrapMemoryValue()
        {
            string input = "byte(0x001234)";
            var accessor = TriggerExpressionTests.Parse<MemoryAccessorExpression>(input);
            var converted = MemoryAccessorExpressionBase.WrapInMemoryValue(accessor);
            Assert.That(converted, Is.InstanceOf<MemoryValueExpression>());
            ExpressionTests.AssertAppendString(converted, input);
        }
    }
}
