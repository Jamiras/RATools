using NUnit.Framework;
using RATools.Parser;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;

namespace RATools.Tests.Parser.Expressions.Trigger
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
        public void TestBuildTrigger(string input, string expected)
        {
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);
            TriggerExpressionTests.AssertSerialize(clause, expected);
        }

        private static string ReplacePlaceholders(string input)
        {
            input = input.Replace("A", "byte(0x001234)");
            input = input.Replace("B", "byte(0x002345)");
            input = input.Replace("C", "byte(0x003456)");
            return input;
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
        [TestCase("A > 1 && B == 1 && A == 3", "A == 3 && B == 1")]
        public void TestOptimize(string input, string expected)
        {
            var context = new TriggerBuilderContext();

            input = ReplacePlaceholders(input);
            var clause = TriggerExpressionTests.Parse<RequirementClauseExpression>(input);
            var optimized = clause.Optimize(context);

            expected = ReplacePlaceholders(expected);
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
        public void TestLogicalIntersect(string left, string right, ConditionalOperation op, string expected)
        {
            left = ReplacePlaceholders(left);
            var leftClause = TriggerExpressionTests.Parse<RequirementClauseExpression>(left);

            right = ReplacePlaceholders(right);
            var rightClause = TriggerExpressionTests.Parse<RequirementExpressionBase>(right);

            var intersect = leftClause.LogicalIntersect(rightClause, op);

            expected = ReplacePlaceholders(expected);
            ExpressionTests.AssertAppendString(intersect, expected);
        }
    }
}
