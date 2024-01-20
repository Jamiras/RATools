using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using RATools.Parser.Tests.Expressions;

namespace RATools.Parser.Tests.Internal
{
    [TestFixture]
    class TriggerBuilderContextTests
    {
        private static ExpressionBase Parse(string input)
        {
            return ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
        }

        [TestCase("always_false()", "0=1")]
        [TestCase("byte(0x1234) == 10", "0xH001234=10")]
        [TestCase("byte(0x1234) > byte(0x2345)", "0xH001234>0xH002345")]
        [TestCase("byte(0x1234) / byte(0x2345) < 10", "A:0xH001234/0xH002345_0<10")]
        [TestCase("byte(0x1234) / byte(0x2345) < 0.8", "A:0xH001234/0xH002345_0<f0.8")]
        [TestCase("byte(0x1234) * 100 / byte(0x2345) < 80", "expression is not a requirement")]
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
                ExpressionTests.AssertError(error, expected);
            }
            else
            {
                Assert.That(error, Is.Null);
                Assert.That(result, Is.EqualTo(expected));
            }
        }
    }
}
