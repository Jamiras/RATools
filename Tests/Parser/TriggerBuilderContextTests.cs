using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using RATools.Tests.Data;

namespace RATools.Test.Parser
{
    [TestFixture]
    class TriggerBuilderTests
    {
        private static ExpressionBase Parse(string input)
        {
            return ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
        }

        [TestCase("0 == 1", "0=1")]
        [TestCase("byte(0x1234) == 10", "0xH001234=10")]
        [TestCase("byte(0x1234) > byte(0x2345)", "0xH001234>0xH002345")]
        [TestCase("byte(0x1234) / byte(0x2345) < 10", "A:0xH001234/0xH002345_0<10")]
        [TestCase("byte(0x1234) / byte(0x2345) < 0.8", "A:0xH001234/f0.8_0<0xH002345")]
        [TestCase("byte(0x1234) * 100 / byte(0x2345) < 80", "Cannot generate condition using both Divide and Multiply")]
        public void TestGetConditionString(string input, string expected)
        {
            ExpressionBase error;
            InterpreterScope scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new TriggerBuilderContext();

            var expression = Parse(input);

            ExpressionBase processed;
            Assert.That(expression.ReplaceVariables(scope, out processed), Is.True);

            var result = TriggerBuilderContext.GetConditionString(processed, scope, out error);
            if (error != null)
            {
                Assert.That(((ParseErrorExpression)error).InnermostError.Message, Is.EqualTo(expected));
            }
            else
            {
                Assert.That(error, Is.Null);
                Assert.That(result, Is.EqualTo(expected));
            }
        }

        [Test]
        [TestCase("1", "v1")]
        [TestCase("1 + 7", "v8")]
        [TestCase("1 + 3 * 2", "v7")]
        [TestCase("byte(0x1234)", "0xH001234")]
        [TestCase("byte(0x1234) * 10", "0xH001234*10")]
        [TestCase("byte(0x1234) / 10", "0xH001234*0.1")]
        [TestCase("byte(0x1234) * 10 / 3", "0xH001234*3.333333")]
        [TestCase("byte(0x1234) + 10", "0xH001234_v10")]
        [TestCase("byte(0x1234) - 10", "0xH001234_v-10")]
        [TestCase("(byte(0) + byte(1)) * 10", "0xH000000*10_0xH000001*10")]
        [TestCase("(byte(0) + 2) * 10", "0xH000000*10_v20")]
        [TestCase("(byte(0) + byte(1)) / 10", "0xH000000*0.1_0xH000001*0.1")]
        [TestCase("byte(0x1234) * 2", "0xH001234*2")]
        [TestCase("byte(0x1234) / 2", "0xH001234*0.5")]
        [TestCase("byte(0x1234) * 100 / 2", "0xH001234*50")]
        [TestCase("byte(0x1234) * 2 / 100", "0xH001234*0.02")]
        [TestCase("byte(0x1234) + 100 - 2", "0xH001234_v98")]
        [TestCase("byte(0x1234) + 1 - 1", "0xH001234")]
        [TestCase("byte(0x1234) * 2 + 1", "0xH001234*2_v1")]
        [TestCase("byte(0x1234) * 2 - 1", "0xH001234*2_v-1")]
        [TestCase("byte(0x1234) * 256 + byte(0x2345) + 1", "0xH001234*256_0xH002345_v1")]
        [TestCase("(byte(0x1234) / (2 * 20)) * 100", "0xH001234*2.5")]
        [TestCase("byte(0x1234) * byte(0x2345)", "M:0xH001234*0xH002345")]
        [TestCase("byte(0x1234) / byte(0x2345)", "M:0xH001234/0xH002345")]
        [TestCase("byte(0x1234 + byte(0x2345))", "I:0xH002345_M:0xH001234")]
        [TestCase("byte(0x1234 + byte(0x2345)) + 1", "I:0xH002345_A:0xH001234_M:1")]
        [TestCase("byte(0x1234 + byte(0x2345)) + byte(0x1235 + byte(0x2345))", "I:0xH002345_A:0xH001234_I:0xH002345_M:0xH001235")]
        [TestCase("byte(0x1234 + byte(0x2345)) + byte(0x1235 + byte(0x2345)) + 1", "I:0xH002345_A:0xH001234_I:0xH002345_A:0xH001235_M:1")]
        [TestCase("byte(0x1234 + byte(0x2345)) + 1 + byte(0x1235 + byte(0x2345))", "I:0xH002345_A:0xH001234_A:1_I:0xH002345_M:0xH001235")]
        [TestCase("1 + byte(0x1234 + byte(0x2345))", "I:0xH002345_A:0xH001234_M:1")]
        [TestCase("byte(0x1234 + byte(0x2345)) - 1", "B:1_I:0xH002345_M:0xH001234")]
        [TestCase("byte(0x1234 + byte(0x2345)) - byte(0x1235 + byte(0x2345))", "I:0xH002345_B:0xH001235_I:0xH002345_M:0xH001234")]
        [TestCase("byte(0x1234 + byte(0x2345)) * 2", "I:0xH002345_M:0xH001234*2")]
        [TestCase("measured(byte(0x1234) != prev(byte(0x1234)))", "M:0xH001234!=d0xH001234")]
        [TestCase("measured(byte(0x1234) != prev(byte(0x1234))) && never(byte(0x2345) == 1)", "M:0xH001234!=d0xH001234_R:0xH002345=1")]
        [TestCase("tally(0, byte(0x1234) != prev(byte(0x1234))) && never(byte(0x2345) == 1)", "M:0xH001234!=d0xH001234_R:0xH002345=1")]
        [TestCase("tally(20, byte(0x1234) != prev(byte(0x1234))) && never(byte(0x2345) == 1)", "M:0xH001234!=d0xH001234.20._R:0xH002345=1")]
        [TestCase("byte(byte(0x1234) - 10)", "I:0xH001234_M:0xHfffffff6")]
        public void TestGetValueString(string input, string expected)
        {
            ExpressionBase error;
            InterpreterScope scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new TriggerBuilderContext();

            var expression = Parse(input);
            var result = TriggerBuilderContext.GetValueString(expression, scope, out error);
            Assert.That(error, Is.Null);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("byte(0x1234) / 10", "0xH001234*0.1")]
        [TestCase("byte(0x1234) * 10 / 3", "0xH001234*3.333333")]
        [TestCase("(byte(0) + byte(1)) / 10", "0xH000000*0.1_0xH000001*0.1")]
        [TestCase("byte(0x1234) * 2.0", "0xH001234*2")]
        [TestCase("byte(0x1234) / 2", "0xH001234*0.5")]
        [TestCase("byte(0x1234) * 2 / 100", "0xH001234*0.02")]
        [TestCase("(byte(0x1234) / (2 * 20)) * 100", "0xH001234*2.5")]
        public void TestGetValueStringCulture(string input, string expected)
        {
            using (var cultureOverride = new CultureOverride("fr-FR"))
            {
                ExpressionBase error;
                InterpreterScope scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
                scope.Context = new TriggerBuilderContext();

                var expression = Parse(input);
                var result = TriggerBuilderContext.GetValueString(expression, scope, out error);
                Assert.That(error, Is.Null);
                Assert.That(result, Is.EqualTo(expected));
            }
        }
    }
}
