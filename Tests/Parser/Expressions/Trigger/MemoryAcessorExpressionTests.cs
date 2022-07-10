using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Tests.Parser.Expressions.Trigger
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
        [TestCase("word((tbyte(0x002345) & 0x1FFFFFF) + 4660)")]
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
        [TestCase("word((tbyte(0x002345) & 0x1FFFFFF) + 4660)", "I:0xW002345&33554431_0x 001234")]
        [TestCase("prev(high4(0x001234))", "d0xU001234")]
        [TestCase("prior(bit0(0x001234))", "p0xM001234")]
        public void TestBuildTrigger(string input, string expected)
        {
            var accessor = TriggerExpressionTests.Parse<MemoryAccessorExpression>(input);
            TriggerExpressionTests.AssertSerialize(accessor, expected);
        }

        [Test]
        [TestCase("byte(0x001234)", "+", "2",
            ExpressionType.MemoryValue, "byte(0x001234) + 2")]
        [TestCase("byte(0x001234)", "-", "2",
            ExpressionType.MemoryValue, "byte(0x001234) - 2")]
        [TestCase("byte(0x001234)", "*", "2",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x001234) * 2")]
        [TestCase("byte(0x001234)", "/", "2",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x001234) / 2")]
        [TestCase("byte(0x001234)", "&", "2",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x001234) & 0x00000002")]
        [TestCase("byte(0x001234)", "%", "2",
            ExpressionType.Error, "Cannot modulus using a runtime value")]
        [TestCase("byte(0x001234)", "+", "byte(0x002345)",
            ExpressionType.MemoryValue, "byte(0x001234) + byte(0x002345)")]
        [TestCase("byte(0x001234)", "-", "byte(0x002345)",
            ExpressionType.MemoryValue, "byte(0x001234) - byte(0x002345)")]
        [TestCase("byte(0x001234)", "*", "byte(0x002345)",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x001234) * byte(0x002345)")]
        [TestCase("byte(0x001234)", "/", "byte(0x002345)",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x001234) / byte(0x002345)")]
        [TestCase("byte(0x001234)", "&", "byte(0x002345)",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x001234) & byte(0x002345)")]
        [TestCase("byte(0x001234)", "%", "byte(0x002345)",
            ExpressionType.Error, "Cannot modulus using a runtime value")]
        public void TestCombine(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            // MemoryAccessorExpression.Combine just converts to a ModifiedMemoryAccessor and
            // calls its Combine method. These tests are mostly redundant.
            ExpressionTests.AssertCombine(left, operation, right, expectedType, expected);
        }

        [Test]
        [TestCase("2", "+", "byte(0x002345)",
            ExpressionType.MemoryValue, "byte(0x002345) + 2")]
        [TestCase("2", "-", "byte(0x002345)",
            ExpressionType.MemoryValue, "- byte(0x002345) + 2")]
        [TestCase("2", "*", "byte(0x002345)",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x002345) * 2")]
        [TestCase("2", "/", "byte(0x002345)",
            ExpressionType.ModifiedMemoryAccessor, "2 / byte(0x002345)")]
        [TestCase("2", "&", "byte(0x002345)",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x002345) & 0x00000002")]
        [TestCase("2", "%", "byte(0x002345)",
            ExpressionType.Error, "Cannot modulus using a runtime value")]
        [TestCase("byte(0x001234)", "+", "byte(0x002345)",
            ExpressionType.MemoryValue, "byte(0x001234) + byte(0x002345)")]
        [TestCase("byte(0x001234)", "-", "byte(0x002345)",
            ExpressionType.MemoryValue, "byte(0x001234) - byte(0x002345)")]
        [TestCase("byte(0x001234)", "*", "byte(0x002345)",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x001234) * byte(0x002345)")]
        [TestCase("byte(0x001234)", "/", "byte(0x002345)",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x001234) / byte(0x002345)")]
        [TestCase("byte(0x001234)", "&", "byte(0x002345)",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x001234) & byte(0x002345)")]
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
            ExpressionType.Comparison, "byte(0x001234) - 2 == byte(0x002345)")] // constant moved to left side
        [TestCase("byte(0x001234)", "=", "byte(0x002345) - 2",
            ExpressionType.Comparison, "byte(0x001234) + 2 == byte(0x002345)")] // constant moved to left side
        [TestCase("byte(0x001234)", "=", "prev(byte(0x002345)) - 3",
            ExpressionType.Comparison, "byte(0x001234) + 3 == prev(byte(0x002345))")] // constant moved to left side
        [TestCase("byte(0x001234)", ">", "byte(0x002345) - 2",
            ExpressionType.Comparison, "byte(0x001234) + 2 > byte(0x002345)")] // constant moved to left side
        [TestCase("byte(0x001234)", ">", "byte(0x002345) + 2",
            ExpressionType.Comparison, "byte(0x001234) - 2 > byte(0x002345)")] // constant moved to left side
        public void TestNormalizeComparison(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            ExpressionTests.AssertNormalizeComparison(left, operation, right, expectedType, expected);
        }

        [Test]
        public void TestUpconvertToModifiedMemoryAccesor()
        {
            string input = "byte(0x001234)";
            var accessor = TriggerExpressionTests.Parse<MemoryAccessorExpression>(input);
            var converted = accessor.UpconvertTo(ExpressionType.ModifiedMemoryAccessor);
            Assert.That(converted, Is.InstanceOf<ModifiedMemoryAccessorExpression>());
            ExpressionTests.AssertAppendString(converted, input);
        }

        [Test]
        public void TestUpconvertToMemoryValue()
        {
            string input = "byte(0x001234)";
            var accessor = TriggerExpressionTests.Parse<MemoryAccessorExpression>(input);
            var converted = accessor.UpconvertTo(ExpressionType.MemoryValue);
            Assert.That(converted, Is.InstanceOf<MemoryValueExpression>());
            ExpressionTests.AssertAppendString(converted, input);
        }
    }
}
