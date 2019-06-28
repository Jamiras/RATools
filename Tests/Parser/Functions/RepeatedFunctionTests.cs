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
    class RepeatedFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new RepeatedFunction();
            Assert.That(def.Name.Name, Is.EqualTo("repeated"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("count"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("comparison"));
        }

        private List<Requirement> Evaluate(string input, string expectedError = null)
        {
            var requirements = new List<Requirement>();
            var funcDef = new RepeatedFunction();

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

        [Test]
        public void TestSimple()
        {
            var requirements = Evaluate("repeated(4, byte(0x1234) == 56)");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[0].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestExplicitCall()
        {
            // not providing a TriggerBuilderContext simulates calling the function at a global scope
            var funcDef = new RepeatedFunction();

            var input = "repeated(4, byte(0x1234) == 56)";
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, AchievementScriptInterpreter.GetGlobalScope(), out error);
            Assert.That(funcDef.Evaluate(scope, out error), Is.False);
            Assert.That(error, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)error).Message, Is.EqualTo("repeated has no meaning outside of a trigger clause"));
        }

        [Test]
        public void TestMultipleConditions()
        {
            var requirements = Evaluate("repeated(4, byte(0x1234) == 56 && byte(0x2345) == 67)");
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
            Evaluate("repeated(4, 6+2)", "Cannot generate trigger from IntegerConstant");
        }

        [Test]
        public void TestNonConditionFunction()
        {
            Evaluate("repeated(4, byte(0x1234))", "comparison did not evaluate to a valid comparison");
        }

        [Test]
        public void TestAddHitsSimple()
        {
            var requirements = Evaluate("repeated(4, byte(0x1234) == 56 || byte(0x2345) == 67)");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[1].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestAddHitsMany()
        {
            var requirements = Evaluate("repeated(4, byte(0x1234) == 56 || byte(0x2345) == 67 || byte(0x3456) == 78 || byte(0x4567) == 89)");
            Assert.That(requirements.Count, Is.EqualTo(4));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[0].HitCount, Is.EqualTo(0));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[1].HitCount, Is.EqualTo(0));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("byte(0x003456)"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("78"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[2].HitCount, Is.EqualTo(0));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("byte(0x004567)"));
            Assert.That(requirements[3].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[3].Right.ToString(), Is.EqualTo("89"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[3].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestAddHitsRestricted()
        {
            var requirements = Evaluate("repeated(4, once(byte(0x1234) == 56) || byte(0x2345) == 67)");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[0].HitCount, Is.EqualTo(1));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[1].HitCount, Is.EqualTo(4));
        }

        [Test]
        public void TestAddHitsRestrictedReorder()
        {
            var requirements = Evaluate("repeated(4, byte(0x1234) == 56 || once(byte(0x2345) == 67))");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x002345)"));
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
        public void TestAddHitsRestrictedAll()
        {
            var requirements = Evaluate("repeated(4, repeated(3, byte(0x1234) == 56) || repeated(3, byte(0x2345) == 67))");
            Assert.That(requirements.Count, Is.EqualTo(3));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
            Assert.That(requirements[0].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[0].Right.ToString(), Is.EqualTo("56"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[0].HitCount, Is.EqualTo(3));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("byte(0x002345)"));
            Assert.That(requirements[1].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[1].Right.ToString(), Is.EqualTo("67"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddHits));
            Assert.That(requirements[1].HitCount, Is.EqualTo(3));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("1"));
            Assert.That(requirements[2].Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirements[2].Right.ToString(), Is.EqualTo("0"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirements[2].HitCount, Is.EqualTo(4));
        }
    }
}
