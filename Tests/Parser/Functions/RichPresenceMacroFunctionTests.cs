using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Tests.Parser.Functions
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

        private RichPresenceBuilder Evaluate(string input, string expectedError = null)
        {
            var funcDef = new RichPresenceMacroFunction();

            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, AchievementScriptInterpreter.GetGlobalScope(), out error);
            var context = new RichPresenceDisplayFunction.RichPresenceDisplayContext { RichPresence = new RichPresenceBuilder() };
            scope.Context = context;

            ExpressionBase evaluated;
            if (expectedError != null)
            {
                Assert.That(funcDef.ReplaceVariables(scope, out evaluated), Is.False);
                var parseError = evaluated as ErrorExpression;
                Assert.That(parseError, Is.Not.Null);
                Assert.That(parseError.Message, Is.EqualTo(expectedError));
                return context.RichPresence;
            }

            ExpressionBase result;
            Assert.That(funcDef.ReplaceVariables(scope, out evaluated), Is.True);
            Assert.That(funcDef.BuildMacro(context, scope, out result), Is.True);
            context.RichPresence.DisplayString = ((StringConstantExpression)result).Value;

            return context.RichPresence;
        }

        [Test]
        [TestCase("Number")]
        [TestCase("Score")]
        [TestCase("Centisecs")]
        [TestCase("Seconds")]
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
            Assert.That(rp.ToString(), Is.EqualTo("Display:\r\n@" + macro  +"(0xH001234)\r\n"));
        }

        [Test]
        public void TestMacroInvalid()
        {
            Evaluate("rich_presence_macro(\"unknown\", byte(0x1234))", "Unknown rich presence macro: unknown");
        }
    }
}
