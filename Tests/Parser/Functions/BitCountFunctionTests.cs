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
    class BitCountFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new BitCountFunction();
            Assert.That(def.Name.Name, Is.EqualTo("bitcount"));
            Assert.That(def.Parameters.Count, Is.EqualTo(1));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("accessor"));
        }

        private List<Requirement> Evaluate(string input, string expectedError = null)
        {
            var requirements = new List<Requirement>();
            var funcDef = new BitCountFunction();

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
        public void TestByte()
        {
            var requirements = Evaluate("bitcount(byte(0x1234))");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("bitcount(0x001234)"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.None));
        }

        [Test]
        [TestCase("bit0")]
        [TestCase("bit1")]
        [TestCase("bit2")]
        [TestCase("bit3")]
        [TestCase("bit4")]
        [TestCase("bit5")]
        [TestCase("bit6")]
        [TestCase("bit7")]
        public void TestBits(string func)
        {
            var requirements = Evaluate("bitcount(" + func + "(0x1234))");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo(func + "(0x001234)"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.None));
        }

        [Test]
        public void TestWord()
        {
            var requirements = Evaluate("bitcount(word(0x1234))");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("bitcount(0x001234)"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("bitcount(0x001235)"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.None));
        }

        [Test]
        public void TestTByte()
        {
            var requirements = Evaluate("bitcount(tbyte(0x1234))");
            Assert.That(requirements.Count, Is.EqualTo(3));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("bitcount(0x001234)"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("bitcount(0x001235)"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("bitcount(0x001236)"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.None));
        }

        [Test]
        public void TestDWord()
        {
            var requirements = Evaluate("bitcount(dword(0x1234))");
            Assert.That(requirements.Count, Is.EqualTo(4));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("bitcount(0x001234)"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("bitcount(0x001235)"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("bitcount(0x001236)"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("bitcount(0x001237)"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.None));
        }

        [Test]
        public void TestLowNibble()
        {
            var requirements = Evaluate("bitcount(low4(0x1234))");
            Assert.That(requirements.Count, Is.EqualTo(4));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("bit0(0x001234)"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("bit1(0x001234)"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("bit2(0x001234)"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("bit3(0x001234)"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.None));
        }

        [Test]
        public void TestHighNibble()
        {
            var requirements = Evaluate("bitcount(high4(0x1234))");
            Assert.That(requirements.Count, Is.EqualTo(4));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("bit4(0x001234)"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("bit5(0x001234)"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("bit6(0x001234)"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("bit7(0x001234)"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.None));
        }

        [Test]
        public void TestByteAddAddress()
        {
            var requirements = Evaluate("bitcount(byte(0x1234 + word(0x2222)))");
            Assert.That(requirements.Count, Is.EqualTo(2));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("word(0x002222)"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddAddress));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("bitcount(0x001234)"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.None));
        }

        [Test]
        public void TestWordAddAddress()
        {
            var requirements = Evaluate("bitcount(word(0x1234 + word(0x2222)))");
            Assert.That(requirements.Count, Is.EqualTo(4));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("word(0x002222)"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddAddress));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("bitcount(0x001234)"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("word(0x002222)"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.AddAddress));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("bitcount(0x001235)"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.None));
        }

        [Test]
        public void TestLowNibbleAddAddress()
        {
            var requirements = Evaluate("bitcount(low4(0x1234 + word(0x2222)))");
            Assert.That(requirements.Count, Is.EqualTo(8));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("word(0x002222)"));
            Assert.That(requirements[0].Type, Is.EqualTo(RequirementType.AddAddress));
            Assert.That(requirements[1].Left.ToString(), Is.EqualTo("bit0(0x001234)"));
            Assert.That(requirements[1].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[2].Left.ToString(), Is.EqualTo("word(0x002222)"));
            Assert.That(requirements[2].Type, Is.EqualTo(RequirementType.AddAddress));
            Assert.That(requirements[3].Left.ToString(), Is.EqualTo("bit1(0x001234)"));
            Assert.That(requirements[3].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[4].Left.ToString(), Is.EqualTo("word(0x002222)"));
            Assert.That(requirements[4].Type, Is.EqualTo(RequirementType.AddAddress));
            Assert.That(requirements[5].Left.ToString(), Is.EqualTo("bit2(0x001234)"));
            Assert.That(requirements[5].Type, Is.EqualTo(RequirementType.AddSource));
            Assert.That(requirements[6].Left.ToString(), Is.EqualTo("word(0x002222)"));
            Assert.That(requirements[6].Type, Is.EqualTo(RequirementType.AddAddress));
            Assert.That(requirements[7].Left.ToString(), Is.EqualTo("bit3(0x001234)"));
            Assert.That(requirements[7].Type, Is.EqualTo(RequirementType.None));
        }

        [Test]
        public void TestExplicitCall()
        {
            // not providing a TriggerBuilderContext simulates calling the function at a global scope
            var funcDef = new BitCountFunction();

            var input = "bitcount(byte(0x1234))";
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, AchievementScriptInterpreter.GetGlobalScope(), out error);
            Assert.That(funcDef.Evaluate(scope, out error), Is.False);
            Assert.That(error, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)error).Message, Is.EqualTo("bitcount has no meaning outside of a trigger clause"));
        }
    }
}
