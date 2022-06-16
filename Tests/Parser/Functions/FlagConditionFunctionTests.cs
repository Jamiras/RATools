using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Test.Parser.Functions
{
    [TestFixture]
    class FlagConditionFunctionTests
    {
        [Test]
        public void TestNeverDefinition()
        {
            var funcDef = AchievementScriptInterpreter.GetGlobalScope().GetFunction("never");
            Assert.That(funcDef, Is.Not.Null);
            Assert.That(funcDef.Name.Name, Is.EqualTo("never"));
            Assert.That(funcDef.Parameters.Count, Is.EqualTo(1));
            Assert.That(funcDef.Parameters.ElementAt(0).Name, Is.EqualTo("comparison"));
        }

        public void TestUnlessDefinition()
        {
            var funcDef = AchievementScriptInterpreter.GetGlobalScope().GetFunction("unless");
            Assert.That(funcDef, Is.Not.Null);
            Assert.That(funcDef.Name.Name, Is.EqualTo("unless"));
            Assert.That(funcDef.Parameters.Count, Is.EqualTo(1));
            Assert.That(funcDef.Parameters.ElementAt(0).Name, Is.EqualTo("comparison"));
        }

        public void TestTriggerWhenDefinition()
        {
            var funcDef = AchievementScriptInterpreter.GetGlobalScope().GetFunction("trigger_when");
            Assert.That(funcDef, Is.Not.Null);
            Assert.That(funcDef.Name.Name, Is.EqualTo("trigger_when"));
            Assert.That(funcDef.Parameters.Count, Is.EqualTo(1));
            Assert.That(funcDef.Parameters.ElementAt(0).Name, Is.EqualTo("comparison"));
        }

        [Test]
        [TestCase("never(byte(1) == 56)", "never(byte(1) == 56)")]
        [TestCase("never(repeated(6, byte(1) == 56))", "never(repeated(6, byte(1) == 56))")]
        [TestCase("never(byte(1) == 56 || byte(2) == 3)", "never(byte(1) == 56) && never(byte(2) == 3)")] // or clauses can be separated
        [TestCase("never(byte(1) == 56 && byte(2) == 3)", "never(byte(1) == 56 && byte(2) == 3)")] // and clauses cannot be separated
        [TestCase("unless(byte(1) == 56)", "unless(byte(1) == 56)")]
        [TestCase("unless(repeated(6, byte(1) == 56))", "unless(repeated(6, byte(1) == 56))")]
        [TestCase("unless(byte(1) == 56 || byte(2) == 3)", "unless(byte(1) == 56) && unless(byte(2) == 3)")] // or clauses can be separated
        [TestCase("unless(byte(1) == 56 && byte(2) == 3)", "unless(byte(1) == 56 && byte(2) == 3)")] // and clauses cannot be separated
        [TestCase("trigger_when(byte(1) == 56)", "trigger_when(byte(1) == 56)")]
        [TestCase("trigger_when(repeated(6, byte(1) == 56))", "trigger_when(repeated(6, byte(1) == 56))")]
        [TestCase("trigger_when(byte(1) == 56 && byte(2) == 3)", "trigger_when(byte(1) == 56) && trigger_when(byte(2) == 3)")] // and clauses can be separated
        [TestCase("trigger_when(byte(1) == 56 || byte(2) == 3)", "trigger_when(byte(1) == 56) || trigger_when(byte(2) == 3)")] // or clauses can be separated
        [TestCase("trigger_when(repeated(6, byte(1) == 56) && unless(byte(2) == 3))", "trigger_when(repeated(6, byte(1) == 56)) && unless(byte(2) == 3)")] // PauseIf clause can be extracted
        [TestCase("trigger_when(repeated(6, byte(1) == 56) && never(byte(2) == 3))", "trigger_when(repeated(6, byte(1) == 56)) && never(byte(2) == 3)")] // ResetIf clause can be extracted
        [TestCase("trigger_when(repeated(6, byte(1) == 56 && never(byte(2) == 3)))", "trigger_when(repeated(6, byte(1) == 56 && never(byte(2) == 3)))")] // ResetNextIf clause should not be extracted
        [TestCase("trigger_when((byte(1) == 56 && byte(2) == 3) || (byte(1) == 55 && byte(2) == 4))",
            "trigger_when(byte(1) == 56) && trigger_when(byte(2) == 3) || trigger_when(byte(1) == 55) && trigger_when(byte(2) == 4)")] // or with ands can be separated
        [TestCase("trigger_when((byte(1) == 56 || byte(2) == 3) && (byte(1) == 55 || byte(2) == 4))",
            "trigger_when(byte(1) == 56 || byte(2) == 3) && trigger_when(byte(1) == 55 || byte(2) == 4)")] // and can be separated, but not nested ors
        [TestCase("trigger_when((byte(1) == 56 && byte(2) == 3) && (byte(1) == 55 || byte(2) == 4))",
            "trigger_when(byte(1) == 56) && trigger_when(byte(2) == 3) && trigger_when(byte(1) == 55 || byte(2) == 4)")] // and can be separated, but not nested ors
        public void TestReplaceVariables(string input, string expected)
        {
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new TriggerBuilderContext();

            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));

            ExpressionBase evaluated;
            Assert.That(expression.ReplaceVariables(scope, out evaluated), Is.True);

            var builder = new StringBuilder();
            evaluated.AppendString(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
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
        public void TestNeverSimple()
        {
            var requirements = Evaluate("never(byte(0x1234) == 56)");
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
            var funcDef = new NeverFunction();

            var input = "never(byte(0x1234) == 56)";
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, AchievementScriptInterpreter.GetGlobalScope(), out error);
            Assert.That(funcDef.Evaluate(scope, out error), Is.False);
            Assert.That(error, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)error).Message, Is.EqualTo("never has no meaning outside of a trigger clause"));
        }

        [Test]
        public void TestNeverMultipleConditions()
        {
            var requirements = Evaluate("never(byte(0x1234) == 56 && byte(0x2345) == 67)");
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
            Evaluate("never(6+2)", "comparison did not evaluate to a valid comparison");
        }

        [Test]
        public void TestNeverNonConditionFunction()
        {
            Evaluate("never(byte(0x1234))", "comparison did not evaluate to a valid comparison");
        }

        [Test]
        public void TestNeverOrNext()
        {
            var requirements = Evaluate("never(byte(0x1234) == 56 || byte(0x2345) == 67)");
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
            var requirements = Evaluate("never(never(byte(0x1234) == 56))");
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
            Evaluate("never(never(byte(0x1234) == 56 && once(byte(0x2345) == 67)))",
                "Cannot apply 'never' to condition already flagged with ResetIf");
        }

        [Test]
        public void TestNeverUnless()
        {
            // the inner 'unless(byte(0x1234) == 56)' will be optimized to 
            // 'byte(0x1234) != 56'. then the outer never will be applied.
            var requirements = Evaluate("never(unless(byte(0x1234) == 56))");
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
            Evaluate("never(unless(byte(0x1234) == 56) && once(byte(0x2345) == 67))", 
                "Cannot apply 'never' to condition already flagged with PauseIf");
        }

        [Test]
        public void TestUnlessSimple()
        {
            var requirements = Evaluate("unless(byte(0x1234) == 56)");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.PauseIf));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
        }
        [Test]
        public void TestTriggerWhenSimple()
        {
            var requirements = Evaluate("trigger_when(byte(0x1234) == 56)");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.Trigger));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void TestTriggerWhenOnceNever()
        {
            var requirements = Evaluate("trigger_when(once(byte(0x1234) == 56 && never(byte(0x2345) == 1)))");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("1"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.ResetNextIf));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.Trigger));
            Assert.That(requirements[1].HitCount, Is.EqualTo(1));
        }
    }
}
