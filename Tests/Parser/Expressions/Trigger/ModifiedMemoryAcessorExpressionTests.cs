using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using System;

namespace RATools.Parser.Tests.Expressions.Trigger
{
    [TestFixture]
    class ModifiedMemoryAccessorExpressionTests
    {
        [Test]
        [TestCase("byte(0x001234) * 10")]
        [TestCase("byte(0x001234) / 10")]
        [TestCase("byte(0x001234) & 0x0000000A")]
        [TestCase("byte(0x001234) ^ 0x0000000A")]
        [TestCase("byte(0x001234) * byte(0x002345)")]
        [TestCase("byte(0x001234) / byte(0x002345)")]
        [TestCase("byte(0x001234) & byte(0x002345)")]
        [TestCase("byte(0x001234) ^ byte(0x002345)")]
        [TestCase("byte(0x001234) * byte(0x001234)")]
        [TestCase("low4(word(0x001234)) * 20")]
        [TestCase("prev(high4(0x001234)) * 16")]
        [TestCase("low4(word(0x001234)) * high4(word(0x001234) + 10)")]
        [TestCase("byte(0x001234) * -1")]
        public void TestAppendString(string input)
        {
            var accessor = TriggerExpressionTests.Parse<ModifiedMemoryAccessorExpression>(input);
            ExpressionTests.AssertAppendString(accessor, input);
        }

        [Test]
        [TestCase("byte(0x001234) * 10", "0xH001234*10")]
        [TestCase("byte(0x001234) / 10", "0xH001234/10")]
        [TestCase("byte(0x001234) & 10", "0xH001234&10")]
        [TestCase("byte(0x001234) ^ 10", "0xH001234^10")]
        [TestCase("byte(0x001234) * byte(0x002345)", "0xH001234*0xH002345")]
        [TestCase("byte(0x001234) / byte(0x002345)", "0xH001234/0xH002345")]
        [TestCase("byte(0x001234) & byte(0x002345)", "0xH001234&0xH002345")]
        [TestCase("byte(0x001234) ^ byte(0x002345)", "0xH001234^0xH002345")]
        [TestCase("byte(0x001234) * byte(0x001234)", "0xH001234*0xH001234")]
        [TestCase("low4(word(0x001234)) * 20", "I:0x 001234_0xL000000*20")]
        [TestCase("prev(high4(0x001234)) * prior(bit3(0x001235))", "d0xU001234*p0xP001235")]
        [TestCase("low4(word(0x001234)) * high4(word(0x001234) + 10)", "I:0x 001234_0xL000000*0xU00000a")]
        [TestCase("byte(0x001234) * -1", "0xH001234*4294967295")]
        [TestCase("byte(dword(0x001234) + 0x10) * 10", "I:0xX001234_0xH000010*10")]
        [TestCase("byte(dword(0x001234) + 0x10) * byte(dword(0x001234) + 0x14)", "I:0xX001234_0xH000010*0xH000014")]
        [TestCase("byte(dword(0x001234) + 0x10) / 10", "I:0xX001234_0xH000010/10")]
        [TestCase("byte(dword(0x001234) + 0x10) / byte(dword(0x001234) + 0x14)", "I:0xX001234_0xH000010/0xH000014")]
        public void TestBuildTrigger(string input, string expected)
        {
            var accessor = TriggerExpressionTests.Parse<ModifiedMemoryAccessorExpression>(input);
            TriggerExpressionTests.AssertSerialize(accessor, expected);
        }

        [Test]
        [TestCase("byte(0x001234) * 10", "+", "2",
            ExpressionType.MemoryAccessor, "byte(0x001234) * 10 + 2")]
        [TestCase("byte(0x001234) * 10", "-", "2",
            ExpressionType.MemoryAccessor, "byte(0x001234) * 10 - 2")]
        [TestCase("byte(0x001234) * 10", "*", "2",
            ExpressionType.MemoryAccessor, "byte(0x001234) * 20")]
        [TestCase("byte(0x001234) * 10", "/", "2",
            ExpressionType.MemoryAccessor, "byte(0x001234) * 5")]
        [TestCase("byte(0x001234) * 10", "&", "2",
            ExpressionType.Mathematic, "byte(0x001234) * 10 & 2")]
        [TestCase("byte(0x001234) * 10", "^", "2",
            ExpressionType.Mathematic, "byte(0x001234) * 10 ^ 2")]
        [TestCase("byte(0x001234) * 10", "%", "2",
            ExpressionType.Error, "Cannot modulus using a runtime value")]
        [TestCase("byte(0x001234) * 10", "+", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x001234) * 10 + byte(0x002345)")]
        [TestCase("byte(0x001234) * 10", "-", "byte(0x002345)",
            ExpressionType.MemoryAccessor, "byte(0x001234) * 10 - byte(0x002345)")]
        [TestCase("byte(0x001234) * 10", "*", "byte(0x002345)",
            ExpressionType.Mathematic, "byte(0x001234) * 10 * byte(0x002345)")]
        [TestCase("byte(0x001234) * 10", "/", "byte(0x002345)",
            ExpressionType.Mathematic, "byte(0x001234) * 10 / byte(0x002345)")]
        [TestCase("byte(0x001234) * 10", "&", "byte(0x002345)",
            ExpressionType.Mathematic, "byte(0x001234) * 10 & byte(0x002345)")]
        [TestCase("byte(0x001234) * 10", "^", "byte(0x002345)",
            ExpressionType.Mathematic, "byte(0x001234) * 10 ^ byte(0x002345)")]
        [TestCase("byte(0x001234) * 10", "%", "byte(0x002345)",
            ExpressionType.Error, "Cannot modulus using a runtime value")]
        [TestCase("byte(0x001234) * 10", "/", "3",
            ExpressionType.Mathematic, "byte(0x001234) * 10 / 3")] // don't collapse integer division with remainder
        [TestCase("byte(0x001234) / 10", "/", "3",
            ExpressionType.MemoryAccessor, "byte(0x001234) / 30")]
        [TestCase("byte(0x001234) / 10", "*", "2",
            ExpressionType.Mathematic, "byte(0x001234) / 10 * 2")] // "/5" could produce differing results: 17/10*2 = 2, 17/5=3
        [TestCase("byte(0x001234) * 10", "/", "10",
            ExpressionType.MemoryAccessor, "byte(0x001234)")] // "*1" is unnecessary
        [TestCase("byte(0x001234) / 10", "*", "10",
            ExpressionType.Mathematic, "byte(0x001234) / 10 * 10")] // "/1" could produce differing results: 17/10*10 = 10, 17/1=17
        [TestCase("byte(0x001234) & 10", "&", "6",
            ExpressionType.MemoryAccessor, "byte(0x001234) & 0x00000002")] // merge ANDs
        [TestCase("byte(0x001234) ^ 10", "^", "6",
            ExpressionType.MemoryAccessor, "byte(0x001234) ^ 0x0000000C")] // merge XORs
        [TestCase("byte(0x001234) ^ 10", "&", "6",
            ExpressionType.MemoryAccessor, "byte(0x001234) ^ 0x00000002")] // merge ANDs
        [TestCase("byte(0x001234) & 10", "^", "6",
            ExpressionType.Mathematic, "byte(0x001234) & 0x0000000A ^ 6")] // cannot merge
        [TestCase("byte(dword(0x001234) + 0x10)", "*", "byte(dword(0x002345) + 0x14)",
            ExpressionType.Error, "Cannot multiply two values with differing pointers")]
        [TestCase("byte(dword(0x001234) + 0x10)", "/", "byte(dword(0x002345) + 0x14)",
            ExpressionType.Error, "Cannot divide two values with differing pointers")]
        [TestCase("byte(dword(0x001234) + 0x10)", "*", "byte(0x10)",
            ExpressionType.Error, "Cannot multiply two values with differing pointers")]
        [TestCase("byte(dword(0x001234) + 0x10)", "/", "byte(0x10)",
            ExpressionType.Error, "Cannot divide two values with differing pointers")]
        [TestCase("byte(0x10) * 10", "*", "byte(dword(0x002345) + 0x14)",
            ExpressionType.Error, "Cannot multiply pointer value and modified value")]
        [TestCase("byte(0x10) * 10", "/", "byte(dword(0x002345) + 0x14)",
            ExpressionType.Error, "Cannot divide pointer value and modified value")]
        [TestCase("byte(0x001234) * 10", "&", "2.5",
            ExpressionType.Error, "Cannot perform bitwise operations on floating point values")]
        [TestCase("byte(0x001234) * 10", "^", "2.5",
            ExpressionType.Error, "Cannot perform bitwise operations on floating point values")]
        [TestCase("byte(0x001234) * 2.5", "&", "10",
            ExpressionType.Error, "Cannot perform bitwise operations on floating point values")]
        [TestCase("byte(0x001234) * 2.5", "^", "10",
            ExpressionType.Error, "Cannot perform bitwise operations on floating point values")]
        [TestCase("byte(0x001234) & 10", "*", "2.5",
            ExpressionType.Error, "Cannot perform bitwise operations on floating point values")]
        [TestCase("byte(0x001234) ^ 10", "*", "2.5",
            ExpressionType.Error, "Cannot perform bitwise operations on floating point values")]
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
        [TestCase("byte(0x001234) * 10", "=", "byte(0x002345) * 8",
            ExpressionType.Comparison, "byte(0x001234) * 10 - byte(0x002345) * 8 == 0U")] // comparison of two modified memory accesors results in subsource chain
        [TestCase("byte(0x001234) * 10", "!=", "byte(0x002345) * 8",
            ExpressionType.Comparison, "byte(0x001234) * 10 - byte(0x002345) * 8 != 0U")] // comparison of two modified memory accesors results in subsource chain
        [TestCase("byte(0x001234) * 10", "<", "byte(0x002345) * 8",
            ExpressionType.Comparison, "byte(0x001234) * 10 - byte(0x002345) * 8 >= 2147483648U")] // comparison of two modified memory accesors results in subsource chain
        [TestCase("byte(0x001234) * 10", "<=", "byte(0x002345) * 8",
            ExpressionType.Requirement, "byte(0x001234) * 10 - byte(0x002345) * 8 >= 2147483648U || byte(0x001234) * 10 - byte(0x002345) * 8 == 0U")] // comparison of two modified memory accesors results in subsource chain
        [TestCase("byte(0x001234) * 10", ">", "byte(0x002345) * 8",
            ExpressionType.Requirement, "byte(0x001234) * 10 - byte(0x002345) * 8 < 2147483648U && byte(0x001234) * 10 - byte(0x002345) * 8 != 0U")] // comparison of two modified memory accesors results in subsource chain
        [TestCase("byte(0x001234) * 10", ">=", "byte(0x002345) * 8",
            ExpressionType.Comparison, "byte(0x001234) * 10 - byte(0x002345) * 8 < 2147483648U")] // comparison of two modified memory accesors results in subsource chain
        public void TestNormalizeComparison(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            ExpressionTests.AssertNormalizeComparison(left, operation, right, expectedType, expected);
        }

        [Test]
        public void TestWrapInMemoryValue()
        {
            string input = "byte(0x001234) * 10";
            var accessor = TriggerExpressionTests.Parse<ModifiedMemoryAccessorExpression>(input);
            var converted = MemoryAccessorExpressionBase.WrapInMemoryValue(accessor);
            Assert.That(converted, Is.InstanceOf<MemoryValueExpression>());
            ExpressionTests.AssertAppendString(converted, input);
        }

        [Test]
        [TestCase("dword(0x1234) & 0xFFFFFFFF", "dword(0x001234)")]
        [TestCase("dword(0x1234) & 0x0FFFFFFF", "dword(0x001234) & 0x0FFFFFFF")]
        [TestCase("dword(0x1234) & 0x00FFFFFF", "tbyte(0x001234)")]
        [TestCase("dword(0x1234) & 0x000FFFFF", "tbyte(0x001234) & 0x000FFFFF")]
        [TestCase("dword(0x1234) & 0x0000FFFF", "word(0x001234)")]
        [TestCase("dword(0x1234) & 0x00000FFF", "word(0x001234) & 0x00000FFF")]
        [TestCase("dword(0x1234) & 0x000000FF", "byte(0x001234)")]
        [TestCase("dword(0x1234) & 0x0000000F", "low4(0x001234)")]
        [TestCase("dword(0x1234) & 0x00000001", "bit0(0x001234)")]
        [TestCase("dword(0x1234) & 0x00000000", "0")]
        [TestCase("dword_be(0x1234) & 0xFFFFFFFF", "dword_be(0x001234)")]
        [TestCase("dword_be(0x1234) & 0x0FFFFFFF", "dword_be(0x001234) & 0x0FFFFFFF")]
        [TestCase("dword_be(0x1234) & 0x00FFFFFF", "tbyte_be(0x001235)")]
        [TestCase("dword_be(0x1234) & 0x000FFFFF", "tbyte_be(0x001235) & 0x000FFFFF")]
        [TestCase("dword_be(0x1234) & 0x0000FFFF", "word_be(0x001236)")]
        [TestCase("dword_be(0x1234) & 0x00000FFF", "word_be(0x001236) & 0x00000FFF")]
        [TestCase("dword_be(0x1234) & 0x000000FF", "byte(0x001237)")]
        [TestCase("dword_be(0x1234) & 0x0000000F", "low4(0x001237)")]
        [TestCase("dword_be(0x1234) & 0x00000001", "bit0(0x001237)")]
        [TestCase("dword_be(0x1234) & 0x00000000", "0")]
        [TestCase("word(0x1234) & 0xFFFFFFFF", "word(0x001234)")]
        [TestCase("word(0x1234) & 0x0FFFFFFF", "word(0x001234)")]
        [TestCase("word(0x1234) & 0x00FFFFFF", "word(0x001234)")]
        [TestCase("word(0x1234) & 0x000FFFFF", "word(0x001234)")]
        [TestCase("word(0x1234) & 0x0000FFFF", "word(0x001234)")]
        [TestCase("word(0x1234) & 0x00000FFF", "word(0x001234) & 0x00000FFF")]
        [TestCase("word(0x1234) & 0x000000FF", "byte(0x001234)")]
        [TestCase("word(0x1234) & 0x0000000F", "low4(0x001234)")]
        [TestCase("word(0x1234) & 0x00000001", "bit0(0x001234)")]
        [TestCase("word(0x1234) & 0x00000000", "0")]
        [TestCase("word_be(0x1234) & 0xFFFFFFFF", "word_be(0x001234)")]
        [TestCase("word_be(0x1234) & 0x0FFFFFFF", "word_be(0x001234)")]
        [TestCase("word_be(0x1234) & 0x00FFFFFF", "word_be(0x001234)")]
        [TestCase("word_be(0x1234) & 0x000FFFFF", "word_be(0x001234)")]
        [TestCase("word_be(0x1234) & 0x0000FFFF", "word_be(0x001234)")]
        [TestCase("word_be(0x1234) & 0x00000FFF", "word_be(0x001234) & 0x00000FFF")]
        [TestCase("word_be(0x1234) & 0x000000FF", "byte(0x001235)")]
        [TestCase("word_be(0x1234) & 0x0000000F", "low4(0x001235)")]
        [TestCase("word_be(0x1234) & 0x00000001", "bit0(0x001235)")]
        [TestCase("word_be(0x1234) & 0x00000000", "0")]
        [TestCase("byte(0x1234) & 0x0000FFFF", "byte(0x001234)")]
        [TestCase("byte(0x1234) & 0x00000FFF", "byte(0x001234)")]
        [TestCase("byte(0x1234) & 0x000000FF", "byte(0x001234)")]
        [TestCase("byte(0x1234) & 0x0000000F", "low4(0x001234)")]
        [TestCase("byte(0x1234) & 0x00000001", "bit0(0x001234)")]
        [TestCase("byte(0x1234) & 0x00000000", "0")]
        [TestCase("dword(0x1234) & 0xFFF0FFFF", "dword(0x001234) & 0xFFF0FFFF")]
        [TestCase("dword(0x1234) & 0x10000000", "dword(0x001234) & 0x10000000")]
        public void TestApplyMask(string input, string expected)
        {
            var accessor = TriggerExpressionTests.Parse(input);
            ExpressionTests.AssertAppendString(accessor, expected);
        }
    }
}
