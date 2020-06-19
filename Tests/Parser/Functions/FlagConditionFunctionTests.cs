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
    class FlagConditionFunctionTests
    {
        [Test]
        public void TestNeverDefinition()
        {
            var funcDef = new FlagConditionFunction("never", RequirementType.ResetIf);
            Assert.That(funcDef.Name.Name, Is.EqualTo("never"));
            Assert.That(funcDef.Parameters.Count, Is.EqualTo(1));
            Assert.That(funcDef.Parameters.ElementAt(0).Name, Is.EqualTo("comparison"));
        }

        private List<Requirement> Evaluate(FlagConditionFunction funcDef, string input, string expectedError)
        {
            var requirements = new List<Requirement>();

            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, AchievementScriptInterpreter.GetGlobalScope(), out error);
            var context = new TriggerBuilderContext { Trigger = requirements };
            scope.Context = context;

            ExpressionBase evaluated;
            Assert.That(funcDef.ReplaceVariables(scope, out evaluated), Is.True);

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

        private List<Requirement> EvaluateNever(string input, string expectedError = null)
        {
            var funcDef = new FlagConditionFunction("never", RequirementType.ResetIf);
            return Evaluate(funcDef, input, expectedError);
        }

        [Test]
        public void TestNeverSimple()
        {
            var requirements = EvaluateNever("never(byte(0x1234) == 56)");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.ResetIf));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestNeverExplicitCall()
        {
            // not providing a TriggerBuilderContext simulates calling the function at a global scope
            var funcDef = new FlagConditionFunction("never", RequirementType.ResetIf);

            var input = "never(byte(0x1234) == 56)";
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, AchievementScriptInterpreter.GetGlobalScope(), out error);
            Assert.That(funcDef.Evaluate(scope, out error), Is.False);
            Assert.That(error, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)error).Message, Is.EqualTo("never has no meaning outside of a trigger clause"));
        }

        [Test]
        public void TestNeverMultipleConditions()
        {
            var requirements = EvaluateNever("never(byte(0x1234) == 56 && byte(0x2345) == 67)");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.ResetIf));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestNeverNonConditionConstant()
        {
            EvaluateNever("never(6+2)", "comparison did not evaluate to a valid comparison");
        }

        [Test]
        public void TestNeverNonConditionFunction()
        {
            EvaluateNever("never(byte(0x1234))", "comparison did not evaluate to a valid comparison");
        }

        [Test]
        public void TestNeverOrNext()
        {
            var requirements = EvaluateNever("never(byte(0x1234) == 56 || byte(0x2345) == 67)");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.OrNext));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.ResetIf));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestNeverNever()
        {
            // the inner 'never(byte(0x1234) == 56)' will be optimized to 
            // 'byte(0x1234) != 56'. then the outer never will be applied.
            var requirements = EvaluateNever("never(never(byte(0x1234) == 56))");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.NotEqual));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.ResetIf));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestNeverNeverWithHitTarget()
        {
            // the hit target will prevent the optimizer from inverting the inner logic,
            // so the inner clause will retain its ResetIf. to prevent a discrepency with
            // the TestNeverNever where the logic is inverted, generate an error.
            EvaluateNever("never(never(byte(0x1234) == 56 && once(byte(0x2345) == 67)))",
                "Cannot apply 'never' to condition already flagged with ResetIf");
        }

        [Test]
        public void TestNeverUnless()
        {
            // the inner 'unless(byte(0x1234) == 56)' will be optimized to 
            // 'byte(0x1234) != 56'. then the outer never will be applied.
            var requirements = EvaluateNever("never(unless(byte(0x1234) == 56))");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.NotEqual));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.ResetIf));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestNeverUnlessWithHitTarget()
        {
            // the hit target will prevent the optimizer from inverting the inner logic,
            // so the inner clause will retain its PauseIf. PauseIf and ResetIf are
            // mutually exclusive, so expect an error.
            EvaluateNever("never(unless(byte(0x1234) == 56 && once(byte(0x2345) == 67)))", 
                "Cannot apply 'never' to condition already flagged with PauseIf");
        }
    }
}
