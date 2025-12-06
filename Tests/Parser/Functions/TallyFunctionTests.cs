using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using RATools.Parser.Tests.Expressions;
using RATools.Parser.Tests.Expressions.Trigger;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class TallyFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new TallyFunction();
            Assert.That(def.Name.Name, Is.EqualTo("tally"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("count"));
        }

        [Test]
        [TestCase("tally(5, byte(0x001234) == 56, byte(0x001234) == 67)")]
        [TestCase("tally(8, byte(0x001234) == 56, byte(0x001234) == 67, byte(0x001234) == 78, byte(0x001234) == 89)")]
        [TestCase("tally(10, once(byte(0x001234) == 56), byte(0x001234) == 67)")]
        [TestCase("tally(13, byte(0x001234) == 56 && byte(0x002345) == 67, byte(0x001234) == 56 && byte(0x002345) == 45)")]
        [TestCase("tally(15, byte(dword(0x002345) + 0x30) == 56, byte(dword(0x002345) + 0x20) == 34)")]
        [TestCase("tally(16, byte(0x001234) + byte(0x002345) == 56, byte(0x001234) - byte(0x002345) == 34)")]
        [TestCase("tally(18, byte(0x001234) == 56, deduct(byte(0x002345) == 99))")]
        [TestCase("tally(19, byte(0x001234) == 56, deduct(byte(0x002345) == 99), byte(0x005555) == 0, deduct(byte(0x005555) == 1))")]
        public void TestAppendString(string input)
        {
            var clause = TriggerExpressionTests.Parse<TalliedRequirementExpression>(input);
            ExpressionTests.AssertAppendString(clause, input);
        }

        [Test]
        [TestCase("tally(02, byte(0x1234) == 56)", "0xH001234=56.2.")] // tally of one item is just repeated
        [TestCase("tally(03, byte(0x1234) == 56 && byte(0x2345) == 67)", "N:0xH001234=56_0xH002345=67.3.")] // tally of one item is just repeated
        [TestCase("tally(04, byte(0x1234) == 56 || byte(0x2345) == 67)", "O:0xH001234=56_0xH002345=67.4.")] // tally of one item is just repeated
        [TestCase("tally(05, byte(0x1234) == 56, byte(0x1234) == 67)", "C:0xH001234=56_0xH001234=67.5.")]
        [TestCase("tally(07, once(byte(0x1234) == 56 || once(byte(0x1234) == 67)), once(byte(0x1234) == 78))",
            "O:0xH001234=67.1._C:0xH001234=56.1._C:0xH001234=78.1._0=1.7.")] // OrNext should be preserved within a tallied item
        [TestCase("tally(08, byte(0x1234) == 56, byte(0x1234) == 67, byte(0x1234) == 78, byte(0x1234) == 89)",
            "C:0xH001234=56_C:0xH001234=67_C:0xH001234=78_0xH001234=89.8.")]
        [TestCase("tally(09, [byte(0x1234) == 56, byte(0x1234) == 67])",
            "C:0xH001234=56_0xH001234=67.9.")] // array support
        [TestCase("tally(10, once(byte(0x1234) == 56), byte(0x1234) == 67)",
            "C:0xH001234=56.1._0xH001234=67.10.")] // only first restricted
        [TestCase("tally(11, byte(0x1234) == 56, once(byte(0x1234) == 67))",
            "C:0xH001234=67.1._0xH001234=56.11.")] // only second restricted, bubble up first
        [TestCase("tally(12, once(byte(0x1234) == 56), once(byte(0x1234) == 67))",
            "C:0xH001234=56.1._C:0xH001234=67.1._0=1.12.")] // both restricted, add always_false
        [TestCase("tally(13, byte(0x1234) == 56 && byte(0x2345) == 67, byte(0x1234) == 56 && byte(0x2345) == 45)",
            "N:0xH001234=56_C:0xH002345=67_N:0xH001234=56_0xH002345=45.13.")] // multiple AndNext clauses
        [TestCase("tally(14, (byte(0x1234) == 56 && byte(0x2345) == 67) || byte(0x2345) == 67)",
            "0xH002345=67.14.")] // redundant subclause is eliminated
        [TestCase("tally(15, byte(0x1234 + dword(0x2345)) == 56, byte(0x1234 + dword(0x2345)) == 34)",
            "I:0xX002345_C:0xH001234=56_I:0xX002345_0xH001234=34.15.")] // AddAddress
        [TestCase("tally(16, byte(0x1234) + byte(0x2345) == 56, byte(0x1234) - byte(0x2345) == 34)",
            "A:0xH001234=0_C:0xH002345=56_B:0xH002345=0_0xH001234=34.16.")] // AddSource/SubSource
        [TestCase("tally(17, repeated(6, byte(0x1234) == 5 || byte(0x2345) == 6), byte(0x1234) == 34)",
            "O:0xH001234=5_C:0xH002345=6.6._0xH001234=34.17.")] // with nested repeated()
        [TestCase("tally(18, byte(0x1234) == 56, deduct(byte(0x2345) == 99))",
            "D:0xH002345=99_C:0xH001234=56_0=1.18.")] // simple deduct
        [TestCase("tally(19, byte(0x1234) == 56, deduct(byte(0x2345) == 99), byte(0x5555) == 0, deduct(byte(0x5555) == 1))",
            "C:0xH001234=56_D:0xH002345=99_D:0xH005555=1_C:0xH005555=0_0=1.19.")] // mutiple deducts moved after adds
        [TestCase("tally(20, byte(0x1234) == 56, deduct(repeated(10, byte(0x2345) == 99)))",
            "D:0xH002345=99.10._C:0xH001234=56_0=1.20.")] // deduct repeated
        [TestCase("tally(21, repeated(10, byte(0x1234) == 56), deduct(repeated(10, byte(0x2345) == 99)))",
            "C:0xH001234=56.10._D:0xH002345=99.10._0=1.21.")] // deduct and non-deduct repeated
        [TestCase("tally(22, byte(0x1234) == 56, always_true(), deduct(always_true()), byte(0x1234) == 78)",
            "C:0xH001234=56_C:1=1_D:1=1_C:0xH001234=78_0=1.22.")] // always_trues are not ignored
        [TestCase("tally(23, byte(0x1234) == 1 && never(byte(0x2345) == 2 && byte(0x3456) == 3))",
            "N:0xH002345=2_Z:0xH003456=3_0xH001234=1.23.")]
        [TestCase("tally(24, byte(0x1234) == 1 && once(byte(0x1234) == 2))",
            "N:0xH001234=2.1._0xH001234=1.24.")] // singular clause - rearrange so always_false() isn't needed for total
        [TestCase("tally(25, byte(0x1234) == 1, byte(0x1235) == 1 && once(byte(0x1235) == 2))",
            "N:0xH001235=1_C:0xH001235=2.1._0xH001234=1.25.")] // multiple clauses - can rearrange the clauses themselves
        [TestCase("tally(26, byte(0x1234) == 1 && once(byte(0x1234) == 2), byte(0x1235) == 1 && once(byte(0x1235) == 2))",
            "N:0xH001234=1_C:0xH001234=2.1._N:0xH001235=1_C:0xH001235=2.1._0=1.26.")] // multiple clauses - don't rearrage within the clauses; use always_false() for total
        [TestCase("tally(27, byte(0x10) & 0x02 == 0x02)", "A:0xH000010&2_0=2.27.")]
        public void TestBuildTrigger(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<TalliedRequirementExpression>(input);
            TriggerExpressionTests.AssertSerialize(clause, expected);
        }

        [Test]
        [TestCase("tally(4, always_true())", "repeated(4, always_true())")]
        [TestCase("tally(4, always_false())", "always_false()")]
        [TestCase("tally(8, byte(0x1234) == 56, always_false(), deduct(always_false()), byte(0x1234) == 78)",
            "tally(8, byte(0x001234) == 56, byte(0x001234) == 78)")] // always_falses are ignored
        public void TestOptimize(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<TalliedRequirementExpression>(input);
            var optimized = clause.Optimize(new TriggerBuilderContext());
            ExpressionTests.AssertAppendString(optimized, expected);
        }

        [Test]
        [TestCase("once(A > 1)", "once(A > 2)", ConditionalOperation.And, "once(A > 1)")]
        [TestCase("repeated(3, A > 1)", "repeated(3, A > 2)", ConditionalOperation.And, "repeated(3, A > 1)")] // same hit target, keep more restrictive clause
        [TestCase("repeated(3, A > 1)", "repeated(2, A > 2)", ConditionalOperation.And, "repeated(2, A > 1)")] // keep more restrictive if hit target is higher
        [TestCase("repeated(3, A > 1)", "repeated(4, A > 2)", ConditionalOperation.And, null)]                 // can't keep more restrictive if hit target is lower
        public void TestLogicalIntersect(string left, string right, ConditionalOperation op, string expected)
        {
            TriggerExpressionTests.AssertLogicalIntersect(left, right, op, expected);
        }

        [Test]
        public void TestZeroCount()
        {
            TriggerExpressionTests.AssertBuildTriggerError(
                "tally(0, byte(0x1234) == 56, byte(0x1234) == 67)",
                "Unbounded count is only supported in measured value expressions");
        }

        [Test]
        public void TestZeroCountValue()
        {
            var clause = TriggerExpressionTests.Parse<TalliedRequirementExpression>("tally(0, byte(0x1234) == 56, byte(0x1234) == 67)");
            TriggerExpressionTests.AssertSerializeValue(clause, "C:0xH001234=56_0xH001234=67");
        }

        [Test]
        public void TestZeroCountAndNext()
        {
            TriggerExpressionTests.AssertBuildTriggerError(
                "tally(0, byte(0x1234) == 56 && byte(0x1234) == 67)",
                "Unbounded count is only supported in measured value expressions");
        }

        [Test]
        public void TestNegativeCount()
        {
            TriggerExpressionTests.AssertParseError(
                "tally(-1, byte(0x1234) == 56, byte(0x1234) == 67)",
                "count must be greater than or equal to zero");
        }

        [Test]
        [TestCase("tally(4)")]
        [TestCase("tally(4, [])")]
        public void TestNoConditions(string input)
        {
            TriggerExpressionTests.AssertBuildTriggerError(input, "tally requires at least one non-deducted item");
        }

        [Test]
        public void TestOnlyDeduct()
        {
            TriggerExpressionTests.AssertBuildTriggerError(
                "tally(4, deduct(byte(0x2345) == 99), deduct(byte(0x2345) == 99))",
                "tally requires at least one non-deducted item");
        }

        [Test]
        [TestCase("tally(4, tally(6, byte(0x1234) == 5, byte(0x2345) == 6), byte(0x1234) == 34)", "tally")]
        [TestCase("tally(4, never(byte(0x1234) == 5) || once(byte(0x1234) == 34))", "never")]
        [TestCase("tally(4, measured(repeated(6, byte(0x1234) == 5)) || byte(0x1234) == 34)", "measured")]
        public void TestUnsupportedFlags(string input, string unsupported)
        {
            TriggerExpressionTests.AssertParseError(input, unsupported + " not allowed in subclause");
        }

        [Test]
        public void TestAndChain()
        {
            TriggerExpressionTests.AssertParseError(
                "tally(4, once(byte(0x2345) == 99) && once(byte(0x2345) == 99))",
                "Cannot tally subclause with multiple hit targets");

            // nested once() is okay. outer once will be combined with outer repeated
            var input = "tally(4, once(byte(0x1234) == 99 && once(byte(0x2345) == 99)))";
            var clause = TriggerExpressionTests.Parse<TalliedRequirementExpression>(input);
            TriggerExpressionTests.AssertSerialize(clause, "N:0xH002345=99.1._0xH001234=99.4.");

            // nested once() is okay. inner repeated will be combined with outer repeated
            input = "tally(4, repeated(2, byte(0x1234) == 99 && once(byte(0x2345) == 99)))";
            clause = TriggerExpressionTests.Parse<TalliedRequirementExpression>(input);
            TriggerExpressionTests.AssertSerialize(clause, "N:0xH002345=99.1._0xH001234=99.8.");
        }

        [Test]
        public void TestOrChain()
        {
            TriggerExpressionTests.AssertParseError(
                "tally(4, once(byte(0x2345) == 99) || once(byte(0x2345) == 99))",
                "Cannot tally subclause with multiple hit targets");
        }
    }
}
