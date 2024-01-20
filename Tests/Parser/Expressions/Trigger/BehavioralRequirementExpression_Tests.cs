using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;

namespace RATools.Parser.Tests.Expressions.Trigger
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
        [TestCase("never(repeated(3, always_true()))", "R:1=1.3.")]
        [TestCase("never(byte(1) == 56 || byte(2) == 3)", "R:0xH000001=56_R:0xH000002=3")] // or clauses can be separated
        [TestCase("never(byte(1) == 56 && byte(2) == 3)", "N:0xH000001=56_R:0xH000002=3")] // and clauses cannot be separated
        [TestCase("unless(byte(0x001234) == 3)", "P:0xH001234=3")]
        [TestCase("unless(repeated(6, byte(1) == 56))", "P:0xH000001=56.6.")]
        [TestCase("unless(byte(1) == 56 || byte(2) == 3)", "P:0xH000001=56_P:0xH000002=3")] // or clauses can be separated
        [TestCase("unless(byte(1) == 56 && byte(2) == 3)", "N:0xH000001=56_P:0xH000002=3")] // and clauses cannot be separated
        [TestCase("trigger_when(byte(0x001234) == 3)", "T:0xH001234=3")]
        [TestCase("trigger_when(repeated(6, byte(1) == 56))", "T:0xH000001=56.6.")]
        [TestCase("trigger_when(byte(1) == 56 && byte(2) == 3)", "T:0xH000001=56_T:0xH000002=3")] // and clauses can be separated
        [TestCase("trigger_when(byte(1) == 56 || byte(2) == 3)", "O:0xH000001=56_T:0xH000002=3")] // or clauses cannot be separated
        [TestCase("trigger_when(always_false() || byte(2) == 3)", "T:0xH000002=3")] // always_false can be ignored
        [TestCase("trigger_when(repeated(6, byte(1) == 56) && unless(byte(2) == 3))", "T:0xH000001=56.6._P:0xH000002=3")] // PauseIf clause can be extracted
        [TestCase("trigger_when(repeated(6, byte(1) == 56) && never(byte(2) == 3))", "Z:0xH000002=3_T:0xH000001=56.6.")] // never clause outside repeated
        [TestCase("trigger_when(repeated(6, byte(1) == 56 && never(byte(2) == 3)))", "Z:0xH000002=3_T:0xH000001=56.6.")] // never clause inside repeated
        [TestCase("trigger_when(once(byte(1) == 56 && never(byte(2) == 3)))", "Z:0xH000002=3_T:0xH000001=56.1.")] // ResetNextIf clause should not be extracted
        [TestCase("trigger_when((byte(1) == 56 || byte(2) == 3) && (byte(1) == 55 || byte(2) == 4))",
            "O:0xH000001=56_T:0xH000002=3_O:0xH000001=55_T:0xH000002=4")] // and can be separated, but not nested ors
        [TestCase("trigger_when((byte(1) == 56 && byte(2) == 3) && (byte(1) == 55 || byte(2) == 4))",
            "T:0xH000001=56_T:0xH000002=3_O:0xH000001=55_T:0xH000002=4")] // and can be separated, but not nested ors
        [TestCase("never(0 + byte(1) + byte(2) == 56)", "A:0xH000001=0_R:0xH000002=56")]
        [TestCase("never(never(byte(1) == 56))", "R:0xH000001!=56")] // inner never can be inverted
        [TestCase("never(unless(byte(1) == 56))", "R:0xH000001!=56")] // inner unless can be inverted
        [TestCase("unless(never(byte(1) == 56))", "P:0xH000001!=56")] // inner never can be inverted
        [TestCase("unless(unless(byte(1) == 56))", "P:0xH000001!=56")] // inner unless can be inverted
        [TestCase("never(never(never(byte(1) == 56)))", "R:0xH000001=56")] // inner nevers can be inverted
        [TestCase("never(byte(2) == 34 && never(byte(1) == 56))", "N:0xH000002=34_R:0xH000001!=56")] // inner never can be inverted
        [TestCase("never(once(byte(0x1234) == 1) && repeated(12, always_true()))", "N:0xH001234=1.1._R:1=1.12.")]
        [TestCase("never(repeated(12, once(byte(0x1234) == 1) && always_true()))", "N:0xH001234=1.1._R:1=1.12.")]
        [TestCase("never(float(0x1111) == 2.0)", "R:fF001111=f2.0")]
        [TestCase("never(repeated(6, byte(1) == 56 && never(byte(2) == 3)))", "Z:0xH000002=3_R:0xH000001=56.6.")]
        [TestCase("unless(repeated(6, byte(1) == 56 && never(byte(2) == 3)))", "Z:0xH000002=3_P:0xH000001=56.6.")]
        public void TestBuildTrigger(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<BehavioralRequirementExpression>(input);
            TriggerExpressionTests.AssertSerialize(clause, expected);
        }

        [Test]
        [TestCase("never(6+2)", "integerconstant")] // numeric
        [TestCase("never(byte(0x1234))", "memoryaccessor")] // no comparison
        [TestCase("never(f)", "variable")] // function reference
        [TestCase("unless(6+2)", "integerconstant")] // numeric
        [TestCase("unless(byte(0x1234))", "memoryaccessor")] // no comparison
        [TestCase("unless(f)", "variable")] // function reference
        [TestCase("trigger_when(6+2)", "integerconstant")] // numeric
        [TestCase("trigger_when(byte(0x1234))", "memoryaccessor")] // no comparison
        [TestCase("trigger_when(f)", "variable")] // function reference
        public void TestUnsupportedComparisons(string input, string unsupportedType)
        {
            var scope = TriggerExpressionTests.CreateScope();
            scope.AssignVariable(new VariableExpression("f"), new FunctionReferenceExpression("f2"));

            TriggerExpressionTests.AssertParseError(input, scope, "Cannot convert " + unsupportedType + " to requirement");
        }

        [Test]
        [TestCase("never(A > 1)", "never(A > 2)", ConditionalOperation.And, "never(A > 1)")]
        [TestCase("never(A > 3)", "A <= 3", ConditionalOperation.And, "never(A > 3)")] // all values of A behave the same in both
        [TestCase("never(A > 4)", "A <= 3", ConditionalOperation.And, null)] // 4 doesn't activate either clause
        [TestCase("never(A > 5)", "A <= 6", ConditionalOperation.And, null)] // 6 activates both clauses
        [TestCase("unless(A > 1)", "unless(A > 2)", ConditionalOperation.And, "unless(A > 1)")] // unless behaves like never, so tests aren't duplicated
        [TestCase("trigger_when(A > 1)", "trigger_when(A > 2)", ConditionalOperation.And, "trigger_when(A > 2)")]
        [TestCase("trigger_when(A > 2)", "A > 2", ConditionalOperation.And, "trigger_when(A > 2)")] // non-trigger clause is redundant
        [TestCase("trigger_when(A > 2)", "A > 3", ConditionalOperation.And, null)] // challenge would never show - for non-trigger to be true, trigger would also be true
        [TestCase("trigger_when(A > 3)", "A > 2", ConditionalOperation.And, null)] // challenge could show for 3
        public void TestLogicalIntersect(string left, string right, ConditionalOperation op, string expected)
        {
            TriggerExpressionTests.AssertLogicalIntersect(left, right, op, expected);
        }

        [Test]
        public void TestNeverNeverWithHitTarget()
        {
            // the hit target will prevent the optimizer from inverting the inner logic,
            // so the inner clause will retain its ResetIf. to prevent a discrepency with
            // the 'never(never(A) == X)' test where the logic is inverted, generate an error.
            var input = "never(never(byte(0x1234) == 56 && once(byte(0x2345) == 67)))";
            TriggerExpressionTests.AssertBuildTriggerError(input, "Cannot apply never to condition already flagged with never");
        }

        [Test]
        public void TestTriggerAlts()
        {
            // too complex for a subclause, but still valid for an achievement
            var input = "trigger_when((byte(1) == 56 && byte(2) == 3) || (byte(1) == 55 && byte(2) == 4))";
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);

            var expected = "1=1ST:0xH000001=56_T:0xH000002=3ST:0xH000001=55_T:0xH000002=4";
            TriggerExpressionTests.AssertSerializeAchievement(clause, expected);
        }

        [Test]
        public void TestTriggerFalseCore()
        {
            // trigger_when(always_false()) at top level makes the entire top-level clause impossible
            // which makes the entire achievement impossible.
            var input = "trigger_when(always_false()) && byte(0x1234) == 2";
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);
            TriggerExpressionTests.AssertSerializeAchievement(clause, "0=1");
        }

        [Test]
        public void TestTriggerFalseInAlt()
        {
            // trigger_when(always_false()) in an alt makes that alt impossible, but not the entire
            // achievement. this behavior can be used to show a trigger icon while a measured counter
            // is eligible to increment. for example:
            //
            //    Alt1 = measured(tally_of(event1(), event2(), event3()))
            //    Alt2 = trigger_when(always_false()) && event1_eligible()
            //    Alt3 = trigger_when(always_false()) && event2_eligible()
            //    Alt4 = trigger_when(always_false()) && event3_eligible()
            //
            // that would show the trigger icon while any of the three events was eligible, then the
            // measured would increment when each individual event was completed and the alt containing
            // the measured would be true when all three events were completed.
            //
            // TODO: consider implementing a helper function for this
            //
            //    measured_tally_of({
            //        event1_eligible() => event1(),
            //        event2_eligible() => event2(),
            //        event3_eligible() => event3(),
            //    })
            //
            var input = "byte(0x1234) == 1 && (" +
                         "measured(byte(0x2345) == 56) || " +
                         "(trigger_when(always_false()) && byte(0x3456) == 2)" +
                        ")";
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);
            TriggerExpressionTests.AssertSerializeAchievement(clause, "0xH001234=1SM:0xH002345=56ST:0=1_0xH003456=2");
        }

        [Test]
        public void TestTriggerWhenMeasured()
        {
            var input = "byte(0x1234) == 1 && trigger_when(measured(repeated(3, byte(0x2345) == 6)))";
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);
            TriggerExpressionTests.AssertSerializeAchievement(clause, "0xH001234=1SM:0xH002345=6.3.ST:0=1");
        }
    }
}
