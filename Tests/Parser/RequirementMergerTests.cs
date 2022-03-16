using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser;
using RATools.Parser.Internal;

namespace RATools.Test.Parser
{
    [TestFixture]
    class RequirementMergerTests
    {
        private static AchievementBuilder CreateAchievement(string input, string expectedError = null)
        {
            // NOTE: these are integration tests as they rely on ExpressionBase.Parse and 
            // AchievementScriptInterpreter.ScriptInterpreterAchievementBuilder, but using string 
            // inputs /output makes reading the tests and validating the behavior easier for humans.
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expression = ExpressionBase.Parse(tokenizer);
            if (expression is ParseErrorExpression)
                Assert.Fail(((ParseErrorExpression)expression).Message);

            var achievement = new ScriptInterpreterAchievementBuilder();
            var error = achievement.PopulateFromExpression(expression);
            if (expectedError != null)
                Assert.That(error, Is.EqualTo(expectedError));
            else
                Assert.That(error, Is.Null.Or.Empty);

            return achievement;
        }

        private void AssertLogicalMerge(string input, string y, string expected)
        {
            input = input.Replace("X", "6");
            input = input.Replace("Y", y);
            var logic = input;

            input = input.Replace("A", "byte(0x00000A)");
            input = input.Replace("B", "byte(0x00000B)");
            input = input.Replace("C", "byte(0x00000C)");

            var achievement = CreateAchievement(input);
            achievement.Optimize();

            var result = achievement.RequirementsDebugString;
            result = result.Replace("byte(0x00000A)", "A");
            result = result.Replace("byte(0x00000B)", "B");
            result = result.Replace("byte(0x00000C)", "C");

            if (expected == "ignore")
            {
                expected = logic;
            }
            else if (expected == "false ")
            {
                expected = "always_false()";
            }
            else if (expected == "true  ")
            {
                expected = "always_true()";
            }
            else
            {
                expected = expected.Replace("  ", " ");
                expected = expected.Replace("X", "6");
                expected = expected.Replace("Y", y);
            }

            Assert.That(result, Is.EqualTo(expected), logic);
        }

        [Test] //                      X == Y    X <  Y    X >  Y
        [TestCase("A == X && A == Y", "A == X", "false ", "false ")]
        [TestCase("A == X && A != Y", "false ", "A == X", "A == X")]
        [TestCase("A == X && A <  Y", "false ", "A == X", "false ")]
        [TestCase("A == X && A <= Y", "A == X", "A == X", "false ")]
        [TestCase("A == X && A >  Y", "false ", "false ", "A == X")]
        [TestCase("A == X && A >= Y", "A == X", "false ", "A == X")]
        [TestCase("A != X && A == Y", "false ", "A == Y", "A == Y")]
        [TestCase("A != X && A != Y", "A != X", "ignore", "ignore")]
        [TestCase("A != X && A <  Y", "A <  Y", "ignore", "A <  Y")]
        [TestCase("A != X && A <= Y", "A <  X", "ignore", "A <= Y")]
        [TestCase("A != X && A >  Y", "A >  Y", "A >  Y", "ignore")]
        [TestCase("A != X && A >= Y", "A >  Y", "A >= Y", "ignore")]
        [TestCase("A >  X && A == Y", "false ", "A == Y", "false ")]
        [TestCase("A >  X && A != Y", "A >  X", "ignore", "A >  X")]
        [TestCase("A >  X && A <  Y", "false ", "ignore", "false ")]
        [TestCase("A >  X && A <= Y", "false ", "ignore", "false ")]
        [TestCase("A >  X && A >  Y", "A >  X", "A >  Y", "A >  X")]
        [TestCase("A >  X && A >= Y", "A >  X", "A >= Y", "A >  X")]
        [TestCase("A >= X && A == Y", "A == Y", "A == Y", "false ")]
        [TestCase("A >= X && A != Y", "A >  X", "ignore", "A >= X")]
        [TestCase("A >= X && A <  Y", "false ", "ignore", "false ")]
        [TestCase("A >= X && A <= Y", "A == X", "ignore", "false ")]
        [TestCase("A >= X && A >  Y", "A >  X", "A >  Y", "A >= X")]
        [TestCase("A >= X && A >= Y", "A >= X", "A >= Y", "A >= X")]
        [TestCase("A <  X && A == Y", "false ", "false ", "A == Y")]
        [TestCase("A <  X && A != Y", "A <  X", "A <  X", "ignore")]
        [TestCase("A <  X && A <  Y", "A <  X", "A <  X", "A <  Y")]
        [TestCase("A <  X && A <= Y", "A <  X", "A <  X", "A <= Y")]
        [TestCase("A <  X && A >  Y", "false ", "false ", "ignore")]
        [TestCase("A <  X && A >= Y", "false ", "false ", "ignore")]
        [TestCase("A <= X && A == Y", "A == Y", "false ", "A == Y")]
        [TestCase("A <= X && A != Y", "A <  X", "A <= X", "ignore")]
        [TestCase("A <= X && A <  Y", "A <  Y", "A <= X", "A <  Y")]
        [TestCase("A <= X && A <= Y", "A <= X", "A <= X", "A <= Y")]
        [TestCase("A <= X && A >  Y", "false ", "false ", "ignore")]
        [TestCase("A <= X && A >= Y", "A == X", "false ", "ignore")]
        public void TestMergeRequirementsAnd(string input, string expectedXEqualsY, string expectedXLessY, string expectedXGreaterY)
        {
            input = input.Replace("  ", " ");
            AssertLogicalMerge(input, "6", expectedXEqualsY);
            AssertLogicalMerge(input, "8", expectedXLessY);
            AssertLogicalMerge(input, "4", expectedXGreaterY);
        }

        [Test] //                      X == Y    X <  Y    X >  Y
        [TestCase("A == X || A == Y", "A == X", "ignore", "ignore")]
        [TestCase("A == X || A != Y", "true  ", "A != Y", "A != Y")]
        [TestCase("A == X || A <  Y", "A <= X", "A <  Y", "ignore")]
        [TestCase("A == X || A <= Y", "A <= Y", "A <= Y", "ignore")]
        [TestCase("A == X || A >  Y", "A >= X", "ignore", "A >  Y")]
        [TestCase("A == X || A >= Y", "A >= Y", "ignore", "A >= Y")]
        [TestCase("A != X || A == Y", "true  ", "A != X", "A != X")]
        [TestCase("A != X || A != Y", "A != Y", "true  ", "true  ")]
        [TestCase("A != X || A <  Y", "A != X", "true  ", "A != X")]
        [TestCase("A != X || A <= Y", "true  ", "true  ", "A != X")]
        [TestCase("A != X || A >  Y", "A != X", "A != X", "true  ")]
        [TestCase("A != X || A >= Y", "true  ", "A != X", "true  ")]
        [TestCase("A >  X || A == Y", "A >= X", "A >  X", "ignore")]
        [TestCase("A >  X || A != Y", "A != Y", "true  ", "A != Y")]
        [TestCase("A >  X || A <  Y", "A != X", "true  ", "ignore")]
        [TestCase("A >  X || A <= Y", "true  ", "true  ", "ignore")]
        [TestCase("A >  X || A >  Y", "A >  X", "A >  X", "A >  Y")]
        [TestCase("A >  X || A >= Y", "A >= Y", "A >  X", "A >= Y")]
        [TestCase("A >= X || A == Y", "A >= X", "A >= X", "ignore")]
        [TestCase("A >= X || A != Y", "true  ", "true  ", "A != Y")]
        [TestCase("A >= X || A <  Y", "true  ", "true  ", "ignore")]
        [TestCase("A >= X || A <= Y", "true  ", "true  ", "ignore")]
        [TestCase("A >= X || A >  Y", "A >= X", "A >= X", "A >  Y")]
        [TestCase("A >= X || A >= Y", "A >= X", "A >= X", "A >= Y")]
        [TestCase("A <  X || A == Y", "A <= X", "ignore", "A <  X")]
        [TestCase("A <  X || A != Y", "A != Y", "A != Y", "true  ")]
        [TestCase("A <  X || A <  Y", "A <  X", "A <  Y", "A <  X")]
        [TestCase("A <  X || A <= Y", "A <= X", "A <= Y", "A <  X")]
        [TestCase("A <  X || A >  Y", "A != X", "ignore", "true  ")]
        [TestCase("A <  X || A >= Y", "true  ", "ignore", "true  ")]
        [TestCase("A <= X || A == Y", "A <= X", "ignore", "A <= X")]
        [TestCase("A <= X || A != Y", "true  ", "A != Y", "true  ")]
        [TestCase("A <= X || A <  Y", "A <= X", "A <  Y", "A <= X")]
        [TestCase("A <= X || A <= Y", "A <= X", "A <= Y", "A <= X")]
        [TestCase("A <= X || A >  Y", "true  ", "ignore", "true  ")]
        [TestCase("A <= X || A >= Y", "true  ", "ignore", "true  ")]
        public void TestMergeRequirementsOr(string input, string expectedXEqualsY, string expectedXLessY, string expectedXGreaterY)
        {
            input = input.Replace("  ", " ");
            AssertLogicalMerge(input, "6", expectedXEqualsY);
            AssertLogicalMerge(input, "8", expectedXLessY);
            AssertLogicalMerge(input, "4", expectedXGreaterY);
        }

        [Test]
        [TestCase("A == B && A == B", "A == B")]
        [TestCase("A == B && A != B", "false ")]
        [TestCase("A == B && A >= B", "A == B")]
        [TestCase("A == B && A >  B", "false ")]
        [TestCase("A == B && A <= B", "A == B")]
        [TestCase("A == B && A <  B", "false ")]
        [TestCase("A == B && A == X", "ignore")]
        [TestCase("A == B || A == B", "A == B")]
        [TestCase("A == B || A != B", "true  ")]
        [TestCase("A == B || A >= B", "A >= B")]
        [TestCase("A == B || A >  B", "A >= B")]
        [TestCase("A == B || A <= B", "A <= B")]
        [TestCase("A == B || A <  B", "A <= B")]
        [TestCase("A == B || A == X", "ignore")]
        [TestCase("A == B && A != prev(B)", "ignore")]
        [TestCase("A == X && prev(A) != X", "ignore")]
        [TestCase("A == X && prev(A) == X", "ignore")]
        public void TestMergeRequirementsNonValue(string input, string expected)
        {
            input = input.Replace("  ", " ");
            AssertLogicalMerge(input, "8", expected);
        }

        [Test]
        [TestCase("once(A == 1) && once(A == 2)", "ignore")] // once() should not cause a conflict
        [TestCase("(A != 255 && B == 5 && A < 6)", "B == 5 && A < 6")] // A != 255 is handled by A < 6
        [TestCase("once(C == 18 && A == 0 && B == 1) && never(A > 5)", "ignore")] // reset condition should not be merged into andnext chain
        [TestCase("never((once(A == 0 && B == 1) && A > 5))", "ignore")] // hitcount on once(A/B) should prevent merge of A>5
        [TestCase("never((once(once(C == 18) && A == 0 && B == 1) && A > 5))", "ignore")] // hitcount on once(A/B) should prevent merge of A>5
        [TestCase("once(A == 0 && B == 1) && once(A == 0 && B == 1 && C == 2) && A > 5", "ignore")] // hitcount on once(A/B) should prevent merge of A>5
        [TestCase("once(A == 0 && B == 1) && never((once(A == 0 && B == 1 && C == 2) && A > 5))", "ignore")] // hitcount on once(A/B) should prevent merge of A>5
        [TestCase("(A == prev(B) && C > prev(C)) || (A != prev(B) && C > 1)", "ignore")]
        [TestCase("(A > prev(A) && B > prev(B)) || (A == prev(A) && B == prev(B))", "ignore")]
        public void TestMergeRequirementsComplex(string input, string expected)
        {
            AssertLogicalMerge(input, "8", expected);
        }

        [Test]
        [TestCase("(A < 5 && B == 5) || (A < 5 && B == 5)", "A < 5 && B == 5")] // both are identical
        [TestCase("(A < 5 && B == 5) || (A > 5 && B == 5)", "A != 5 && B == 5")] // B is identical, A can be merged
        [TestCase("(A < 5 && B == 5) || (A > 5 && B == 6)", "ignore")] // B is not identical, A should not be merged
        [TestCase("(A < 5 && B == 5) || (A <= 5 && B <= 6)", "A <= 5 && B <= 6")] // first group is a subset of second
        [TestCase("(A < 5 && B >= 5) || (A <= 5 && B == 6)", "ignore")] // A is a subset of left, B is a subset of right, can't merge
        [TestCase("(A < 5 && B == 5) || (A <= 5 && B >= 6 && C <= 7)", "ignore")] // A is a subset, but not B or C
        [TestCase("(A < 5 && B == 5) || (A <= 5 && B <= 6 && C <= 7)", "ignore")] // A and B are a subset, but C is more restrictive
        [TestCase("(A < 5 && B == 5) || A < 5", "A < 5")] // B is superfluous
        [TestCase("(A < 5 && B == 5) || A <= 6", "A <= 6")] // B is superfluous
        [TestCase("(A < 5 && B == 5) || A < 4", "ignore")] // B matters for A == 4
        [TestCase("(A < 5 && B == 5) || B == 5", "B == 5")] // A is superfluous
        [TestCase("(A < 5 && B == 5) || B < 6", "B < 6")] // A is superfluous
        [TestCase("(A < 5 && B == 5) || B > 6", "ignore")] // A matters for B == 5
        [TestCase("A > 7 || (A == 7 && B > 5)", "ignore")] // B matters for A == 7
        [TestCase("(A == 7 && B > 5) || A > 7", "ignore")] // B matters for A == 7
        [TestCase("(A <= 5 && B <= 6 && C <= 7) || (A < 5 && B == 5)", "ignore")] // A and B are a subset, but C is more restrictive
        [TestCase("(A <= 5 && B <= 6) || (A < 5 && B == 5 && C <= 7)", "A <= 5 && B <= 6")] // A and B are a superset, so more restrive C can be discarded
        [TestCase("(A < 5 && B == 5 && C <= 7) || (A <= 5 && B <= 6)", "A <= 5 && B <= 6")] // A and B are a superset, so more restrive C can be discarded
        [TestCase("(A < 5 && B == 5) || (A <= 5 && B <= 6) || (A == 2 && B <= 8)", "(A <= 5 && B <= 6) || (A == 2 && B <= 8)")] // third group cannot be handled by second
        [TestCase("(A < 5 && B == 5) || (A == 2 && B <= 4) || (A <= 5 && B <= 6)", "A <= 5 && B <= 6")] // double merge
        [TestCase("(A < 5 && B == 5) || (A <= 5 && B <= 6) || (A == 2 && B <= 4)", "A <= 5 && B <= 6")] // double merge
        [TestCase("(A <= 5 && B <= 6) || (A < 5 && B == 5) || (A == 2 && B <= 4)", "A <= 5 && B <= 6")] // double merge
        [TestCase("(A == 1 && B == 2 && C == 3) || (A == 1 && B != 2 && C == 3)", "A == 1 && C == 3")] // conflicting Bs cancel each other out
        [TestCase("(A == 1 && B == 2 && C == 3) || (A == 1 && B != 2 && C == 4)", "A == 1 && ((B == 2 && C == 3) || (B != 2 && C == 4))")] // unique Cs prevent merger, but A can still be extracted
        public void TestMergeRequirementGroups(string input, string expected)
        {
            AssertLogicalMerge(input, "8", expected);
        }
    }
}
