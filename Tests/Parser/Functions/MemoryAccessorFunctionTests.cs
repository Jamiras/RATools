using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Parser.Tests.Functions
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

        private static List<Requirement> Evaluate(string input, string expectedError = null)
        {
            var requirements = new List<Requirement>();
            var context = new TriggerBuilderContext { Trigger = requirements };

            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = context;

            ExpressionBase evaluated;
            funcCall.ReplaceVariables(scope, out evaluated);
            var error = evaluated as ErrorExpression;
            if (error == null)
            {
                var accessor = evaluated as MemoryAccessorExpression;
                Assert.That(accessor, Is.Not.Null);
                error = accessor.BuildTrigger(context);
            }

            if (expectedError == null)
            {
                Assert.That(error, Is.Null);
            }
            else
            {
                Assert.That(error, Is.Not.Null);

                if (error.InnerError != null)
                    Assert.That(error.InnermostError.Message, Is.EqualTo(expectedError));
                else
                    Assert.That(error.Message, Is.EqualTo(expectedError));
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
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());

            var input = "byte(0x1234)";
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase evaluated;
            Assert.That(funcCall.ReplaceVariables(scope, out evaluated), Is.True);
            var error = evaluated as ErrorExpression;
            if (error == null)
            {
                var accessor = evaluated as MemoryAccessorExpression;
                Assert.That(accessor, Is.Not.Null);
                error = accessor.Execute(scope);
            }

            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Is.EqualTo("byte has no meaning outside of a trigger clause"));
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
        [TestCase("word_be(0x1234)", FieldSize.BigEndianWord)]
        [TestCase("tbyte_be(0x1234)", FieldSize.BigEndianTByte)]
        [TestCase("dword_be(0x1234)", FieldSize.BigEndianDWord)]
        [TestCase("float(0x1234)", FieldSize.Float)]
        [TestCase("float_be(0x1234)", FieldSize.BigEndianFloat)]
        [TestCase("mbf32(0x1234)", FieldSize.MBF32)]
        [TestCase("mbf32_le(0x1234)", FieldSize.LittleEndianMBF32)]
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
        [TestCase("byte(word(0x1234))", "byte(word(0x001234))")] // direct pointer
        [TestCase("byte(word(0x1234) + 10)", "byte(word(0x001234) + 0x00000A)")] // indirect pointer
        [TestCase("byte(10 + word(0x1234))", "byte(word(0x001234) + 0x00000A)")] // indirect pointer
        [TestCase("byte(0x1234 + word(0x2345))", "byte(word(0x002345) + 0x001234)")] // array index
        [TestCase("byte(word(word(0x1234)))", "byte(word(word(0x001234)))")] // double direct pointer
        [TestCase("byte(0x1234 + word(word(0x2345) + 10))", "byte(word(word(0x002345) + 0x00000A) + 0x001234)")] // double indirect pointer
        [TestCase("byte(prev(word(0x1234)))", "byte(prev(word(0x001234)))")] // direct pointer using prev data
        [TestCase("byte(word(0x1234) * 2)", "byte(word(0x001234) * 0x00000002)")] // scaled direct pointer [unexpected]
        [TestCase("byte(word(0x2345) * 2 + 0x1234)", "byte(word(0x002345) * 0x00000002 + 0x001234)")] // scaled array index
        [TestCase("byte(0x1234 + word(0x2345) * 2)", "byte(word(0x002345) * 0x00000002 + 0x001234)")] // scaled array index
        [TestCase("byte(word(word(0x2345) * 2 + 0x1234) * 4 + 0x3456)",
                  "byte(word(word(0x002345) * 0x00000002 + 0x001234) * 0x00000004 + 0x003456)")] // double scaled array index
        [TestCase("bit(3, word(0x1234))", "bit3(word(0x001234))")] // direct pointer
        [TestCase("bit(18, word(0x1234))", "bit2(word(0x001234) + 0x000002)")] // direct pointer
        [TestCase("bit(3, word(0x1234) + 10)", "bit3(word(0x001234) + 0x00000A)")] // indirect pointer
        [TestCase("bit(18, word(0x1234) + 10)", "bit2(word(0x001234) + 0x00000C)")] // indirect pointer
        [TestCase("bit(18, 10 + word(0x1234))", "bit2(word(0x001234) + 0x00000C)")] // indirect pointer
        [TestCase("bit(18, prev(word(0x1234)))", "bit2(prev(word(0x001234)) + 0x000002)")] // direct pointer using prev data
        [TestCase("bit(18, 0x1234 + word(0x2345) * 2)", "bit2(word(0x002345) * 0x00000002 + 0x001236)")] // scaled array index
        [TestCase("byte(word(0x1234) - 10)", "byte(word(0x001234) + 0xFFFFFFF6)")]
        [TestCase("byte(word(0x1234) / 2)", "byte(word(0x001234) / 0x00000002)")]
        [TestCase("byte(word(0x1234) & 0x1FF)", "byte(word(0x001234) & 0x000001FF)")]
        [TestCase("byte((word(0x1234) & 0x1FF) + 99)", "byte((word(0x001234) & 0x000001FF) + 0x000063)")]
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
        [TestCase("byte(repeated(4, word(0x1234) == 3))", "Cannot convert to an address: repeated(4, word(0x001234) == 3)")]
        [TestCase("byte(word(0x1234) == 3)", "Cannot convert to an address: word(0x001234) == 3")]
        [TestCase("byte(word(0x1234) == 3 && 2 > 1)", "Cannot convert to an address: word(0x001234) == 3")] // 2>1 is true and will be removed before reporting the error
        public void TestInvalidAddress(string input, string error)
        {
            Evaluate(input, error);
        }
    }
}
