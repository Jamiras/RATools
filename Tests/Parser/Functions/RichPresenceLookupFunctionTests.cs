using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Tests.Expressions;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class RichPresenceLookupFunctionTests
    {
        class RichPresenceLookupFunctionHarness
        {
            public RichPresenceLookupFunctionHarness()
            {
                Scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            }

            public InterpreterScope Scope { get; private set; }

            public RichPresenceBuilder Evaluate(string input)
            {
                input = "rich_presence_display(\"{0}\", " + input + ")";
                var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
                Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
                var funcCall = (FunctionCallExpression)expression;

                var context = new AchievementScriptContext { RichPresence = new RichPresenceBuilder() };
                Scope.Context = context;
                funcCall.Execute(Scope);

                return context.RichPresence;
            }

            public DictionaryExpression DefineLookup(string name)
            {
                var dict = new DictionaryExpression();
                Scope.DefineVariable(new VariableDefinitionExpression(name), dict);
                return dict;
            }
        }

        [Test]
        public void TestDefinition()
        {
            var def = new RichPresenceLookupFunction();
            Assert.That(def.Name.Name, Is.EqualTo("rich_presence_lookup"));
            Assert.That(def.Parameters.Count, Is.EqualTo(4));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("name"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("expression"));
            Assert.That(def.Parameters.ElementAt(2).Name, Is.EqualTo("dictionary"));
            Assert.That(def.Parameters.ElementAt(3).Name, Is.EqualTo("fallback"));

            Assert.That(def.DefaultParameters["fallback"], Is.EqualTo(new StringConstantExpression("")));
        }

        [Test]
        public void TestSimple()
        {
            var rp = new RichPresenceLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            lookup.Add(new IntegerConstantExpression(0), new StringConstantExpression("False"));
            lookup.Add(new IntegerConstantExpression(1), new StringConstantExpression("True"));

            var builder = rp.Evaluate("rich_presence_lookup(\"Name\", byte(0x1234), lookup)");
            Assert.That(builder.ToString(), Is.EqualTo("Lookup:Name\r\n0=False\r\n1=True\r\n\r\nDisplay:\r\n@Name(0xH001234)\r\n"));
        }

        [Test]
        public void TestExplicitCall()
        {
            var input = "rich_presence_lookup(\"Name\", byte(0x1234), lookup)";
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            var context = new AchievementScriptContext { RichPresence = new RichPresenceBuilder() };
            var scope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            scope.DefineVariable(new VariableDefinitionExpression("lookup"), new DictionaryExpression());
            scope.Context = context;
            var error = funcCall.Execute(scope);

            ExpressionTests.AssertError(error, "rich_presence_lookup has no meaning outside of a rich_presence_display call");
        }

        [Test]
        public void TestValueExpression()
        {
            // more expressions are validated via TriggerBuilderTests.TestGetValueString
            var rp = new RichPresenceLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            lookup.Add(new IntegerConstantExpression(0), new StringConstantExpression("False"));

            var builder = rp.Evaluate("rich_presence_lookup(\"Name\", byte(0x1234) + 1, lookup)");
            var rpString = builder.ToString();
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
            var rp = new RichPresenceLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            lookup.Add(new IntegerConstantExpression(0), new StringConstantExpression("False"));
            lookup.Add(new IntegerConstantExpression(1), new StringConstantExpression("True"));

            var builder = rp.Evaluate("rich_presence_lookup(\"Name\", 1, lookup)");
            Assert.That(builder.ToString(), Is.EqualTo("Display:\r\nTrue\r\n"));
        }

        [Test]
        public void TestValueConstantFallback()
        {
            var rp = new RichPresenceLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            lookup.Add(new IntegerConstantExpression(0), new StringConstantExpression("False"));
            lookup.Add(new IntegerConstantExpression(1), new StringConstantExpression("True"));

            var builder = rp.Evaluate("rich_presence_lookup(\"Name\", 2, lookup, \"Fallback\")");
            Assert.That(builder.ToString(), Is.EqualTo("Display:\r\nFallback\r\n"));
        }
    }
}
