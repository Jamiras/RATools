using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Test.Parser.Functions
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

        private List<Requirement> Evaluate(string input, string expectedError = null)
        {
            var requirements = new List<Requirement>();
            var funcDef = new TallyFunction();

            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, AchievementScriptInterpreter.GetGlobalScope(), out error);
            var context = new TriggerBuilderContext { Trigger = requirements };
            scope.Context = context;

            ExpressionBase evaluated;
            Assert.That(funcDef.ReplaceVariables(scope, out evaluated), Is.True);
            funcCall = (FunctionCallExpression)evaluated;

            if (expectedError == null)
            {
                Assert.That(funcDef.BuildTrigger(context, scope, funcCall), Is.Null);
            }
            else
            {
                var parseError = funcDef.BuildTrigger(context, scope, funcCall);
                Assert.That(parseError, Is.Not.Null);
                Assert.That(parseError.Message, Is.EqualTo(expectedError));
            }

            return requirements;
        }

        [Test]
        public void TestSimple()
        {
            var requirements = Evaluate("tally(4, byte(0x1234) == 56)");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[0].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestMultipleConditions()
        {
            var requirements = Evaluate("tally(4, byte(0x1234) == 56 && byte(0x2345) == 67)");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[1].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestNonConditionConstant()
        {
            Evaluate("tally(4, 6+2)", "comparison did not evaluate to a valid comparison");
        }

        [Test]
        public void TestNonConditionFunction()
        {
            Evaluate("tally(4, byte(0x1234))", "comparison did not evaluate to a valid comparison");
        }

        [Test]
        public void TestTwoConditions()
        {
            var requirements = Evaluate("tally(4, byte(0x1234) == 56, byte(0x1234) == 67)");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[1].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestManyConditions()
        {
            var requirements = Evaluate("tally(4, byte(0x1234) == 56, byte(0x1234) == 67, byte(0x1234) == 78, byte(0x1234) == 89)");
            Assert.That(requirements.Count, Is.EqualTo(4));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("78"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[2].HitCount, Is.EqualTo(0));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[3].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[3].Right.ToString(), Is.EqualTo("89"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[3].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestTwoConditionsInArray()
        {
            var requirements = Evaluate("tally(4, [byte(0x1234) == 56, byte(0x1234) == 67])");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[1].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestRestrictedPrimaryCondition()
        {
            var requirements = Evaluate("tally(4, once(byte(0x1234) == 56), byte(0x1234) == 67)");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[0].HitCount, Is.EqualTo(1));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[1].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestRestrictedSecondaryCondition()
        {
            var requirements = Evaluate("tally(4, byte(0x1234) == 56, once(byte(0x1234) == 67))");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[0].HitCount, Is.EqualTo(1));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[1].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestRestrictedBothConditions()
        {
            var requirements = Evaluate("tally(4, repeated(3, byte(0x1234) == 56), repeated(2, byte(0x1234) == 67))");
            Assert.That(requirements.Count, Is.EqualTo(3));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[0].HitCount, Is.EqualTo(3));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[1].HitCount, Is.EqualTo(2));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("0"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("1"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[2].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestAndNext()
        {
            var requirements = Evaluate("tally(4, (byte(0x1234) == 56 && byte(0x2345) == 67), (byte(0x1234) == 56 && byte(0x2345) == 45))");
            Assert.That(requirements.Count, Is.EqualTo(4));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[2].HitCount, Is.EqualTo(0));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[3].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[3].Right.ToString(), Is.EqualTo("45"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[3].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestAndNextCommonClause()
        {
            var requirements = Evaluate("tally(4, (byte(0x1234) == 56 && byte(0x2345) == 67), (byte(0x1234) == 34 && byte(0x2345) == 67))");
            Assert.That(requirements.Count, Is.EqualTo(4));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("34"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[2].HitCount, Is.EqualTo(0));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[3].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[3].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[3].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestRestrictedAndNext()
        {
            // NOTE: this logic is invalid as it's impossible to actually get four hits if every subclause is limited to capturing one.
            // the important thing is that it validates the conversion
            var requirements = Evaluate("tally(4, once(byte(0x1234) == 56 && byte(0x2345) == 67), once(byte(0x1234) == 56 && byte(0x2345) == 45))");
            Assert.That(requirements.Count, Is.EqualTo(5));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[1].HitCount, Is.EqualTo(1));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[2].HitCount, Is.EqualTo(0));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[3].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[3].Right.ToString(), Is.EqualTo("45"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[3].HitCount, Is.EqualTo(1));
            Assert.That(requirements[4].Left.ToString(), Is.EqualTo("0"));
            Assert.That(requirements[4].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[4].Right.ToString(), Is.EqualTo("1"));
            Assert.That(requirements[4].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[4].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestAndNextOnlyInFirst()
        {
            var requirements = Evaluate("tally(4, (byte(0x1234) == 56 && byte(0x2345) == 67), byte(0x1234) == 34)");

            Assert.That(requirements.Count, Is.EqualTo(3));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("34"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[2].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestCommonClauseIsEntireCondition()
        {
            // if every clause contains the same subclause, and that subclase is an entire clause
            // the entire condition can be simplified to the subclase.
            // i.e. "(A && B) || B" is just "B"
            var requirements = Evaluate("tally(4, (byte(0x1234) == 56 && byte(0x2345) == 67) || byte(0x2345) == 67)");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[0].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestAddAddress()
        {
            var requirements = Evaluate("tally(4, byte(0x1234 + byte(0x2345)) == 56, byte(0x1234 + byte(0x2345)) == 34)");
            Assert.That(requirements.Count, Is.EqualTo(4));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddAddress));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.AddAddress));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[3].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[3].Right.ToString(), Is.EqualTo("34"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[3].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestAddSourceSubSource()
        {
            var requirements = Evaluate("tally(4, byte(0x1234) + byte(0x2345) == 56, byte(0x1234) - byte(0x2345) == 34)");
            Assert.That(requirements.Count, Is.EqualTo(4));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.SubSource));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[3].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[3].Right.ToString(), Is.EqualTo("34"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[3].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestNestedRepeated()
        {
            var requirements = Evaluate("tally(4, repeated(6, byte(0x1234) == 5 || byte(0x2345) == 6), byte(0x1234) == 34)");
            Assert.That(requirements.Count, Is.EqualTo(3));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("5"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.OrNext));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("6"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[1].HitCount, Is.EqualTo(6));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("34"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[2].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestNestedTally()
        {
            var errorMessage = "tally not allowed in subclause";
            Evaluate("tally(4, tally(6, byte(0x1234) == 5, byte(0x2345) == 6), byte(0x1234) == 34)", errorMessage);
        }

        [Test]
        public void TestUnsupportedFlags()
        {
            var errorMessage = "Modifier not allowed in subclause";
            Evaluate("tally(4, never(byte(0x1234) == 5) || once(byte(0x1234) == 34))", errorMessage);
            Evaluate("tally(4, unless(byte(0x1234) == 5) || once(byte(0x1234) == 34))", errorMessage);
            Evaluate("tally(4, measured(repeated(6, byte(0x1234) == 5)) || byte(0x1234) == 34)", errorMessage);
        }
    }
}
