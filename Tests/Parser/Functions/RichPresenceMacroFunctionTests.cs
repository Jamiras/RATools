﻿using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class RichPresenceMacroFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new RichPresenceMacroFunction();
            Assert.That(def.Name.Name, Is.EqualTo("rich_presence_macro"));
            Assert.That(def.Parameters.Count, Is.EqualTo(2));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("macro"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("expression"));
        }

        private static RichPresenceBuilder Evaluate(string input, string expectedError = null)
        {
            var funcDef = new RichPresenceMacroFunction();

            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, AchievementScriptInterpreter.GetGlobalScope(), out error);
            var context = new RichPresenceDisplayFunction.RichPresenceDisplayContext { RichPresence = new RichPresenceBuilder() };
            context.DisplayString = context.RichPresence.AddDisplayString(null, new StringConstantExpression("{0}"));
            scope.Context = context;

            ExpressionBase evaluated;
            if (expectedError != null && expectedError.StartsWith("Unknown rich presence macro"))
            {
                Assert.That(funcDef.ReplaceVariables(scope, out evaluated), Is.False);
                var parseError = evaluated as ErrorExpression;
                Assert.That(parseError, Is.Not.Null);
                Assert.That(parseError.Message, Is.EqualTo(expectedError));
                return context.RichPresence;
            }

            ExpressionBase result;
            Assert.That(funcDef.Evaluate(scope, out result), Is.True);
            if (expectedError != null)
            {
                Assert.That(result, Is.InstanceOf<ErrorExpression>());
                Assert.That(((ErrorExpression)result).Message, Is.EqualTo(expectedError));
            }

            return context.RichPresence;
        }

        [Test]
        [TestCase("Number")]
        [TestCase("Score")]
        [TestCase("Centiseconds")]
        [TestCase("Seconds")]
        [TestCase("SecondsAsMinutes")]
        [TestCase("Minutes")]
        [TestCase("Float1")]
        [TestCase("Float2")]
        [TestCase("Float3")]
        [TestCase("Float4")]
        [TestCase("Float5")]
        [TestCase("Float6")]
        [TestCase("ASCIIChar")]
        [TestCase("UnicodeChar")]
        public void TestMacro(string macro)
        {
            var rp = Evaluate("rich_presence_macro(\"" + macro + "\", byte(0x1234))");
            var serializationContext = new SerializationContext { MinimumVersion = Version._1_0 };
            Assert.That(rp.Serialize(serializationContext), Is.EqualTo("Display:\r\n@" + macro  +"(0xH001234)\r\n"));
        }

        [Test]
        public void TestMacroInvalid()
        {
            Evaluate("rich_presence_macro(\"unknown\", byte(0x1234))", "Unknown rich presence macro: unknown");
        }
    }
}
