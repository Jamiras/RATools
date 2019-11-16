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
    class MemoryAccessorFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new MemoryAccessorFunction("byte", FieldSize.Byte);
            Assert.That(def.Name.Name, Is.EqualTo("byte"));
            Assert.That(def.Parameters.Count, Is.EqualTo(1));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("address"));
        }

        [Test]
        public void TestDefinitionBit()
        {
            var def = new BitFunction();
            Assert.That(def.Name.Name, Is.EqualTo("bit"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("index"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("address"));
        }

        private List<Requirement> Evaluate(string input, string expectedError = null)
        {
            var requirements = new List<Requirement>();

            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            var funcDef = scope.GetFunction(funcCall.FunctionName.Name) as MemoryAccessorFunction;
            Assert.That(funcDef, Is.Not.Null);

            var context = new TriggerBuilderContext { Trigger = requirements };
            scope.Context = context;

            ExpressionBase evaluated;
            Assert.That(funcCall.ReplaceVariables(scope, out evaluated), Is.True);

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
            var requirements = Evaluate("byte(0x1234)");
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("byte(0x001234)"));
        }

        [Test]
        public void TestExplicitCall()
        {
            // not providing a TriggerBuilderContext simulates calling the function at a global scope
            var funcDef = new MemoryAccessorFunction("byte", FieldSize.Byte);

            var input = "byte(0x1234)";
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, AchievementScriptInterpreter.GetGlobalScope(), out error);
            Assert.That(funcDef.Evaluate(scope, out error), Is.False);
            Assert.That(error, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)error).Message, Is.EqualTo("byte has no meaning outside of a trigger clause"));
        }

        [Test]
        public void TestSizes()
        {
            var scope = AchievementScriptInterpreter.GetGlobalScope();
            var sizes = new Dictionary<string, FieldSize>
            {
                {"byte(0x1234)", FieldSize.Byte },
                {"bit0(0x1234)", FieldSize.Bit0 },
                {"bit1(0x1234)", FieldSize.Bit1 },
                {"bit2(0x1234)", FieldSize.Bit2 },
                {"bit3(0x1234)", FieldSize.Bit3 },
                {"bit4(0x1234)", FieldSize.Bit4 },
                {"bit5(0x1234)", FieldSize.Bit5 },
                {"bit6(0x1234)", FieldSize.Bit6 },
                {"bit7(0x1234)", FieldSize.Bit7 },
                {"low4(0x1234)", FieldSize.LowNibble },
                {"high4(0x1234)", FieldSize.HighNibble },
                {"word(0x1234)", FieldSize.Word },
                {"tbyte(0x1234)", FieldSize.TByte },
                {"dword(0x1234)", FieldSize.DWord },
                {"bit(0,0x1234)", FieldSize.Bit0 },
                {"bit(1,0x1234)", FieldSize.Bit1 },
                {"bit(2,0x1234)", FieldSize.Bit2 },
                {"bit(3,0x1234)", FieldSize.Bit3 },
                {"bit(4,0x1234)", FieldSize.Bit4 },
                {"bit(5,0x1234)", FieldSize.Bit5 },
                {"bit(6,0x1234)", FieldSize.Bit6 },
                {"bit(7,0x1234)", FieldSize.Bit7 },
            };

            foreach (var kvp in sizes)
            {
                var requirements = Evaluate(kvp.Key);
                Assert.That(requirements.Count, Is.EqualTo(1), kvp.Key);
                Assert.That(requirements[0].Left.Size, Is.EqualTo(kvp.Value), kvp.Key);
            }
        }

        [Test]
        public void TestBitOffset()
        {
            var requirements = Evaluate("bit(3, 0x1234)");
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("bit3(0x001234)"));

            requirements = Evaluate("bit(8, 0x1234)");
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("bit0(0x001235)"));

            requirements = Evaluate("bit(18, 0x1234)");
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("bit2(0x001236)"));

            requirements = Evaluate("bit(31, 0x1234)");
            Assert.That(requirements[0].Left.ToString(), Is.EqualTo("bit7(0x001237)"));

            requirements = Evaluate("bit(32, 0x1234)", "index must be between 0 and 31");
        }
    }
}
