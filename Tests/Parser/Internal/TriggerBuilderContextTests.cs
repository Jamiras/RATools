﻿using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
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
        [TestCase("byte(0x1234) * 100 / byte(0x2345) < 80", "K:0xH001234*100_A:{recall}/0xH002345_0<80")]
        [TestCase("dword(dword(0x1234) + ((word(0x2345) & 0x3FF) * 8 + 4)) == 6 && prev(dword(dword(0x1234) + ((word(0x2345) & 0x3FF) * 8 + 4))) == 0",
                  "K:0x 002345&1023_A:{recall}*8_A:4_K:0xX001234_I:{recall}_0xX000000=6_I:{recall}_d0xX000000=0")]
        public void TestGetConditionString(string input, string expected)
        {
            ExpressionBase error;
            InterpreterScope scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = new TriggerBuilderContext();

            var expression = Parse(input);

            ExpressionBase processed;
            Assert.That(expression.ReplaceVariables(scope, out processed), Is.True);

            var result = TriggerBuilderContext.GetConditionString(processed, scope, new SerializationContext(), out error);
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
