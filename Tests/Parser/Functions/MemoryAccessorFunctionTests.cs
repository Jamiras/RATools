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
        [TestCase("byte(0x1234)", FieldSize.Byte)]
        [TestCase("bit0(0x1234)", FieldSize.Bit0)]
        [TestCase("bit1(0x1234)", FieldSize.Bit1)]
        [TestCase("bit2(0x1234)", FieldSize.Bit2)]
        [TestCase("bit3(0x1234)", FieldSize.Bit3)]
        [TestCase("bit4(0x1234)", FieldSize.Bit4)]
        [TestCase("bit5(0x1234)", FieldSize.Bit5)]
        [TestCase("bit6(0x1234)", FieldSize.Bit6)]
        [TestCase("bit7(0x1234)", FieldSize.Bit7)]
        [TestCase("low4(0x1234)", FieldSize.LowNibble)]
        [TestCase("high4(0x1234)", FieldSize.HighNibble)]
        [TestCase("word(0x1234)", FieldSize.Word)]
        [TestCase("tbyte(0x1234)", FieldSize.TByte)]
        [TestCase("dword(0x1234)", FieldSize.DWord)]
        [TestCase("bit(0,0x1234)", FieldSize.Bit0)]
        [TestCase("bit(1,0x1234)", FieldSize.Bit1)]
        [TestCase("bit(2,0x1234)", FieldSize.Bit2)]
        [TestCase("bit(3,0x1234)", FieldSize.Bit3)]
        [TestCase("bit(4,0x1234)", FieldSize.Bit4)]
        [TestCase("bit(5,0x1234)", FieldSize.Bit5)]
        [TestCase("bit(6,0x1234)", FieldSize.Bit6)]
        [TestCase("bit(7,0x1234)", FieldSize.Bit7)]
        [TestCase("bitcount(0x1234)", FieldSize.BitCount)]
        public void TestSize(string func, FieldSize expectedSize)
        {
            var requirements = Evaluate(func);
            Assert.That(requirements.Count, Is.EqualTo(1));
            Assert.That(requirements[0].Left.Size, Is.EqualTo(expectedSize));
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

            Evaluate("bit(32, 0x1234)", "index must be between 0 and 31");
        }

        [Test]
        [TestCase("byte(word(0x1234))", "byte(word(0x001234) + 0x000000)")] // direct pointer
        [TestCase("byte(word(0x1234) + 10)", "byte(word(0x001234) + 0x00000A)")] // indirect pointer
        [TestCase("byte(10 + word(0x1234))", "byte(word(0x001234) + 0x00000A)")] // indirect pointer
        [TestCase("byte(0x1234 + word(0x2345))", "byte(word(0x002345) + 0x001234)")] // array index
        [TestCase("byte(word(word(0x1234)))", "byte(word(word(0x001234) + 0x000000) + 0x000000)")] // double direct pointer
        [TestCase("byte(0x1234 + word(word(0x2345) + 10))", "byte(word(word(0x002345) + 0x00000A) + 0x001234)")] // double indirect pointer
        [TestCase("byte(prev(word(0x1234)))", "byte(prev(word(0x001234)) + 0x000000)")] // direct pointer using prev data
        [TestCase("byte(word(0x1234) * 2)", "byte(word(0x001234) * 0x00000002 + 0x000000)")] // scaled direct pointer [unexpected]
        [TestCase("byte(word(0x2345) * 2 + 0x1234)", "byte(word(0x002345) * 0x00000002 + 0x001234)")] // scaled array index
        [TestCase("byte(0x1234 + word(0x2345) * 2)", "byte(word(0x002345) * 0x00000002 + 0x001234)")] // scaled array index
        [TestCase("byte(word(word(0x2345) * 2 + 0x1234) * 4 + 0x3456)",
                  "byte(word(word(0x002345) * 0x00000002 + 0x001234) * 0x00000004 + 0x003456)")] // double scaled array index
        public void TestAddAddress(string input, string expected)
        {
            var requirements = Evaluate(input);

            var builder = new StringBuilder();
            AchievementBuilder.AppendStringGroup(builder, requirements, NumberFormat.Hexadecimal);

            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase("byte(word(0x1234) + word(0x2345))", "Cannot construct single address lookup from multiple memory references")]
        [TestCase("byte(0x5555 + word(0x1234) + word(0x2345))", "Cannot construct single address lookup from multiple memory references")]
        [TestCase("byte(word(0x1234) + 0x5555 + word(0x2345))", "Cannot construct single address lookup from multiple memory references")]
        [TestCase("byte(word(0x1234) + word(0x2345) + 0x5555)", "Cannot construct single address lookup from multiple memory references")]
        [TestCase("byte(repeated(4, word(0x1234) == 3))", "Cannot convert to an address: repeated(4, word(4660) == 3)")]
        [TestCase("byte(word(0x1234) == 3)", "Cannot convert to an address: word(4660) == 3")]
        [TestCase("byte(word(0x1234) == 3 && 2 > 1)", "Cannot convert to an address: word(4660) == 3 && 2 > 1")]
        [TestCase("byte(word(0x1234) - 10)", "Negative relative offset not supported")]
        public void TestInvalidAddress(string input, string error)
        {
            Evaluate(input, error);
        }
    }
}
