using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Test.Parser.Functions
{
    [TestFixture]
    class DisableWhenFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = AchievementScriptInterpreter.GetGlobalScope().GetFunction("disable_when");
            Assert.That(def, Is.Not.Null);
            Assert.That(def.Name.Name, Is.EqualTo("disable_when"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("comparison"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("until"));
        }

        private List<Requirement> Evaluate(string input, string expectedError = null)
        {
            var requirements = new List<Requirement>();

            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;
            var scope = AchievementScriptInterpreter.GetGlobalScope();
            var funcDef = scope.GetFunction(funcCall.FunctionName.Name) as TriggerBuilderContext.FunctionDefinition;

            ExpressionBase error;
            scope = funcCall.GetParameters(funcDef, scope, out error);
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
            var requirements = Evaluate("disable_when(byte(0x1234) == 56)");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.PauseIf));
            Assert.That(requirements[0].HitCount, Is.EqualTo(1));
        }

        [Test]
        public void TestUntilSimple()
        {
            var requirements = Evaluate("disable_when(byte(0x1234) == 56, until=byte(0x2345) == 67)");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.ResetNextIf));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.PauseIf));
            Assert.That(requirements[1].HitCount, Is.EqualTo(1));
        }

        [Test]
        public void TestHitTarget()
        {
            var requirements = Evaluate("disable_when(repeated(6, byte(0x1234) == 56))");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.PauseIf));
            Assert.That(requirements[0].HitCount, Is.EqualTo(6));
        }

        [Test]
        public void TestAndNext()
        {
            var requirements = Evaluate("disable_when(byte(0x1234) == 56 && byte(0x1235) == 55, until=byte(0x2345) == 67 && byte(0x2346) == 66)");
            Assert.That(requirements.Count, Is.EqualTo(4));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002346)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("66"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.ResetNextIf));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[2].HitCount, Is.EqualTo(0));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("byte(0x001235)"));
            Assert.That(requirements[3].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[3].Right.ToString(), Is.EqualTo("55"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.PauseIf));
            Assert.That(requirements[3].HitCount, Is.EqualTo(1));
        }

        [Test]
        public void TestOrNext()
        {
            var requirements = Evaluate("disable_when(byte(0x1234) == 56 || byte(0x1235) == 55, until=byte(0x2345) == 67 || byte(0x2346) == 66)");
            Assert.That(requirements.Count, Is.EqualTo(4));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.OrNext));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002346)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("66"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.ResetNextIf));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.OrNext));
            Assert.That(requirements[2].HitCount, Is.EqualTo(0));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("byte(0x001235)"));
            Assert.That(requirements[3].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[3].Right.ToString(), Is.EqualTo("55"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.PauseIf));
            Assert.That(requirements[3].HitCount, Is.EqualTo(1));
        }

        [Test]
        public void TestTally()
        {
            // ResetNextIf has to proceed each clause of the tally
            var requirements = Evaluate("disable_when(" +
                "tally(3, once(byte(0x1234) == 56), byte(0x1235) == 55, repeated(2, byte(0x1236) == 54))," +
                "until=byte(0x2345) == 1)");
            Assert.That(requirements.Count, Is.EqualTo(6));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("1"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.ResetNextIf));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[1].HitCount, Is.EqualTo(1));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("1"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.ResetNextIf));
            Assert.That(requirements[2].HitCount, Is.EqualTo(0));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("byte(0x001236)"));
            Assert.That(requirements[3].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[3].Right.ToString(), Is.EqualTo("54"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[3].HitCount, Is.EqualTo(2));
            Assert.That(requirements[4].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[4].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[4].Right.ToString(), Is.EqualTo("1"));
            Assert.That(requirements[4].Type, Is.EqualTo(RequirementType.ResetNextIf));
            Assert.That(requirements[4].HitCount, Is.EqualTo(0));
            Assert.That(requirements[5].Left.ToString(), Is.EqualTo("byte(0x001235)"));
            Assert.That(requirements[5].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[5].Right.ToString(), Is.EqualTo("55"));
            Assert.That(requirements[5].Type, Is.EqualTo(RequirementType.PauseIf));
            Assert.That(requirements[5].HitCount, Is.EqualTo(3));
        }
    }
}
