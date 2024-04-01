using Jamiras.Components;
using NUnit.Framework;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
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

            public RichPresenceBuilder Evaluate(string input, string expectedError = null)
            {
                var funcDef = new RichPresenceLookupFunction();

                var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
                Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
                var funcCall = (FunctionCallExpression)expression;

                ExpressionBase error;
                var scope = funcCall.GetParameters(funcDef, Scope, out error);
                var context = new RichPresenceDisplayFunction.RichPresenceDisplayContext { RichPresence = new RichPresenceBuilder() };
                context.DisplayString = context.RichPresence.AddDisplayString(null, new StringConstantExpression("{0}"));
                scope.Context = context;

                ExpressionBase evaluated;
                if (expectedError != null && expectedError.EndsWith(" format"))
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
            // not providing a RichPresenceDisplayContext simulates calling the function at a global scope
            var funcDef = new RichPresenceLookupFunction();

            var input = "rich_presence_lookup(\"Name\", byte(0x1234), lookup)";
            var expression = ExpressionBase.Parse(new PositionalTokenizer(Tokenizer.CreateTokenizer(input)));
            Assert.That(expression, Is.InstanceOf<FunctionCallExpression>());
            var funcCall = (FunctionCallExpression)expression;

            var parentScope = new InterpreterScope(AchievementScriptInterpreter.GetGlobalScope());
            var dict = new DictionaryExpression();
            parentScope.DefineVariable(new VariableDefinitionExpression("lookup"), dict);

            ExpressionBase error;
            var scope = funcCall.GetParameters(funcDef, parentScope, out error);
            Assert.That(funcDef.Evaluate(scope, out error), Is.False);
            Assert.That(error, Is.InstanceOf<ErrorExpression>());
            Assert.That(((ErrorExpression)error).Message, Is.EqualTo("rich_presence_lookup has no meaning outside of a rich_presence_display call"));
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
