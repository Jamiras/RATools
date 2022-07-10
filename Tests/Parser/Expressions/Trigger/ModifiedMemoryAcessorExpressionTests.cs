using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Tests.Parser.Expressions.Trigger
{
    [TestFixture]
    class ModifiedMemoryAccessorExpressionTests
    {
        [Test]
        [TestCase("byte(0x001234) * 10")]
        [TestCase("byte(0x001234) / 10")]
        [TestCase("byte(0x001234) & 0x0000000A")]
        [TestCase("byte(0x001234) * byte(0x002345)")]
        [TestCase("byte(0x001234) / byte(0x002345)")]
        [TestCase("byte(0x001234) & byte(0x002345)")]
        [TestCase("byte(0x001234) * byte(0x001234)")]
        [TestCase("low4(word(0x001234)) * 20")]
        [TestCase("prev(high4(0x001234)) * 16")]
        [TestCase("low4(word(0x001234)) * high4(word(0x001234) + 10)")]
        public void TestAppendString(string input)
        {
            var accessor = TriggerExpressionTests.Parse<ModifiedMemoryAccessorExpression>(input);
            ExpressionTests.AssertAppendString(accessor, input);
        }

        [Test]
        [TestCase("byte(0x001234) * 10", "0xH001234*10")]
        [TestCase("byte(0x001234) / 10", "0xH001234/10")]
        [TestCase("byte(0x001234) & 10", "0xH001234&10")]
        [TestCase("byte(0x001234) * byte(0x002345)", "0xH001234*0xH002345")]
        [TestCase("byte(0x001234) / byte(0x002345)", "0xH001234/0xH002345")]
        [TestCase("byte(0x001234) & byte(0x002345)", "0xH001234&0xH002345")]
        [TestCase("byte(0x001234) * byte(0x001234)", "0xH001234*0xH001234")]
        [TestCase("low4(word(0x001234)) * 20", "I:0x 001234_0xL000000*20")]
        [TestCase("prev(high4(0x001234)) * prior(bit3(0x001235))", "d0xU001234*p0xP001235")]
        [TestCase("low4(word(0x001234)) * high4(word(0x001234) + 10)", "I:0x 001234_0xL000000*0xU00000a")]
        public void TestBuildTrigger(string input, string expected)
        {
            var accessor = TriggerExpressionTests.Parse<ModifiedMemoryAccessorExpression>(input);
            TriggerExpressionTests.AssertSerialize(accessor, expected);
        }

        [Test]
        [TestCase("byte(0x001234) * 10", "+", "2",
            ExpressionType.MemoryValue, "byte(0x001234) * 10 + 2")]
        [TestCase("byte(0x001234) * 10", "-", "2",
            ExpressionType.MemoryValue, "byte(0x001234) * 10 - 2")]
        [TestCase("byte(0x001234) * 10", "*", "2",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x001234) * 20")]
        [TestCase("byte(0x001234) * 10", "/", "2",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x001234) * 5")]
        [TestCase("byte(0x001234) * 10", "&", "2",
            ExpressionType.Mathematic, "byte(0x001234) * 10 & 2")]
        [TestCase("byte(0x001234) * 10", "%", "2",
            ExpressionType.Error, "Cannot modulus using a runtime value")]
        [TestCase("byte(0x001234) * 10", "+", "byte(0x002345)",
            ExpressionType.MemoryValue, "byte(0x001234) * 10 + byte(0x002345)")]
        [TestCase("byte(0x001234) * 10", "-", "byte(0x002345)",
            ExpressionType.MemoryValue, "byte(0x001234) * 10 - byte(0x002345)")]
        [TestCase("byte(0x001234) * 10", "*", "byte(0x002345)",
            ExpressionType.Mathematic, "byte(0x001234) * 10 * byte(0x002345)")]
        [TestCase("byte(0x001234) * 10", "/", "byte(0x002345)",
            ExpressionType.Mathematic, "byte(0x001234) * 10 / byte(0x002345)")]
        [TestCase("byte(0x001234) * 10", "&", "byte(0x002345)",
            ExpressionType.Mathematic, "byte(0x001234) * 10 & byte(0x002345)")]
        [TestCase("byte(0x001234) * 10", "%", "byte(0x002345)",
            ExpressionType.Error, "Cannot modulus using a runtime value")]
        [TestCase("byte(0x001234) * 10", "/", "3",
            ExpressionType.Mathematic, "byte(0x001234) * 10 / 3")] // don't collapse integer division with remainder
        [TestCase("byte(0x001234) / 10", "/", "3",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x001234) / 30")]
        [TestCase("byte(0x001234) / 10", "*", "2",
            ExpressionType.Mathematic, "byte(0x001234) / 10 * 2")] // "/5" could produce differing results: 17/10*2 = 2, 17/5=3
        [TestCase("byte(0x001234) * 10", "/", "10",
            ExpressionType.ModifiedMemoryAccessor, "byte(0x001234)")] // "*1" is unnecessary
        [TestCase("byte(0x001234) / 10", "*", "10",
            ExpressionType.Mathematic, "byte(0x001234) / 10 * 10")] // "/1" could produce differing results: 17/10*10 = 10, 17/1=17
        public void TestCombine(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            ExpressionTests.AssertCombine(left, operation, right, expectedType, expected);
        }

        [Test]
        [TestCase("byte(0x001234) * 10", "=", "100",
            ExpressionType.Comparison, "byte(0x001234) == 10")]
        [TestCase("byte(0x001234) * 10", "=", "101",
            ExpressionType.Error, "Result can never be true using integer math")]
        [TestCase("byte(0x001234) * 10", ">=", "101",
            ExpressionType.Comparison, "byte(0x001234) > 10")]
        [TestCase("byte(0x001234) * 10", ">=", "10.1",
            ExpressionType.Comparison, "byte(0x001234) > 1")]
        [TestCase("byte(0x001234) / 10", "=", "100",
            ExpressionType.Comparison, "byte(0x001234) == 1000")]
        [TestCase("byte(0x001234) * 10", "=", "101.0",
            ExpressionType.Error, "Result can never be true using integer math")]
        [TestCase("byte(0x001234) * 10.0", "=", "101",
            ExpressionType.Error, "Result can never be true using integer math")]
        [TestCase("byte(0x001234) * 10.0", "=", "101.0",
            ExpressionType.Error, "Result can never be true using integer math")]
        [TestCase("byte(0x001234) * 2.2", "=", "6.6",
            ExpressionType.Comparison, "byte(0x001234) == 3")]
        [TestCase("float(0x001234) * 10", "<", "99",
            ExpressionType.Comparison, "float(0x001234) < 9.9")]
        [TestCase("float(0x001234) * 2.2", "!=", "7.4",
            ExpressionType.Comparison, "float(0x001234) != 3.363636")]
        [TestCase("byte(0x001234) * 10", "=", "byte(0x002345) * 5",
            ExpressionType.Comparison, "byte(0x001234) * 2 == byte(0x002345)")]
        [TestCase("byte(0x001234) * 10", "=", "byte(0x002345) * 3",
            ExpressionType.Error, "Result can never be true using integer math")]
        [TestCase("byte(0x001234) * 10", "=", "byte(0x002345) / 3",
            ExpressionType.Comparison, "byte(0x001234) * 30 == byte(0x002345)")]
        [TestCase("byte(0x001234) / 10", "=", "byte(0x002345) * 3",
            ExpressionType.Comparison, "byte(0x001234) / 30 == byte(0x002345)")]
        [TestCase("byte(0x001234) * 10", "=", "byte(0x002345) * 50",
            ExpressionType.Comparison, "byte(0x002345) * 5 == byte(0x001234)")] // prefer modifiers on left
        [TestCase("byte(0x001234) * 3", "=", "(byte(0x002345) + 1) * 3",
            ExpressionType.Comparison, "byte(0x001234) == byte(0x002345) + 1")] // prefer modifiers on left
        [TestCase("byte(0x001234) * 10 * 2", "=", "100",
            ExpressionType.Comparison, "byte(0x001234) == 5")]
        [TestCase("2 * byte(0x001234) * 10", "=", "100",
            ExpressionType.Comparison, "byte(0x001234) == 5")]
        [TestCase("2.2 * byte(0x001234) * 10", "=", "100",
            ExpressionType.Error, "Result can never be true using integer math")]
        [TestCase("2.2 * float(0x001234) * 10", "=", "100",
            ExpressionType.Comparison, "float(0x001234) == 4.545455")]
        [TestCase("byte(0x001234) * 10 / 2", "=", "100",
            ExpressionType.Comparison, "byte(0x001234) == 20")]
        [TestCase("byte(0x001234) * 10 / 3", "=", "100",
            ExpressionType.Comparison, "byte(0x001234) * 10 == 300")] // left is a mathematic, not an accessor, normalization only undoes the division
        [TestCase("byte(0x001234) * 100 / byte(0x002345)", ">", "75",
            ExpressionType.None, null)] // division cannot be moved; multiplication cannot be extracted
        [TestCase("byte(0x001234) / byte(0x002345) * 10", ">", "80",
            ExpressionType.Comparison, "byte(0x001234) / byte(0x002345) > 8")]
        public void TestNormalizeComparison(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            ExpressionTests.AssertNormalizeComparison(left, operation, right, expectedType, expected);
        }

        [Test]
        public void TestUpconvertToMemoryValue ()
        {
            string input = "byte(0x001234) * 10";
            var accessor = TriggerExpressionTests.Parse<ModifiedMemoryAccessorExpression>(input);
            var converted = accessor.UpconvertTo(ExpressionType.MemoryValue);
            Assert.That(converted, Is.InstanceOf<MemoryValueExpression>());
            ExpressionTests.AssertAppendString(converted, input);
        }
    }
}
