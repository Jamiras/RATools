using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;

namespace RATools.Parser.Tests.Expressions.Trigger
{
    [TestFixture]
    class RequirementClauseExpressionTests
    {
        [Test]
        [TestCase("byte(0x001234) == 3 && byte(0x002345) == 4")]
        [TestCase("byte(0x001234) == 3 || byte(0x002345) == 4")]
        [TestCase("byte(0x001234) == 3 && byte(0x002345) == 4 && byte(0x003456) == 5")]
        [TestCase("byte(0x001234) == 3 || byte(0x002345) == 4 || byte(0x003456) == 5")]
        public void TestAppendString(string input)
        {
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);
            ExpressionTests.AssertAppendString(clause, input);
        }
        
        [Test]
        [TestCase("byte(0x001234) == 3 && byte(0x002345) == 4", "0xH001234=3_0xH002345=4")]
        [TestCase("byte(0x001234) == 3 || byte(0x002345) == 4", "O:0xH001234=3_0xH002345=4")]
        [TestCase("byte(0x001234) == 3 && byte(0x002345) == 4 && byte(0x003456) == 5", "0xH001234=3_0xH002345=4_0xH003456=5")]
        [TestCase("byte(0x001234) == 3 || byte(0x002345) == 4 || byte(0x003456) == 5", "O:0xH001234=3_O:0xH002345=4_0xH003456=5")]
        [TestCase("bit6(0x00627E) == 1 && prev(bit6(0x00627E)) == 0", "0xS00627e=1_d0xS00627e=0")]
        [TestCase("always_true() && byte(0x001234) == 0", "0xH001234=0")] // always_true() is redundant
        [TestCase("always_false() && byte(0x001234) == 0", "0=1")] // always_false() makes everything else redundant
        [TestCase("always_false() && never(byte(0x001234) == 0)", "0=1_R:0xH001234=0")] // always_false() ANDed with reset is not eliminated
        [TestCase("always_false() && unless(byte(0x001234) == 0)", "0=1_P:0xH001234=0")] // always_false() ANDed with pause is not eliminated
        [TestCase("always_false() || byte(0x001234) == 0", "0xH001234=0")] // always_false is redundant
        [TestCase("always_true() || byte(0x001234) == 0", "1=1")] // always_true() makes everything else redundant
        [TestCase("always_true() || never(byte(0x001234) == 0)", "R:0xH001234=0")] // discard always_true() when reset is kept
        [TestCase("always_true() || unless(byte(0x001234) == 0)", "1=1")] // always_true() makes everything else redundant
        public void TestBuildTrigger(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);
            TriggerExpressionTests.AssertSerialize(clause, expected);
        }

        [Test]
        [TestCase("byte(0x000028) == 12 && word(0x000042) == 25959 || byte(0x0062AF) != 0 || word(0x0062AD) >= 10000",
                  "1=1S0xH000028=12_0x 000042=25959S0xH0062af!=0S0x 0062ad>=10000")] // no parentheses - && ties first condition to second, add always_true core
        [TestCase("byte(0x000028) == 12 && (word(0x000042) == 25959 || byte(0x0062AF) != 0 || word(0x0062AD) >= 10000)",
                  "0xH000028=12S0x 000042=25959S0xH0062af!=0S0x 0062ad>=10000")] // parenthesis ensure first condition separate from alts
        [TestCase("byte(0x002345) == 5 && (byte(0x001234) == 1 || byte(0x001234) == 2)",
                  "0xH002345=5S0xH001234=1S0xH001234=2")] // alts created
        [TestCase("byte(0x002345) == 5 && __ornext(byte(0x001234) == 1 || byte(0x001234) == 2)",
                  "0xH002345=5_O:0xH001234=1_0xH001234=2")] // forced OrNext chain
        [TestCase("once(byte(0x1234) == 1) && (always_false() || never(byte(0x2345) == 1))",
                  "0xH001234=1.1._R:0xH002345=1")] // ResetIf can be collapsed into the main group
        [TestCase("once(byte(0x1234) == 1) && unless(byte(0x1234) == 2) && (always_false() || never(byte(0x2345) == 1))",
                  "0xH001234=1.1._P:0xH001234=2SR:0xH002345=1")] // PauseIf in core keeps ResetIf in alt
        [TestCase("once(byte(0x1234) == 1) && (always_true() || (always_false() && never(byte(0x2345) == 2) && unless(byte(0x3456) == 3)))",
                  "0xH001234=1.1.S1=1S0=1_R:0xH002345=2_P:0xH003456=3")] // always_true()/always_false() forced alt groups
        [TestCase("once(byte(0x1234) == 1) && (always_true() || (never(byte(0x2345) == 2) && unless(byte(0x3456) == 3)))",
                  "0xH001234=1.1.S1=1SR:0xH002345=2_P:0xH003456=3")] // always_true()/unless forced alt groups
        [TestCase("unless(byte(0x3456) == 3) && once(byte(0x1234) == 1) && (always_true() || never(byte(0x2345) == 2))",
                  "P:0xH003456=3_0xH001234=1.1.S1=1SR:0xH002345=2")] // always_true()/unless forced alt groups
        [TestCase("byte(0x001234) == 3 && (byte(0x002345) == 4 || (byte(0x002345) >= 7 && byte(0x002345) <= 10))",
                  "0xH001234=3S0xH002345=4S0xH002345>=7_0xH002345<=10")]
        [TestCase("byte(0x001234) == 3 && (byte(word(0x002345) + 8) == 4 || (byte(word(0x002345) + 8) >= 7 && byte(word(0x002345) + 8) <= 10))",
                  "0xH001234=3SI:0x 002345_0xH000008=4SI:0x 002345_0xH000008>=7_I:0x 002345_0xH000008<=10")]
        [TestCase("byte(0x001234) == 3 && trigger_when(byte(word(0x002345) + 8) == 4 || (byte(word(0x002345) + 8) >= 7 && byte(word(0x002345) + 8) <= 10))",
                  "0xH001234=3SI:0x 002345_T:0xH000008=4SI:0x 002345_T:0xH000008>=7_I:0x 002345_T:0xH000008<=10")]
        public void TestBuildAchievement(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);
            TriggerExpressionTests.AssertSerializeAchievement(clause, expected);
        }

        [Test]
        [TestCase("byte(0x001234) == 3 && byte(0x002345) == 4", "N:0xH001234=3_0xH002345=4")]
        [TestCase("byte(0x001234) == 3 || byte(0x002345) == 4", "O:0xH001234=3_0xH002345=4")]
        [TestCase("byte(0x001234) == 3 && (byte(0x002345) == 4 || byte(0x003456) == 5)", 
                  "O:0xH002345=4_N:0xH003456=5_0xH001234=3")]
        [TestCase("byte(0x001234) == 3 && ((byte(0x001234) == 3 && byte(0x002345) == 4) || byte(0x003456) == 5)",
                  "O:0xH002345=4_N:0xH003456=5_0xH001234=3")] // common subclause should be eliminated
        [TestCase("byte(0x001234) == 3 && (byte(0x001234) == 3 || byte(0x003456) == 5)",
                  "0xH001234=3")] // common subclause will become always_true and eliminate the other subclause
        [TestCase("byte(0x001234) == 3 && byte(0x002345) == 4 && ((byte(0x001234) == 3 && byte(0x002345) == 4) || byte(0x003456) == 5)",
                  "N:0xH001234=3_0xH002345=4")] // common subclause will become always_true and eliminate the other subclause
        public void TestBuildSubclauseTrigger(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);

            var context = new AchievementBuilderContext();
            var result = clause.BuildSubclauseTrigger(context);
            Assert.That(result, Is.Null);

            Assert.That(context.Achievement.SerializeRequirements(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase("A == 1 || B == 1 || always_true()", "always_true()")]
        [TestCase("A == 1 || always_true() || C== 1", "always_true()")]
        [TestCase("always_true() || B == 1 || C== 1", "always_true()")]
        [TestCase("A == 1 || B == 1 || C == 1", "A == 1 || B == 1 || C == 1")]
        [TestCase("A == 1 || B == 1 || always_false()", "A == 1 || B == 1")]
        [TestCase("A == 1 || always_false() || C == 1", "A == 1 || C == 1")]
        [TestCase("always_false() || B == 1 || C == 1", "B == 1 || C == 1")]
        [TestCase("A == 1 && B == 1 && C == 1", "A == 1 && B == 1 && C == 1")]
        [TestCase("A == 1 && B == 1 && always_true()", "A == 1 && B == 1")]
        [TestCase("A == 1 && always_true() && C == 1", "A == 1 && C == 1")]
        [TestCase("always_true() && B == 1 && C == 1", "B == 1 && C == 1")]
        [TestCase("A == 1 && B == 1 && always_false()", "always_false()")]
        [TestCase("A == 1 && always_false() && C == 1", "always_false()")]
        [TestCase("always_false() && B == 1 && C == 1", "always_false()")]
        [TestCase("A == 1 || B == 1 || A == 1", "A == 1 || B == 1")]
        [TestCase("A == 1 && B == 1 && A == 1", "A == 1 && B == 1")]
        [TestCase("A == 1 && B == 1 && A == 3", "always_false()")]
        [TestCase("A > 1 && B == 1 && A == 3", "B == 1 && A == 3")]
        [TestCase("(A == 1 && B == 1) || (A == 1 && B == 2) || (A == 1 && B == 3)", "A == 1 && (B == 1 || B == 2 || B == 3)")]
        [TestCase("(A == 1 || B != 1) && (A == 1 || B != 2) && (A == 1 || B != 3)", "A == 1 || (B != 1 && B != 2 && B != 3)")]
        [TestCase("A == 1 && ((once(B == 1 && C == 1) && D == 1) || (once(B == 2 && C == 1) && D == 1))",
            // "D == 1" can be extracted from the alts, but  don't collapse
            // "(once(B == 1 && C == 1) || once(B == 2 && C == 1))" to
            // "once((B == 1 || B == 2) && C == 1)" when processing a subclause
            "A == 1 && D == 1 && (once(B == 1 && C == 1) || once(B == 2 && C == 1))")]
        public void TestOptimize(string input, string expected)
        {
            input = ExpressionTests.ReplacePlaceholders(input);
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);
            var optimized = clause.Optimize(new TriggerBuilderContext());

            expected = ExpressionTests.ReplacePlaceholders(expected);
            ExpressionTests.AssertAppendString(optimized, expected);
        }

        [Test]
        [TestCase("A == 1 || B == 1", "A == 1", ConditionalOperation.And, "A == 1")]
        [TestCase("A == 1 || B == 1", "A == 1", ConditionalOperation.Or, "A == 1 || B == 1")]
        [TestCase("A == 1 || B == 1 || C == 1", "A == 1 || B == 1", ConditionalOperation.And, "A == 1 || B == 1")]
        [TestCase("A == 1 || B == 1 || C == 1", "A == 1 || B == 1", ConditionalOperation.Or, "A == 1 || B == 1 || C == 1")]
        [TestCase("A == 1 || B == 1", "A == 1 || C == 1", ConditionalOperation.And, "A == 1 || (B == 1 && C == 1)")]
        [TestCase("A == 1 && B == 1", "A == 1", ConditionalOperation.And, "A == 1 && B == 1")]
        [TestCase("A == 1 && B == 1", "A == 1", ConditionalOperation.Or, "A == 1")]
        [TestCase("A == 1 && B == 1 && C == 1", "A == 1 && B == 1", ConditionalOperation.And, "A == 1 && B == 1 && C == 1")]
        [TestCase("A == 1 && B == 1 && C == 1", "A == 1 && B == 1", ConditionalOperation.Or, "A == 1 && B == 1")]
        [TestCase("A == 1 && B == 1", "A == 1 && C == 1", ConditionalOperation.Or, "A == 1 && (B == 1 || C == 1)")]
        [TestCase("A == 1 && B == 1", "A > 1", ConditionalOperation.Or, null)]
        [TestCase("A > 1", "A == 1 && B == 1", ConditionalOperation.Or, null)]
        [TestCase("float(0x1234) > 1.0", "float(0x1234) > 2.0", ConditionalOperation.And, "float(0x001234) > 2.0")]
        [TestCase("float(0x1234) > 1.0", "float(0x1234) < 2.0", ConditionalOperation.And, null)]
        [TestCase("float(0x1234) > 1.0", "float(0x1234) < -4.0", ConditionalOperation.And, "always_false()")]
        public void TestLogicalIntersect(string left, string right, ConditionalOperation op, string expected)
        {
            TriggerExpressionTests.AssertLogicalIntersect(left, right, op, expected);
        }

        [Test]
        public void TestNestedComplex()
        {
            var input = "(A == 1 && B == 1) || (A == 2 && (B == 2 || B == 3 || B == 4))";

            input = ExpressionTests.ReplacePlaceholders(input);
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);

            var expected = "1=1S0xH000001=1_0xH000002=1S0xH000001=2_O:0xH000002=2_O:0xH000002=3_0xH000002=4";
            TriggerExpressionTests.AssertSerializeAchievement(clause, expected);
        }

        [Test]
        public void TestNestedComplex2()
        {
            var input = "(A == 1 && B == 1) || (A == 2 && ((B == 2 && C == 2) || (B == 3 && C == 3)))";

            input = ExpressionTests.ReplacePlaceholders(input);
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);

            var expected = "1=1S0xH000001=1_0xH000002=1S0xH000001=2_0xH000002=2_0xH000003=2S0xH000001=2_0xH000002=3_0xH000003=3";
            TriggerExpressionTests.AssertSerializeAchievement(clause, expected);
        }

        [Test]
        public void TestNestedComplexWithOnce()
        {
            var input = "once(prev(A) == 0 && A == 1) && once(once(once(always_true() && B == 1) && B == 2) && B == 3)";

            input = ExpressionTests.ReplacePlaceholders(input);

            var tokenizer = Tokenizer.CreateTokenizer(input);
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new TriggerBuilderContext();

            ExpressionBase result;
            if (!expr.ReplaceVariables(scope, out result))
                Assert.Fail(result.ToString());

            var expected = "N:d0xH000001=0_0xH000001=1.1._N:0xH000002=1.1._N:0xH000002=2.1._0xH000002=3.1.";

            var builder = new ScriptInterpreterAchievementBuilder();
            builder.PopulateFromExpression(result);
            builder.Optimize();
            Assert.That(builder.SerializeRequirements(), Is.EqualTo(expected));
        }

        [Test]
        public void TestNestedOnceChain()
        {
            // this is generated by a helper function used to create a forced series of
            // sequential events in an AndNext chain:
            //
            //   function CreateHitTargetedAndNextChain(permutation)
            //   {
            //     trigger = always_true()
            //
            //     for condition in permutation
            //     {
            //       trigger = once(trigger && once(condition))
            //     }
            //
            //     return trigger
            //   }

            var input = "once(once(once(always_true() && once(A == 1)) && once(A == 2)) && once(A == 3))";

            input = ExpressionTests.ReplacePlaceholders(input);

            var tokenizer = Tokenizer.CreateTokenizer(input);
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new TriggerBuilderContext();

            ExpressionBase result;
            if (!expr.ReplaceVariables(scope, out result))
                Assert.Fail(result.ToString());

            var expected = "N:0xH000001=1.1._N:0xH000001=2.1._0xH000001=3.1.";

            var builder = new ScriptInterpreterAchievementBuilder();
            builder.PopulateFromExpression(result);
            builder.Optimize();
            Assert.That(builder.SerializeRequirements(), Is.EqualTo(expected));
        }

        [Test]
        public void TestEnsureLastClauseHasNoHitCount()
        {
            // prev(A) should not be moved to the end; A == 4 should.
            var input = "repeated(3, A == 4 && once(prev(A) == 0 && A == 1) && once(B == 2))";

            input = ExpressionTests.ReplacePlaceholders(input);

            var tokenizer = Tokenizer.CreateTokenizer(input);
            var expr = ExpressionBase.Parse(new PositionalTokenizer(tokenizer));

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new TriggerBuilderContext();

            ExpressionBase result;
            if (!expr.ReplaceVariables(scope, out result))
                Assert.Fail(result.ToString());

            var expected = "N:d0xH000001=0_N:0xH000001=1.1._N:0xH000002=2.1._0xH000001=4.3.";

            var builder = new ScriptInterpreterAchievementBuilder();
            builder.PopulateFromExpression(result);
            builder.Optimize();
            Assert.That(builder.SerializeRequirements(), Is.EqualTo(expected));
        }
    }
}
