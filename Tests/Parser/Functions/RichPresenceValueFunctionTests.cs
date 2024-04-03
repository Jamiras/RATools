﻿using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Tests.Expressions;
using System.Linq;

namespace RATools.Parser.Tests.Functions
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
        public void TestSimple()
        {
            var rp = Evaluate("rich_presence_value(\"Name\", byte(0x1234))");
            Assert.That(rp.ToString(), Is.EqualTo("Format:Name\r\nFormatType=VALUE\r\n\r\nDisplay:\r\n@Name(0xH001234)\r\n"));
        }

        [Test]
        public void TestExplicitCall()
        {
            // not providing a RichPresenceDisplayContext simulates calling the function at a global scope
            var input = "rich_presence_value(\"Name\", byte(0x1234))";
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            var context = new AchievementScriptContext { RichPresence = new RichPresenceBuilder() };
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = context;
            var error = funcCall.Execute(scope);

            ExpressionTests.AssertError(error, "rich_presence_value has no meaning outside of a rich_presence_display call");
        }

        [Test]
        public void TestValueExpression()
        {
            // more expressions are validated via TriggerBuilderTests.TestGetValueString
            var rp = Evaluate("rich_presence_value(\"Name\", byte(0x1234) + 1)");
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
                Assert.That(subString, Is.EqualTo("0xH001234_v1"));
            }
        }

        [Test]
        public void TestValueConstant()
        {
            var rp = Evaluate("rich_presence_value(\"Name\", 1)");
            Assert.That(rp.ToString(), Is.EqualTo("Format:Name\r\nFormatType=VALUE\r\n\r\nDisplay:\r\n@Name(v1)\r\n"));
        }

        [Test]
        [TestCase("VALUE", "VALUE")]
        [TestCase("SECS", "SECS")]
        [TestCase("TIMESECS", "SECS")]
        [TestCase("FRAMES", "FRAMES")]
        [TestCase("TIME", "FRAMES")]
        [TestCase("POINTS", "SCORE")]
        [TestCase("SCORE", "SCORE")]
        [TestCase("CENTISECS", "MILLISECS")]
        [TestCase("MILLISECS", "MILLISECS")]
        [TestCase("MINUTES", "MINUTES")]
        [TestCase("SECS_AS_MINS", "SECS_AS_MINS")]
        [TestCase("OTHER", "OTHER")]
        [TestCase("FLOAT1", "FLOAT1")]
        [TestCase("FLOAT2", "FLOAT2")]
        [TestCase("FLOAT3", "FLOAT3")]
        [TestCase("FLOAT4", "FLOAT4")]
        [TestCase("FLOAT5", "FLOAT5")]
        [TestCase("FLOAT6", "FLOAT6")]
        public void TestFormat(string format, string expectedFormat)
        {
            var rp = Evaluate("rich_presence_value(\"Name\", byte(0x1234), format=\"" + format + "\")");
            Assert.That(rp.ToString(), Is.EqualTo("Format:Name\r\nFormatType=" + expectedFormat + "\r\n\r\nDisplay:\r\n@Name(0xH001234)\r\n"));
        }

        [Test]
        public void TestFormatInvalid()
        {
            var input = "rich_presence_display(\"{0}\", rich_presence_value(\"Name\", byte(0x1234), format=\"INVALID\"))";
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            var context = new AchievementScriptContext { RichPresence = new RichPresenceBuilder() };
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.Context = context;
            var error = funcCall.Execute(scope);

            ExpressionTests.AssertError(error, "INVALID is not a supported rich_presence_value format");
        }
    }
}
