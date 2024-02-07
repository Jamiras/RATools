using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System;
using System.Text;

namespace RATools.Parser.Tests.Expressions.Trigger
{
    [TestFixture]
    class MemoryValueExpressionTests
    {
        [Test]
        [TestCase("byte(0x001234) + 1")]
        [TestCase("word(0x001234) - 1")]
        [TestCase("float(0x001234) + 1.2345")]
        [TestCase("mbf32(0x001234) - 1.2345")]
        [TestCase("low4(0x001234) + high4(0x001234)")]
        [TestCase("bit0(0x001234) + bit1(0x001234) - bit2(0x001234) + bit3(0x001234) - 1")]
        public void TestAppendString(string input)
        {
            var accessor = TriggerExpressionTests.Parse<MemoryValueExpression>(input);
            ExpressionTests.AssertAppendString(accessor, input);
        }

        [Test]
        [TestCase("byte(0x001234) + 1", "A:1=0_0xH001234")] // constant should always be first
        [TestCase("word(0x001234) - 1", "B:1=0_0x 001234")] // constant should always be first
        [TestCase("float(0x001234) + 1.2345", "A:f1.2345_fF001234")] // constant should always be first
        [TestCase("mbf32(0x001234) - 1.2345", "B:f1.2345_fM001234")] // constant should always be first
        [TestCase("low4(0x001234) + high4(0x001234)", "A:0xL001234=0_0xU001234")]
        [TestCase("bit0(0x001234) + bit1(0x001234) - bit2(0x001234) + bit3(0x001234) - 1",
            "B:1=0_A:0xM001234=0_A:0xN001234=0_B:0xO001234=0_0xP001234")] // constant should always be first
        public void TestBuildTrigger(string input, string expected)
        {
            var accessor = TriggerExpressionTests.Parse<MemoryValueExpression>(input);
            TriggerExpressionTests.AssertSerialize(accessor, expected);
        }

        [Test]
        [TestCase("byte(0x001234) + 10", "+", "2",
                ExpressionType.MemoryAccessor, "byte(0x001234) + 12")]
        [TestCase("byte(0x001234) + 10", "-", "2",
                ExpressionType.MemoryAccessor, "byte(0x001234) + 8")]
        [TestCase("byte(0x001234) + 10", "-", "12",
                ExpressionType.MemoryAccessor, "byte(0x001234) - 2")]
        [TestCase("byte(0x001234) + word(0x002345)", "+", "2",
                ExpressionType.MemoryAccessor, "byte(0x001234) + word(0x002345) + 2")]
        [TestCase("byte(0x001234) + 2", "+", "word(0x002345)",
                ExpressionType.MemoryAccessor, "byte(0x001234) + word(0x002345) + 2")]
        [TestCase("byte(0x001234) + 10", "+", "2.5",
                ExpressionType.MemoryAccessor, "byte(0x001234) + 12.5")]
        [TestCase("byte(0x001234) + 10.5", "+", "2",
                ExpressionType.MemoryAccessor, "byte(0x001234) + 12.5")]
        [TestCase("byte(0x001234) + 10.25", "+", "2.25",
                ExpressionType.MemoryAccessor, "byte(0x001234) + 12.5")]
        [TestCase("byte(0x001234) + 2", "+", "word(0x002345) - 6",
                ExpressionType.MemoryAccessor, "byte(0x001234) + word(0x002345) - 4")]
        [TestCase("byte(0x001234) + 2", "-", "word(0x002345) - 6",
                ExpressionType.MemoryAccessor, "byte(0x001234) - word(0x002345) + 8")]
        [TestCase("byte(0x001234) + 10", "*", "2",
                ExpressionType.MemoryAccessor, "byte(0x001234) * 2 + 20")]
        [TestCase("byte(0x001234) * 50 + 10", "/", "5",
                ExpressionType.MemoryAccessor, "byte(0x001234) * 10 + 2")]
        [TestCase("byte(0x001234) * 50 + 12", "/", "3",
                ExpressionType.None, null)] // division with remainder will not be processed
        [TestCase("byte(0x001234) * 12 + 5", "/", "3",
                ExpressionType.Mathematic, "(byte(0x001234) * 12 + 5) / 3")] // division with remainder will return mathematic
        [TestCase("byte(0x001234) + 1.5", "*", "3",
                ExpressionType.MemoryAccessor, "byte(0x001234) * 3 + 4.5")]
        [TestCase("byte(0x001234) + 3.0", "/", "4",
                ExpressionType.MemoryAccessor, "byte(0x001234) / 4 + 0.75")]
        public void TestCombine(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            ExpressionTests.AssertCombine(left, operation, right, expectedType, expected);
        }

        [Test]
        [TestCase("2", "+", "byte(0x001234) + 10",
                ExpressionType.MemoryAccessor, "byte(0x001234) + 12")]
        [TestCase("2", "-", "byte(0x001234) + 10",
                ExpressionType.MemoryAccessor, "- byte(0x001234) - 8")]
        [TestCase("12", "-", "byte(0x001234) + 10",
                ExpressionType.MemoryAccessor, "- byte(0x001234) + 2")]
        [TestCase("2", "+", "byte(0x001234) + word(0x002345)",
                ExpressionType.MemoryAccessor, "byte(0x001234) + word(0x002345) + 2")]
        [TestCase("word(0x002345)", "+", "byte(0x001234) + 2",
                ExpressionType.MemoryAccessor, "word(0x002345) + byte(0x001234) + 2")]
        [TestCase("2.5", "+", "byte(0x001234) + 10",
                ExpressionType.MemoryAccessor, "byte(0x001234) + 12.5")]
        [TestCase("2", "+", "byte(0x001234) + 10.5",
                ExpressionType.MemoryAccessor, "byte(0x001234) + 12.5")]
        [TestCase("2.25", "+", "byte(0x001234) + 10.25",
                ExpressionType.MemoryAccessor, "byte(0x001234) + 12.5")]
        [TestCase("word(0x002345) - 6", "+", "byte(0x001234) + 2",
                ExpressionType.MemoryAccessor, "word(0x002345) + byte(0x001234) - 4")]
        [TestCase("word(0x002345) - 6", "-", "byte(0x001234) + 2",
                ExpressionType.MemoryAccessor, "word(0x002345) - byte(0x001234) - 8")]
        [TestCase("2", "*", "byte(0x001234) + 10",
                ExpressionType.MemoryAccessor, "byte(0x001234) * 2 + 20")]
        [TestCase("5", "/", "byte(0x001234) * 50 + 10",
                ExpressionType.Error, "Cannot divide by complex memory reference")]
        [TestCase("3", "*", "byte(0x001234) + 1.5",
                ExpressionType.MemoryAccessor, "byte(0x001234) * 3 + 4.5")]
        public void TestCombineInverse(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            var op = ExpressionTests.GetMathematicOperation(operation);

            var leftExpr = ExpressionTests.Parse(left);
            var rightExpr = ExpressionTests.Parse<MemoryValueExpression>(right);

            var result = rightExpr.CombineInverse(leftExpr, op);
            Assert.That(result.Type, Is.EqualTo(expectedType));
            ExpressionTests.AssertAppendString(result, expected);
        }

        [Test]
        [TestCase("byte(0x001234) + 2", "=", "6",
            ExpressionType.Comparison, "byte(0x001234) == 4")]
        [TestCase("byte(0x001234) + 2", "=", "byte(0x002345)",
            ExpressionType.None, null)]
        [TestCase("byte(0x001234) + 2", "=", "byte(0x002345) + 6",
            ExpressionType.Comparison, "byte(0x001234) - byte(0x002345) == 4")]
        [TestCase("byte(0x001234) + 2", "=", "byte(0x002345) + 2",
            ExpressionType.Comparison, "byte(0x001234) == byte(0x002345)")]
        [TestCase("byte(0x001234) * 2 + 1", "=", "byte(0x002345) * 2 + 1",
            ExpressionType.Comparison, "byte(0x001234) * 2 == byte(0x002345) * 2")] // first step only removes addition
        [TestCase("byte(0x001234) + 1.2", "=", "byte(0x002345) + 2.6",
            ExpressionType.Comparison, "byte(0x001234) - byte(0x002345) == 1.4")]
        [TestCase("byte(0x001234) * 2 + 2", "=", "6",
            ExpressionType.Comparison, "byte(0x001234) * 2 == 4")] // normalization only undoes the addition at this level
        [TestCase("byte(0x001234) * 2 + byte(0x002345)", "=", "6",
            ExpressionType.None, null)] // multiplication cannot be factored out
        [TestCase("byte(0x001234) * 2", "=", "byte(0x002345)",
            ExpressionType.None, null)] // multiplication cannot be factored out
        [TestCase("byte(0x001234) * 2", "=", "byte(0x002345) * 4",
            ExpressionType.Comparison, "byte(0x002345) * 2 == byte(0x001234)")] // move modified operation to left side of comparison
        [TestCase("byte(0x001234) - byte(0x002345)", "=", "2",
            ExpressionType.None, null)] // no change necessary
        [TestCase("0 + byte(0x001234) - 9", "=", "0",
            ExpressionType.Comparison, "byte(0x001234) == 9")]
        [TestCase("byte(tbyte(0x001234) + 1) + byte(tbyte(0x001234) + 2) - 7", ">=", "byte(tbyte(0x001234) + 3)",
            ExpressionType.Comparison, "byte(tbyte(0x001234) + 1) + byte(tbyte(0x001234) + 2) - 7 >= byte(tbyte(0x001234) + 3)")]
        public void TestNormalizeComparison(string left, string operation, string right, ExpressionType expectedType, string expected)
        {
            ExpressionTests.AssertNormalizeComparison(left, operation, right, expectedType, expected);
        }

        // If the result of subtracting two bytes is negative, it becomes a very large positive
        // number so you can't perform less than checks. Try to rearrange the logic so no subtraction
        // is performed. If that's not possible, add a constant to both sides of the equation to
        // prevent the subtraction from resulting in a negative number.
        [Test]
        [TestCase("A < B", "A < B")] // control
        [TestCase("A - 10 < B", "B + 10 > A")] // reverse and change to addition
        [TestCase("A - 10 <= B", "B + 10 >= A")] // reverse and change to addition
        [TestCase("A - 10 > B", "B + 10 < A")] // reverse and change to addition
        [TestCase("A - 10 >= B", "B + 10 <= A")] // reverse and change to addition
        [TestCase("A - 10 == B", "A - 10 == B")] // no change needed for equality
        [TestCase("A - 10 != B", "A - 10 != B")] // no change needed for inequality
        [TestCase("A + 10 < B", "A + 10 < B")] // no change needed for addition
        [TestCase("A + 10 > B", "A + 10 > B")] // no change needed for addition
        [TestCase("A + 10 == B", "A + 10 == B")] // no change needed for addition or equality
        [TestCase("A + 10 != B", "A + 10 != B")] // no change needed for addition or inequality
        [TestCase("A - B > 0", "A > B")] // move B
        [TestCase("A + B > 0", "A + B > 0")] // any rearranging will introduce a subtraction
        [TestCase("A + B > 10", "A + B > 10")] // no change needed for addition
        [TestCase("A - B > 10", "B + 10 < A")] // reverse and change to addition
        [TestCase("A - B < 0", "A < B")] // move B
        [TestCase("A - B < 3", "B + 3 > A")] // reverse and change to addition
        [TestCase("A - B > -3", "A + 3 > B")] // move -3 to left side and B to right side
        [TestCase("A - B == 10", "A - B == 10")] // don't rearrange equality comparisons
        [TestCase("A - B != 10", "A - B != 10")] // don't rearrange equality comparisons
        [TestCase("A + 1 - B > 3", "A - B + 255 > 257")] // explicit underflow adjustment ignored for greater than
        [TestCase("A + 1 - B <= 2", "A - B + 1 <= 2")] // explicit underflow adjustment provided
        [TestCase("A + 3 - B > 1", "A - B + 255 > 253")] // explicit underflow adjustment ignored for greater than
        [TestCase("A + 3 - B == 1", "A + 2 == B")] // explicit underflow adjustment ignored for equality, move B right and 1 left
        [TestCase("A + 1 - B > -3", "A - B + 255 > 251")] // explicit underflow ignored for greater than
        [TestCase("A + 1 - B < -3", "A - B + 255 < 251")] // explicit underflow ignored when right side is negative
        [TestCase("A - B + 355 > 255", "A + 100 > B")] // move B to right side, and 100 to left side 
        [TestCase("5 - A < 2", "A > 3")] // move A to right side, 2 to left, and reverse
        [TestCase("5 - A == 2", "A == 3")] // move A to right side, 2 to left, and reverse
        [TestCase("300 - A < 100", "A > 200")] // move A to right side, 100 to left, and reverse
        [TestCase("A + B + C < 100", "A + B + C < 100")] // no change needed
        [TestCase("A + B - C < 100", "A + B - C + 255 < 355")] // possible underflow of 255, add to both sides
        [TestCase("A - B + C < 100", "A - B + C + 255 < 355")] // possible underflow of 255, add to both sides
        [TestCase("A - B - C < 100", "B + C + 100 > A")] // move B and C to right and reverse
        [TestCase("A - B - C < -100", "A - B - C + 510 < 410")] // possible underflow of 510, add to both sides
        [TestCase("A - B - C + 700 < 800", "A - B - C + 510 < 610")] // excess underflow coverage will be minimized
        [TestCase("A - B - C + 300 < 100", "A - B - C + 510 < 310")] // possible underflow of 210, add to both sides
        [TestCase("A - B < C + 100", "B + C + 100 > A")] // C will be moved to left side, then B and C back to the right, and finally reverse
        [TestCase("A - 100 > B - C", "A - B + C + 255 > 355")] // move 100 to right side, B and C to left, and add 255 to prevent underflow
        [TestCase("A * 2 - B < 3", "A * 2 - B + 255 < 258")] // reverse and change to addition
        [TestCase("A - B * 2 < 3", "B * 2 + 3 > A")] // reverse and change to addition
        [TestCase("A / 2 - B < 3", "A / 2 - B + 255 < 258")] // reverse and change to addition
        [TestCase("A - B / 2 < 3", "B / 2 + 3 > A")] // reverse and change to addition
        [TestCase("A / A - (B / B) >= 1", "A / A - B / B + 1 >= 2")] // 0 - 1 is still an underflow
        [TestCase("A / B - (B / C) >= 1", "A / B - B / C + 255 >= 256")] // B/C could be 255
        [TestCase("A / A - 2 > (B / B)", "A / A - B / B + 1 > 3")] // move constsnt to right, avoid underflow
        [TestCase("A / B - 2 > (B / C)", "A / B - B / C + 255 > 257")] // B/C could be 255
        [TestCase("A <= B + 5", "B + 5 >= A")]
        [TestCase("A == B + 5", "B + 5 == A")]
        [TestCase("A + 0 <= B + 5", "B + 5 >= A")]
        public void TestUnderflow(string before, string after)
        {
            string left, op, right;
            var input = ExpressionTests.ReplacePlaceholders(before);
            ExpressionTests.SplitComparison(input, out left, out op, out right);

            if (before == after)
            {
                ExpressionTests.AssertNormalizeComparison(left, op, right, ExpressionType.None, null);
            }
            else
            {
                var expected = ExpressionTests.ReplacePlaceholders(after);
                ExpressionTests.AssertNormalizeComparison(left, op, right, ExpressionType.Comparison, expected);

                // prove that the logic is equivalent                  0123456789
                // ignore items where the explicit underflow was kept (A + 1 - B  ~> A - B + 1)
                var minimum = input.Contains('/') ? "1" : "0";
                var swapped = input.Length > 9 ? input.Substring(0, 1) + input.Substring(5, 4) + input.Substring(1, 4) + input.Substring(9) : string.Empty;
                if (swapped != expected)
                {
                    var values = new string[] { minimum, "10", "100", "255" };
                    foreach (var a in values)
                    {
                        var bValues = input.Contains('B') ? values : new string[] { minimum };
                        foreach (var b in bValues)
                        {
                            var cValues = input.Contains('C') ? values : new string[] { minimum };
                            foreach (var c in cValues)
                            {
                                var original = before.Replace("A", a).Replace("B", b).Replace("C", c);
                                var tokenizer = Tokenizer.CreateTokenizer(original);
                                var originalExpression = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
                                var originalEval = IsComparisonTrue(originalExpression, true);

                                var updated = after.Replace("A", a).Replace("B", b).Replace("C", c);
                                tokenizer = Tokenizer.CreateTokenizer(updated);
                                var updatedExpression = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));
                                var updatedEval = IsComparisonTrue(updatedExpression, false);

                                Assert.That(originalEval, Is.EqualTo(updatedEval), "{0} ({1})  ~>  {2} ({3})", original, originalEval, updated, updatedEval);
                            }
                        }
                    }
                }
            }
        }

        private static bool IsComparisonTrue(ExpressionBase expression, bool signed)
        {
            Assert.That(expression, Is.InstanceOf<ComparisonExpression>());
            var comparison = (ComparisonExpression)expression;

            // the two sides of comparison should be mathematic equations that simplify to single integers
            ExpressionBase left;
            comparison.Left.ReplaceVariables(null, out left);
            Assert.That(left, Is.InstanceOf<IntegerConstantExpression>());

            ExpressionBase right;
            comparison.Right.ReplaceVariables(null, out right);
            Assert.That(right, Is.InstanceOf<IntegerConstantExpression>());

            long lval, rval;
            if (signed)
            {
                lval = (long)((IntegerConstantExpression)left).Value;
                rval = (long)((IntegerConstantExpression)right).Value;
            }
            else
            {
                lval = (long)(uint)((IntegerConstantExpression)left).Value;
                rval = (long)(uint)((IntegerConstantExpression)right).Value;
            }

            switch (comparison.Operation)
            {
                case ComparisonOperation.Equal:
                    return lval == rval;
                case ComparisonOperation.NotEqual:
                    return lval != rval;
                case ComparisonOperation.GreaterThan:
                    return lval > rval;
                case ComparisonOperation.GreaterThanOrEqual:
                    return lval >= rval;
                case ComparisonOperation.LessThan:
                    return lval < rval;
                case ComparisonOperation.LessThanOrEqual:
                    return lval <= rval;
                default:
                    return false;
            }
        }

        // TestUnderflow only supports byte(N), it does not support word(N), dword(N), or indirect
        // memory references. Furthermore, the way it handles indirect memory references creates
        // invalid syntax (indirect memory references are not allowed on the right side), so go
        // one step farther to see the final optimized logic.
        [TestCase("byte(byte(0x000002) + 1) - byte(byte(0x000002) + 2) > 100",
                  "byte(byte(0x000002) + 1) - 100 > byte(byte(0x000002) + 2)", // A - B > 100  ~>  A - 100 > B
                  "A:100_I:0xH000002_0xH000002<0xH000001")]      // both A and B have the same base pointer
        [TestCase("byte(byte(0x000002) + 1) - byte(byte(0x000002) + 2) > -100",
                  "byte(byte(0x000002) + 1) + 100 > byte(byte(0x000002) + 2)", // A - B > -100  ~>  A + 100 > B
                  "A:100_I:0xH000002_0xH000001>0xH000002")]      // both A and B have the same base pointer
        [TestCase("byte(byte(0x000002) + 1) - byte(byte(0x000003) + 2) > 100",
                  "byte(byte(0x000002) + 1) - byte(byte(0x000003) + 2) + 255 > 355",  // different base pointer causes secondary AddSource
                  "A:255_I:0xH000003_B:0xH000002_I:0xH000002_0xH000001>355")]
        [TestCase("byte(byte(0x000002) + 1) - 1 == prev(byte(byte(0x000002) + 1))",
                  "byte(byte(0x000002) + 1) - 1 == prev(byte(byte(0x000002) + 1))",
                  "B:1_I:0xH000002_0xH000001=d0xH000001")]      // both A and B have the same base pointer
        [TestCase("prev(byte(byte(0x000002) + 1)) == byte(byte(0x000002) + 1) - 1",
                  "prev(byte(byte(0x000002) + 1)) + 1 == byte(byte(0x000002) + 1)",
                  "A:1_I:0xH000002_d0xH000001=0xH000001")]      // constant will be moved
        [TestCase("byte(byte(0x000002) + 1) - prev(byte(byte(0x000002) + 1)) == 1",
                  "byte(byte(0x000002) + 1) - 1 == prev(byte(byte(0x000002) + 1))",
                  "B:1_I:0xH000002_0xH000001=d0xH000001")]      // prev(A) and -1 will be swapped to share pointer
        [TestCase("word(54) - word(word(43102) + 54) > 37",
                  "word(word(0x00A85E) + 54) + 37 < word(0x000036)", // A - B > 37  ~>  B + 37 < A
                  "I:0x 00a85e_A:0x 000036_37<0x 000036")] // underflow with combination of direct/indirect, word size
        [TestCase("word(0x000036) + 37 >= word(word(0x00A85E) + 54)",
                  "word(word(0x00A85E) + 54) - word(0x000036) + 65535 <= 65572", // move A to right side, reverse and adjust for underflow
                  "A:65535_B:0x 000036_I:0x 00a85e_0x 000036<=65572")] // combination of direct/indirect, word size
        [TestCase("word(0x000001) - word(0x000002) + word(0x000003) < 100",
                  "word(0x000001) - word(0x000002) + word(0x000003) + 65535 < 65635", // possible underflow of 65535
                  "A:65535=0_A:0x 000001=0_B:0x 000002=0_0x 000003<65635")]
        [TestCase("dword(0x000001) - dword(0x000002) + dword(0x000003) < 100",
                  "dword(0x000001) - dword(0x000002) + dword(0x000003) < 100", // possible underflow of 2^32-1, ignore
                  "A:0xX000001=0_B:0xX000002=0_0xX000003<100")]
        [TestCase("byte(dword(0x000001)) - byte(dword(0x000002)) + byte(dword(0x000003)) < 100",
                  "byte(dword(0x000001)) - byte(dword(0x000002)) + byte(dword(0x000003)) + 255 < 355", // reads are only bytes, underflow is 255
                  "A:255_I:0xX000001_A:0xH000000_I:0xX000002_B:0xH000000_I:0xX000003_0xH000000<355")]
        [TestCase("word(0x000001) - word(0x000002) - byte(0x000003) + byte(0x000004) < 100",
                  "word(0x000001) - word(0x000002) - byte(0x000003) + byte(0x000004) + 65790 < 65890", // combination of byte and word
                  "A:65790=0_A:0x 000001=0_B:0x 000002=0_B:0xH000003=0_0xH000004<65890")]
        [TestCase("byte(0x000001) + byte(0x000002) > byte(0x000003) - byte(0x000004)",
                  "byte(0x000001) + byte(0x000002) + byte(0x000004) > byte(0x000003)", // move byte(4) to left side
                  "A:0xH000001=0_A:0xH000002=0_0xH000004>0xH000003")]
        [TestCase("byte(0x000001) + byte(0x000002) > byte(0x000003) + byte(0x000004)",
                  "byte(0x000001) + byte(0x000002) - byte(0x000003) - byte(0x000004) + 510 > 510", // move byte(0x000003) to left side, add 255 to prevent underflow
                  "A:510=0_A:0xH000001=0_B:0xH000003=0_B:0xH000004=0_0xH000002>510")]
        [TestCase("bit1(0x000001) + bit2(0x000001) > bit3(0x000001) + bit4(0x000001)",
                  "bit1(0x000001) + bit2(0x000001) - bit3(0x000001) - bit4(0x000001) + 2 > 2", // underflow of 2 calculated
                  "A:2=0_A:0xN000001=0_B:0xP000001=0_B:0xQ000001=0_0xO000001>2")]
        [TestCase("bit1(0x000001) + bit2(0x000001) > bit3(0x000001) - bit4(0x000001)",
                  "bit1(0x000001) + bit2(0x000001) + bit4(0x000001) > bit3(0x000001)", // rearrange so single field on right
                  "A:0xN000001=0_A:0xO000001=0_0xQ000001>0xP000001")]
        [TestCase("bit1(0x000001) + bit2(0x000001) > bit3(0x000001) + bit4(0x000001) + 1",
                  "bit1(0x000001) + bit2(0x000001) - bit3(0x000001) - bit4(0x000001) + 2 > 3", // underflow of 2 calculated
                  "A:2=0_A:0xN000001=0_B:0xP000001=0_B:0xQ000001=0_0xO000001>3")]
        [TestCase("bit1(0x000001) + bit2(0x000001) < bit3(0x000001) + bit4(0x000001) + 1",
                  "bit1(0x000001) + bit2(0x000001) - bit3(0x000001) - bit4(0x000001) + 2 < 3", // underflow of 2 calculated
                  "A:2=0_A:0xN000001=0_B:0xP000001=0_B:0xQ000001=0_0xO000001<3")]
        [TestCase("bit1(0x000001) + bit2(0x000001) + 1 > bit3(0x000001) + bit4(0x000001) + 2",
                  "bit1(0x000001) + bit2(0x000001) - bit3(0x000001) - bit4(0x000001) + 2 > 3", // constants merged, then underflow of 2 applied
                  "A:2=0_A:0xN000001=0_B:0xP000001=0_B:0xQ000001=0_0xO000001>3")]
        [TestCase("bit1(0x000001) + bit2(0x000001) - bit3(0x000001) - bit4(0x000001) < 1",
                  "bit1(0x000001) + bit2(0x000001) - bit3(0x000001) - bit4(0x000001) + 2 < 3", // underflow of 2 calculated
                  "A:2=0_A:0xN000001=0_B:0xP000001=0_B:0xQ000001=0_0xO000001<3")]
        [TestCase("bit1(0x000001) + bit2(0x000001) + 2 - bit3(0x000001) - bit4(0x000001) < 3",
                  "bit1(0x000001) + bit2(0x000001) - bit3(0x000001) - bit4(0x000001) + 2 < 3", // underflow of 2 calculated
                  "A:2=0_A:0xN000001=0_B:0xP000001=0_B:0xQ000001=0_0xO000001<3")]
        [TestCase("byte(0x000001) + 1 - byte(0x000002) >= 2",
                  "byte(0x000001) - byte(0x000002) + 255 >= 256", // 254 added to both sides to prevent underflow
                  "A:255=0_B:0xH000002=0_0xH000001>=256")]
        [TestCase("byte(0x000001) + 1 - byte(0x000002) < 2",
                  "byte(0x000001) - byte(0x000002) + 1 < 2", // user-provided underflow adjustment kept
                  "A:1=0_B:0xH000002=0_0xH000001<2")]
        [TestCase("byte(0x000001) - prev(byte(0x000001)) >= 2",
                  "prev(byte(0x000001)) + 2 <= byte(0x000001)", // rearrange to avoid subtraction
                  "A:2=0_d0xH000001<=0xH000001")]
        [TestCase("prev(byte(0x000001)) - prev(byte(0x000002)) + prev(byte(0x000003)) < 2", // make sure the memory reference is seen inside the prev
                  "prev(byte(0x000001)) - prev(byte(0x000002)) + prev(byte(0x000003)) + 255 < 257", // overflow of 510 calculated
                  "A:255=0_A:d0xH000001=0_B:d0xH000002=0_d0xH000003<257")]
        [TestCase("prev(byte(0x000001)) == byte(0x000002) - 1",
                  "prev(byte(0x000001)) + 1 == byte(0x000002)", // rearrange to avoid subtraction
                  "A:1=0_d0xH000001=0xH000002")]
        [TestCase("byte(0x000001) - prev(byte(0x000001)) >= 100",
                  "prev(byte(0x000001)) + 100 <= byte(0x000001)", // rearrange to avoid subtraction
                  "A:100=0_d0xH000001<=0xH000001")]
        public void TestUnderflowComplex(string input, string expected, string expectedSerialized)
        {
            string left, op, right;
            ExpressionTests.SplitComparison(input, out left, out op, out right);

            var operation = ExpressionTests.GetComparisonOperation(op);
            var leftExpr = ExpressionTests.Parse(left);
            var rightExpr = ExpressionTests.Parse(right);
            var comparison = new ComparisonExpression(leftExpr, operation, rightExpr);

            Assert.That(comparison.Left, Is.InstanceOf<IComparisonNormalizeExpression>());
            var normalizing = (IComparisonNormalizeExpression)comparison.Left;
            var result = normalizing.NormalizeComparison(comparison.Right, comparison.Operation, true);

            if (expected == input)
            {
                Assert.That(result, Is.Null);
                result = comparison;
            }
            else
            {
                if (result == null)
                    result = comparison;
                ExpressionTests.AssertAppendString(result, expected);
            }

            var achievementBuilder = new ScriptInterpreterAchievementBuilder();
            achievementBuilder.PopulateFromExpression(result);
            var serialized = achievementBuilder.SerializeRequirements();
            Assert.That(serialized, Is.EqualTo(expectedSerialized));
        }

        [Test]
        public void TestUnderflowAdjustmentImpossible()
        {
            var input = "5 + byte(0x1234) == 2";
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));

            var scope = new InterpreterScope();
            scope.Context = new TriggerBuilderContext();
            scope.AddFunction(new MemoryAccessorFunction("byte", FieldSize.Byte));

            ExpressionBase result;
            Assert.That(expr.ReplaceVariables(scope, out result), Is.False);
            Assert.That(result, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)result).Message, Is.EqualTo("Expression can never be true"));
        }

        [Test]
        [TestCase("b0A + 0", null)] // +0 forces construction of MemoryValueExpression
        [TestCase("b0A + b1A", null)]
        [TestCase("b0A + b1A + b2A + b3A + b4A + b5A + b6A", null)] // incomplete
        [TestCase("b0A + b1A + b2A + b3A + b4A + b5A + b6A + b7A", "kA")] // in order
        [TestCase("b7A + b6A + b5A + b4A + b3A + b2A + b1A + b0A", "kA")] // reversed
        [TestCase("b1A + b6A + b4A + b3A + b7A + b0A + b5A + b2A", "kA")] // random order
        [TestCase("b0A + b1A + b2A + b3B + b4A + b5A + b6A + b7A", null)] // differing addresses
        [TestCase("b0A + b1A + b2A + b3A - b4A + b5A + b6A + b7A", null)] // contains a subtraction
        [TestCase("b0A + b1A + b2A + b3A + b4A + b5A + b6A + b7A + b0A + b1A + b2A + b3A + b4A + b5A + b6A + b7A", "kA + kA")] // doubled
        [TestCase("b0A + b1A + b2A + b3A + b4A + b5A + b6A + b7A + b0A + b1A + b2A + b4A + b5A + b6A + b7A", "kA + b0A + b1A + b2A + b4A + b5A + b6A + b7A")] // partial doubled
        [TestCase("b0A + b0A + b1A + b1A + b2A + b2A + b3A + b3A + b4A + b4A + b5A + b5A + b6A + b6A + b7A + b7A", "kA + kA")] // doubled alternating
        [TestCase("b6A + b7A + b0B + b1B + b2B + b3B + b4B + b5B + b6B + b7B + b0C + b1C", "b6A + b7A + kB + b0C + b1C")] // bitcount in the middle of multiple bytes
        [TestCase("db0A + db1A + db2A + db3A + db4A + db5A + db6A + db7A", "dkA")] // delta
        [TestCase("db0A + db1A + db2A + b3A + db4A + db5A + db6A + db7A", null)] // delta/non-delta mix
        [TestCase("b0I + b1I + b2I + b3I + b4I + b5I + b6I + b7I", "kI")] // with pointer
        [TestCase("b0I + b1I + b2J + b3I + b4I + b5I + b6I + b7I", null)] // differing pointers
        [TestCase("b0I + b1I + b2I + b3I + b4A + b5I + b6I + b7I", null)] // with a non-pointer
        [TestCase("b0A + b1A + b2A + ~b3A + b4A + b5A + b6A + b7A", null)] // partially inverted
        [TestCase("~b0A + ~b1A + ~b2A + ~b3A + ~b4A + ~b5A + ~b6A + ~b7A", null)] // wholly inverted
        public void TestMergeBitCount(string input, string normalized)
        {
            Func<string, string> replacePlaceholders = (string i) =>
            {
                var builder = new StringBuilder();
                bool wrapped = false;
                foreach (var c in i)
                {
                    switch (c)
                    {
                        default: builder.Append(c); break;
                        case 'b': builder.Append("bit"); break;
                        case 'd': builder.Append("prev("); wrapped = true; break;
                        case 'p': builder.Append("prior("); wrapped = true; break;
                        case 'k': builder.Append("bitcount"); break;
                        case '~': builder.Append('~'); break;
                        case 'A':
                        case 'B':
                        case 'C':
                        case 'D':
                            builder.Append("(0x00100");
                            builder.Append(c);
                            builder.Append(wrapped ? "))" : ")");
                            wrapped = false;
                            break;
                        case 'I':
                            builder.Append("(dword(0x005555) + 4106)"); // 4106 = 0x00100A
                            if (wrapped)
                            {
                                builder.Append(')');
                                wrapped = false;
                            }
                            break;
                        case 'J':
                            builder.Append("(dword(0x006666) + 4106)"); // 4106 = 0x00100A
                            if (wrapped)
                            {
                                builder.Append(')');
                                wrapped = false;
                            }
                            break;
                    }
                }
                return builder.ToString();
            };

            var expandedInput = replacePlaceholders(input);
            var accessor = TriggerExpressionTests.Parse<MemoryValueExpression>(expandedInput);

            var comparison = accessor.NormalizeComparison(new IntegerConstantExpression(1), ComparisonOperation.Equal, true);

            if (normalized == null)
            {
                Assert.That(comparison, Is.Null);
            }
            else
            {
                Assert.That(comparison, Is.InstanceOf<ComparisonExpression>());
                var normalizedAccessor = ((ComparisonExpression)comparison).Left;

                var expandedNormalized = replacePlaceholders(normalized);
                ExpressionTests.AssertAppendString(normalizedAccessor, expandedNormalized);
            }
        }

        [Test]
        [TestCase(ComparisonOperation.Equal, 8, true, "byte(0x000001) == 255")]
        [TestCase(ComparisonOperation.NotEqual, 8, true, "byte(0x000001) != 255")]
        [TestCase(ComparisonOperation.Equal, 0, true, "byte(0x000001) == 0")]
        [TestCase(ComparisonOperation.NotEqual, 0, true, "byte(0x000001) != 0")]
        [TestCase(ComparisonOperation.Equal, 4, true, "bitcount(0x000001) == 4")]
        [TestCase(ComparisonOperation.NotEqual, 4, true, "bitcount(0x000001) != 4")]
        [TestCase(ComparisonOperation.Equal, 8, false, "bitcount(0x000001) == 8")]
        [TestCase(ComparisonOperation.Equal, 0, false, "bitcount(0x000001) == 0")]
        [TestCase(ComparisonOperation.Equal, 4, false, "bitcount(0x000001) == 4")]

        // these will be further modified by range validation in RequirementConditionExpression
        [TestCase(ComparisonOperation.GreaterThanOrEqual, 8, true, "byte(0x000001) >= 255")] // = 255
        [TestCase(ComparisonOperation.GreaterThan, 8, true, "byte(0x000001) > 255")]         // false
        [TestCase(ComparisonOperation.LessThanOrEqual, 8, true, "byte(0x000001) <= 255")]    // true
        [TestCase(ComparisonOperation.LessThan, 8, true, "byte(0x000001) < 255")]            // != 255
        [TestCase(ComparisonOperation.Equal, 9, true, "bitcount(0x000001) == 9")]            // false
        [TestCase(ComparisonOperation.NotEqual, 9, true, "bitcount(0x000001) != 9")]         // true
        public void TestMergeBitCountLimits(ComparisonOperation comparisonOperation, int value, bool canModifyRight, string expected)
        {
            string input = ExpressionTests.ReplacePlaceholders("mA + nA + oA + pA + qA + rA + sA + tA", true);
            var accessor = TriggerExpressionTests.Parse<MemoryValueExpression>(input);

            var comparison = accessor.NormalizeComparison(new IntegerConstantExpression(value), comparisonOperation, canModifyRight);

            if (expected == null)
            {
                Assert.That(comparison, Is.Null);
            }
            else
            {
                Assert.That(comparison, Is.InstanceOf<ComparisonExpression>());
                ExpressionTests.AssertAppendString(comparison, expected);
            }
        }
    }
}