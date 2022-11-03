using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;

namespace RATools.Tests.Parser.Expressions.Trigger
{
    [TestFixture]
    class BehavioralRequirementExpressionTests
    {
        [Test]
        [TestCase("never(byte(0x001234) == 3)")]
        [TestCase("never(byte(0x001234) == 3 && byte(0x002345) > 7)")]
        [TestCase("unless(byte(0x001234) == 3)")]
        [TestCase("trigger_when(byte(0x001234) == 3)")]
        public void TestAppendString(string input)
        {
            var clause = TriggerExpressionTests.Parse<BehavioralRequirementExpression>(input);
            ExpressionTests.AssertAppendString(clause, input);
        }

        [Test]
        [TestCase("never(byte(0x001234) == 3)", "R:0xH001234=3")]
        [TestCase("never(byte(0x001234) == 3 && byte(0x002345) > 7)", "N:0xH001234=3_R:0xH002345>7")]
        [TestCase("never(repeated(6, byte(1) == 56))", "R:0xH000001=56.6.")]
        [TestCase("never(byte(1) == 56 || byte(2) == 3)", "R:0xH000001=56_R:0xH00002=3")] // or clauses can be separated
        [TestCase("never(byte(1) == 56 && byte(2) == 3)", "N:0xH000001=56_R:0xH00002=3")] // and clauses cannot be separated
        [TestCase("unless(byte(0x001234) == 3)", "P:0xH001234=3")]
        [TestCase("unless(repeated(6, byte(1) == 56))", "P:0xH000001=56.6.")]
        [TestCase("unless(byte(1) == 56 || byte(2) == 3)", "P:0xH000001=56_P:0xH000002=3")] // or clauses can be separated
        [TestCase("unless(byte(1) == 56 && byte(2) == 3)", "N:0xH000001=56_P:0xH000002=3")] // and clauses cannot be separated
        [TestCase("trigger_when(byte(0x001234) == 3)", "T:0xH001234=3")]
        [TestCase("trigger_when(repeated(6, byte(1) == 56))", "T:0xH000001=56.6.")]
        [TestCase("trigger_when(byte(1) == 56 && byte(2) == 3)", "T:0xH000001=56_T:0xH000002=3")] // and clauses can be separated
        [TestCase("trigger_when(byte(1) == 56 || byte(2) == 3)", "O:0xH000001=56_T:0xH000002=3")] // or clauses cannot be separated
        [TestCase("trigger_when(repeated(6, byte(1) == 56) && unless(byte(2) == 3))", "T:0xH000001=56.6._P:0xH000002=3")] // PauseIf clause can be extracted
        [TestCase("trigger_when(repeated(6, byte(1) == 56) && never(byte(2) == 3))", "T:0xH000001=56.6._R:0xH000002=3")] // ResetIf clause can be extracted
        [TestCase("trigger_when(repeated(6, byte(1) == 56 && never(byte(2) == 3)))", "Z:0xH000002=3_T:0xH000001=56.6.")] // ResetNextIf clause should not be extracted
        [TestCase("trigger_when(once(byte(1) == 56 && never(byte(2) == 3)))", "Z:0xH000002=3_T:0xH000001=56.1.")] // ResetNextIf clause should not be extracted
        [TestCase("trigger_when((byte(1) == 56 && byte(2) == 3) || (byte(1) == 55 && byte(2) == 4))",
            "trigger_when(byte(0x000001) == 56) && trigger_when(byte(0x000002) == 3) || trigger_when(byte(0x000001) == 55) && trigger_when(byte(0x000002) == 4)")] // or with ands can be separated
        [TestCase("trigger_when((byte(1) == 56 || byte(2) == 3) && (byte(1) == 55 || byte(2) == 4))",
            "trigger_when(byte(0x000001) == 56 || byte(0x000002) == 3) && trigger_when(byte(0x000001) == 55 || byte(0x000002) == 4)")] // and can be separated, but not nested ors
        [TestCase("trigger_when((byte(1) == 56 && byte(2) == 3) && (byte(1) == 55 || byte(2) == 4))",
            "trigger_when(byte(0x000001) == 56) && trigger_when(byte(0x000002) == 3) && trigger_when(byte(0x000001) == 55 || byte(0x000002) == 4)")] // and can be separated, but not nested ors
        [TestCase("never(0 + byte(1) + byte(2) == 56)", "A:0xH000001_R:0xH000002=56")]
        [TestCase("never(never(byte(1) == 56))", "R:0xH000001!=56")] // inner never optimized to 'byte(1) != 56', then outer never applied
        [TestCase("never(unless(byte(1) == 56))", "R:0xH000001!=56")] // inner unless optimized to 'byte(1) != 56', then outer never applied
        public void TestBuildTrigger(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<BehavioralRequirementExpression>(input);
            TriggerExpressionTests.AssertSerialize(clause, expected);
        }

        [Test]
        [TestCase("never(6+2)")] // numeric
        [TestCase("never(byte(0x1234))")] // no comparison
        [TestCase("never(f)")] // function reference
        [TestCase("unless(5, 6+2)")] // numeric
        [TestCase("unless(5, byte(0x1234))")] // no comparison
        [TestCase("unless(5, f)")] // function reference
        [TestCase("trigger_when(5, 6+2)")] // numeric
        [TestCase("trigger_when(5, byte(0x1234))")] // no comparison
        [TestCase("trigger_when(5, f)")] // function reference
        public void TestUnsupportedComparisons(string input)
        {
            var scope = TriggerExpressionTests.CreateScope();
            scope.AssignVariable(new VariableExpression("f"), new FunctionReferenceExpression("f2"));

            TriggerExpressionTests.AssertParseError(input, scope, "comparison did not evaluate to a valid comparison");
        }

        public void TestNeverNeverWithHitTarget()
        {
            // the hit target will prevent the optimizer from inverting the inner logic,
            // so the inner clause will retain its ResetIf. to prevent a discrepency with
            // the 'never(never(A) == X)' test where the logic is inverted, generate an error.
            var input = "never(never(byte(0x1234) == 56 && once(byte(0x2345) == 67)))";
            TriggerExpressionTests.AssertBuildTriggerError(input, "Cannot apply 'never' to condition already flagged with ResetIf");
        }
    }
}
