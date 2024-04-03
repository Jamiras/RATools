using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Tests.Expressions;
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

        private static RichPresenceBuilder Evaluate(string input)
        {
            input = "rich_presence_display(\"{0}\", " + input + ")";
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            var context = new AchievementScriptContext { RichPresence = new RichPresenceBuilder() };
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = context;
            funcCall.Execute(scope);

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
            var input = "rich_presence_display(\"{0}\", rich_presence_macro(\"unknown\", byte(0x1234)))";
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            var context = new AchievementScriptContext { RichPresence = new RichPresenceBuilder() };
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = context;
            var error = funcCall.Execute(scope);

            ExpressionTests.AssertError(error, "Unknown rich presence macro: unknown");
        }
    }
}
