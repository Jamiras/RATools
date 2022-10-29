using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Tests.Parser.Functions
{
    [TestFixture]
    class OnceFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new OnceFunction();
            Assert.That(def.Name.Name, Is.EqualTo("once"));
            Assert.That(def.Parameters.Count, Is.EqualTo(1));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("comparison"));
        }

        private List<Requirement> Evaluate(string input, string expectedError = null)
        {
            var requirements = new List<Requirement>();
            var funcDef = new OnceFunction();

            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, AchievementScriptInterpreter.GetGlobalScope(), out error);
            var context = new TriggerBuilderContext { Trigger = requirements };
            scope.Context = context;

            ExpressionBase evaluated;
            Assert.That(funcDef.ReplaceVariables(scope, out evaluated), Is.True);


            var triggerExpression = evaluated as ITriggerExpression;
            if (triggerExpression != null)
            {
                error = triggerExpression.BuildTrigger(context);
            }
            else
            {
                funcCall = evaluated as FunctionCallExpression;
                error = funcDef.BuildTrigger(context, scope, funcCall);
            }

            if (expectedError == null)
            {
                Assert.That(error, Is.Null);
            }
            else
            {
                Assert.That(error, Is.InstanceOf<ErrorExpression>());
                Assert.That(((ErrorExpression)error).Message, Is.EqualTo(expectedError));
            }

            return requirements;
        }

        [Test]
        public void TestSimple()
        {
            var requirements = Evaluate("once(byte(0x1234) == 56)");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[0].HitCount, Is.EqualTo(1));
        }

        [Test]
        public void TestExplicitCall()
        {
            // not providing a TriggerBuilderContext simulates calling the function at a global scope
            var funcDef = new OnceFunction();

            var input = "once(byte(0x1234) == 56)";
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, AchievementScriptInterpreter.GetGlobalScope(), out error);
            Assert.That(funcDef.Evaluate(scope, out error), Is.False);
            Assert.That(error, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)error).Message, Is.EqualTo("once has no meaning outside of a trigger clause"));
        }

        [Test]
        public void TestMultipleConditions()
        {
            var requirements = Evaluate("once(byte(0x1234) == 56 && byte(0x2345) == 67)");
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
            Assert.That(requirements[1].HitCount, Is.EqualTo(1));
        }

        [Test]
        public void TestNonConditionConstant()
        {
            Evaluate("once(6+2)", "comparison did not evaluate to a valid comparison");
        }

        [Test]
        public void TestNonConditionFunction()
        {
            Evaluate("once(byte(0x1234))", "comparison did not evaluate to a valid comparison");
        }

        [Test]
        public void TestFunctionReference()
        {
            string input = "once(f)";

            var requirements = new List<Requirement>();
            var funcDef = new OnceFunction();

            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.AssignVariable(new VariableExpression("f"), new FunctionReferenceExpression("f2"));

            ExpressionBase error;
            scope = funcCall.GetParameters(funcDef, scope, out error);
            var context = new TriggerBuilderContext { Trigger = requirements };
            scope.Context = context;

            ExpressionBase evaluated;
            Assert.That(funcDef.ReplaceVariables(scope, out evaluated), Is.True);
            funcCall = evaluated as FunctionCallExpression;

            var parseError = funcDef.BuildTrigger(context, scope, funcCall);
            Assert.That(parseError, Is.Not.Null);
            Assert.That(parseError.InnermostError.Message, Is.EqualTo("Function used like a variable"));
        }

        [Test]
        public void TestOrNext()
        {
            var requirements = Evaluate("once(byte(0x1234) == 56 || byte(0x2345) == 67)");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.OrNext));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[1].HitCount, Is.EqualTo(1));
        }

        [Test]
        public void TestNested()
        {
            var requirements = Evaluate("once(byte(0x1234) == 1 && prev(byte(0x1235)) == 2 && once(byte(0x1235) == 3))");
            Assert.That(requirements.Count, Is.EqualTo(3));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("1"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x001235)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("3"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AndNext));
            Assert.That(requirements[1].HitCount, Is.EqualTo(1));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("prev(byte(0x001235))"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("2"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[2].HitCount, Is.EqualTo(1));
        }
    }
}
