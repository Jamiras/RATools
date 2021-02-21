using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Functions;
using RATools.Parser.Internal;

namespace RATools.Test.Parser
{
    [TestFixture]
    class TriggerBuilderTests
    {
        private static ExpressionBase Parse(string input)
        {
            return ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
        }

        [Test]
        [TestCase("byte(0x1234)", "0xH001234")]
        [TestCase("byte(0x1234) * 10", "0xH001234*10")]
        [TestCase("byte(0x1234) / 10", "0xH001234*0.1")]
        [TestCase("byte(0x1234) * 10 / 3", "0xH001234*3.33333333333333")]
        [TestCase("byte(0x1234) + 10", "0xH001234_v10")]
        [TestCase("(byte(0) + byte(1)) * 10", "0xH000000*10_0xH000001*10")]
        [TestCase("(byte(0) + 2) * 10", "0xH000000*10_v20")]
        [TestCase("(byte(0) + byte(1)) / 10", "0xH000000*0.1_0xH000001*0.1")]
        public void TestGetValueString(string input, string expected)
        {
            var expression = Parse(input);
            ExpressionBase error;
            InterpreterScope scope = new InterpreterScope();
            scope.Context = new TriggerBuilderContext();
            scope.AddFunction(new MemoryAccessorFunction("byte", FieldSize.Byte));

            Assert.That(TriggerBuilderContext.GetValueString(expression, scope, out error), Is.EqualTo(expected));
        }
    }
}
