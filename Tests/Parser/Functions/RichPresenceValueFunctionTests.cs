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
    class RichPresenceValueFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new RichPresenceValueFunction();
            Assert.That(def.Name.Name, Is.EqualTo("rich_presence_value"));
            Assert.That(def.Parameters.Count, Is.EqualTo(3));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("name"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("expression"));
            Assert.That(def.Parameters.ElementAt(2).Name, Is.EqualTo("format"));

            Assert.That(def.DefaultParameters["format"], Is.EqualTo(new StringConstantExpression("value")));
        }

        private RichPresenceBuilder Evaluate(string input, string expectedError = null)
        {
            var requirements = new List<Requirement>();
            var funcDef = new RichPresenceValueFunction();

            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, AchievementScriptInterpreter.GetGlobalScope(), out error);
            var context = new RichPresenceDisplayFunction.RichPresenceDisplayContext { RichPresence = new RichPresenceBuilder() };
            scope.Context = context;

            ExpressionBase evaluated;
            if (expectedError != null && expectedError.EndsWith(" format"))
            {
                Assert.That(funcDef.ReplaceVariables(scope, out evaluated), Is.False);
                var parseError = evaluated as ParseErrorExpression;
                Assert.That(parseError, Is.Not.Null);
                Assert.That(parseError.Message, Is.EqualTo(expectedError));
                return context.RichPresence;
            }

            Assert.That(funcDef.ReplaceVariables(scope, out evaluated), Is.True);
            if (expectedError == null)
            {
                Assert.That(funcDef.BuildMacro(context, scope, (FunctionCallExpression)evaluated), Is.Null);
            }
            else
            {
                var parseError = funcDef.BuildMacro(context, scope, (FunctionCallExpression)evaluated);
                Assert.That(parseError, Is.Not.Null);
                Assert.That(parseError.Message, Is.EqualTo(expectedError));
            }

            context.RichPresence.DisplayString = context.DisplayString.ToString();
            return context.RichPresence;
        }

        [Test]
        public void TestSimple()
        {
            var rp = Evaluate("rich_presence_value(\"Name\", byte(0x1234))");
            Assert.That(rp.ToString(), Is.EqualTo("Format:Name\r\nFormatType=VALUE\r\n\r\nDisplay:\r\n@Name(0xH001234)\r\n"));
        }

        [Test]
        public void TestExplicitCall()
        {
            // not providing a RichPresenceDisplayContext simulates calling the function at a global scope
            var funcDef = new RichPresenceValueFunction();

            var input = "rich_presence_value(\"Name\", byte(0x1234))";
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, AchievementScriptInterpreter.GetGlobalScope(), out error);
            Assert.That(funcDef.Evaluate(scope, out error), Is.False);
            Assert.That(error, Is.InstanceOf<ParseErrorExpression>());
            Assert.That(((ParseErrorExpression)error).Message, Is.EqualTo("rich_presence_value has no meaning outside of a rich_presence_display call"));
        }

        [Test]
        [TestCase("byte(0x1234)", "0xH001234")]
        [TestCase("byte(0x1234) + 1", "0xH001234_v1")]
        [TestCase("byte(0x1234) - 1", "0xH001234_v-1")]
        [TestCase("byte(0x1234) * 2", "0xH001234*2")]
        [TestCase("byte(0x1234) / 2", "0xH001234*0.5")]
        [TestCase("byte(0x1234) * 100 / 2", "0xH001234*50")]
        [TestCase("byte(0x1234) * 2 / 100", "0xH001234*0.02")]
        [TestCase("byte(0x1234) + 100 - 2", "0xH001234_v98")]
        [TestCase("byte(0x1234) + 1 - 1", "0xH001234")]
        [TestCase("byte(0x1234) * 2 + 1", "0xH001234*2_v1")]
        [TestCase("byte(0x1234) * 2 - 1", "0xH001234*2_v-1")]
        [TestCase("byte(0x1234) * 256 + byte(0x2345) + 1", "0xH001234*256_0xH002345_v1")]
        [TestCase("1", "v1")]
        [TestCase("1 + 7", "v8")]
        [TestCase("1 + 3 * 2", "v7")]
        [TestCase("(byte(0x1234) / (2 * 20)) * 100", "0xH001234*2.5")]
        [TestCase("byte(0x1234 + byte(0x2345))", "I:0xH002345_M:0xH001234")]
        public void TestValueExpressions(string input, string expected)
        {
            var rp = Evaluate("rich_presence_value(\"Name\", " + input + ")");
            var rpString = rp.ToString();
            var index = rpString.IndexOf("@Name(");
            if (index == -1)
            {
                Assert.Fail("Could not find Name macro");
            }
            else
            {
                var index2 = rpString.LastIndexOf(")");
                var subString = rpString.Substring(index + 6, index2 - index - 6);
                Assert.That(subString, Is.EqualTo(expected));
            }
        }

        [Test]
        public void TestFormat()
        {
            var rp = Evaluate("rich_presence_value(\"Name\", byte(0x1234), format=\"FRAMES\")");
            Assert.That(rp.ToString(), Is.EqualTo("Format:Name\r\nFormatType=FRAMES\r\n\r\nDisplay:\r\n@Name(0xH001234)\r\n"));
        }

        [Test]
        public void TestFormatInvalid()
        {
            Evaluate("rich_presence_value(\"Name\", byte(0x1234), format=\"INVALID\")", 
                "INVALID is not a supported rich_presence_value format");
        }
    }
}
