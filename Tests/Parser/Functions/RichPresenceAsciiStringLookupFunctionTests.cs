using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class RichPresenceAsciiStringLookupFunctionTests
    {
        class RichPresenceAsciiStringLookupFunctionHarness
        {
            public RichPresenceAsciiStringLookupFunctionHarness()
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
            var def = new RichPresenceAsciiStringLookupFunction();
            Assert.That(def.Name.Name, Is.EqualTo("rich_presence_ascii_string_lookup"));
            Assert.That(def.Parameters.Count, Is.EqualTo(4));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("name"));
            Assert.That(def.Parameters.ElementAt(1).Name, Is.EqualTo("address"));
            Assert.That(def.Parameters.ElementAt(2).Name, Is.EqualTo("dictionary"));
            Assert.That(def.Parameters.ElementAt(3).Name, Is.EqualTo("fallback"));

            Assert.That(def.DefaultParameters["fallback"], Is.EqualTo(new StringConstantExpression("")));
        }

        [Test]
        public void TestSimple()
        {
            var rp = new RichPresenceAsciiStringLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            lookup.Add(new StringConstantExpression("Zero"), new StringConstantExpression("False"));
            lookup.Add(new StringConstantExpression("One"), new StringConstantExpression("True"));

            var builder = rp.Evaluate("rich_presence_ascii_string_lookup(\"Name\", 0x1234, lookup)");
            Assert.That(builder.ToString(), Is.EqualTo("Lookup:Name\r\n6647375=True\r\n1869768026=False\r\n\r\nDisplay:\r\n@Name(0xX001234)\r\n"));
        }

        [Test]
        public void TestSimplePointer()
        {
            var rp = new RichPresenceAsciiStringLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            lookup.Add(new StringConstantExpression("Zero"), new StringConstantExpression("False"));
            lookup.Add(new StringConstantExpression("One"), new StringConstantExpression("True"));

            var builder = rp.Evaluate("rich_presence_ascii_string_lookup(\"Name\", dword(0x1234), lookup)");
            var serializationContext = new SerializationContext { MinimumVersion = builder.MinimumVersion() };
            Assert.That(builder.Serialize(serializationContext), Is.EqualTo("Lookup:Name\r\n6647375=True\r\n1869768026=False\r\n\r\nDisplay:\r\n@Name(I:0xX001234_M:0xX000000)\r\n"));
        }

        [Test]
        public void TestPointerChain()
        {
            var rp = new RichPresenceAsciiStringLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            lookup.Add(new StringConstantExpression("Zero"), new StringConstantExpression("False"));
            lookup.Add(new StringConstantExpression("One"), new StringConstantExpression("True"));

            var builder = rp.Evaluate("rich_presence_ascii_string_lookup(\"Name\", dword(dword(0x1234)), lookup)");
            var serializationContext = new SerializationContext { MinimumVersion = builder.MinimumVersion() };
            Assert.That(builder.Serialize(serializationContext), Is.EqualTo("Lookup:Name\r\n6647375=True\r\n1869768026=False\r\n\r\nDisplay:\r\n@Name(I:0xX001234_I:0xX000000_M:0xX000000)\r\n"));
        }

        [Test]
        public void TestPointerChainWithOffset()
        {
            var rp = new RichPresenceAsciiStringLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            lookup.Add(new StringConstantExpression("Zero"), new StringConstantExpression("False"));
            lookup.Add(new StringConstantExpression("One"), new StringConstantExpression("True"));

            var builder = rp.Evaluate("rich_presence_ascii_string_lookup(\"Name\", dword(dword(0x1234) + 0x10) + 6, lookup)");
            var serializationContext = new SerializationContext { MinimumVersion = builder.MinimumVersion() };
            Assert.That(builder.Serialize(serializationContext), Is.EqualTo("Lookup:Name\r\n6647375=True\r\n1869768026=False\r\n\r\nDisplay:\r\n@Name(I:0xX001234_I:0xX000010_M:0xX000006)\r\n"));
        }

        [Test]
        public void TestIntegerDictionaryKey()
        {
            var input = "lookup = {\"Zero\": \"False\", 1: \"True\" }\r\n" +
                "\r\n" +
                "rich_presence_display(\"{0}\", rich_presence_ascii_string_lookup(\"Name\", 0x1234, lookup))";

            AchievementScriptTests.Evaluate(input,
                "3:30 Invalid value for parameter: ...\r\n" +
                "- 3:30 rich_presence_ascii_string_lookup call failed\r\n" +
                "- 1:28 Cannot convert integer to string");
        }

        [Test]
        public void TestCommonPrefix()
        {
            var rp = new RichPresenceAsciiStringLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            lookup.Add(new StringConstantExpression("Test_Zero"), new StringConstantExpression("False"));
            lookup.Add(new StringConstantExpression("Test_One"), new StringConstantExpression("True"));

            var builder = rp.Evaluate("rich_presence_ascii_string_lookup(\"Name\", 0x1234, lookup)");
            Assert.That(builder.ToString(), Is.EqualTo("Lookup:Name\r\n1701728095=True\r\n1919244895=False\r\n\r\nDisplay:\r\n@Name(0xX001238)\r\n"));
        }

        [Test]
        public void TestMultipleOverlaps()
        {
            var rp = new RichPresenceAsciiStringLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            // Test is not unique among the first four characters.
            // _Zer is not unique among the second four characters.
            // st_Z, st_O, and ow_Z are unique in the middle.
            lookup.Add(new StringConstantExpression("Test_Zero"), new StringConstantExpression("False"));
            lookup.Add(new StringConstantExpression("Test_One"), new StringConstantExpression("True"));
            lookup.Add(new StringConstantExpression("Slow_Zero"), new StringConstantExpression("Unknown"));

            var builder = rp.Evaluate("rich_presence_ascii_string_lookup(\"Name\", 0x1234, lookup)");
            Assert.That(builder.ToString(), Is.EqualTo("Lookup:Name\r\n1331655795=True\r\n1516205171=False\r\n1516205935=Unknown\r\n\r\nDisplay:\r\n@Name(0xX001236)\r\n"));
        }

        [Test]
        public void TestTailDiffrentiator()
        {
            var rp = new RichPresenceAsciiStringLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            lookup.Add(new StringConstantExpression("Value0"), new StringConstantExpression("False"));
            lookup.Add(new StringConstantExpression("Value1"), new StringConstantExpression("True"));

            var builder = rp.Evaluate("rich_presence_ascii_string_lookup(\"Name\", 0x1234, lookup)");
            Assert.That(builder.ToString(), Is.EqualTo("Lookup:Name\r\n811955564=False\r\n828732780=True\r\n\r\nDisplay:\r\n@Name(0xX001236)\r\n"));
        }

        [Test]
        public void TestShortStrings()
        {
            var rp = new RichPresenceAsciiStringLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            lookup.Add(new StringConstantExpression("Off"), new StringConstantExpression("False"));
            lookup.Add(new StringConstantExpression("On"), new StringConstantExpression("True"));

            var builder = rp.Evaluate("rich_presence_ascii_string_lookup(\"Name\", 0x1234, lookup)");
            Assert.That(builder.ToString(), Is.EqualTo("Lookup:Name\r\n28239=True\r\n6710863=False\r\n\r\nDisplay:\r\n@Name(0xW001234)\r\n"));
        }

        [Test]
        public void TestShortStringsPointer()
        {
            var rp = new RichPresenceAsciiStringLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            lookup.Add(new StringConstantExpression("Off"), new StringConstantExpression("False"));
            lookup.Add(new StringConstantExpression("On"), new StringConstantExpression("True"));

            var builder = rp.Evaluate("rich_presence_ascii_string_lookup(\"Name\", dword(0x1234), lookup)");
            var serializationContext = new SerializationContext { MinimumVersion = builder.MinimumVersion() };
            Assert.That(builder.Serialize(serializationContext), Is.EqualTo("Lookup:Name\r\n28239=True\r\n6710863=False\r\n\r\nDisplay:\r\n@Name(I:0xX001234_M:0xW000000)\r\n"));
        }

        [Test]
        public void TestSummedKey()
        {
            var rp = new RichPresenceAsciiStringLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            // there is no unique set of four characters, so the lookup will need to combine multiple groups of four.
            lookup.Add(new StringConstantExpression("uppercase"), new StringConstantExpression("uc"));
            lookup.Add(new StringConstantExpression("upperCase"), new StringConstantExpression("uC"));
            lookup.Add(new StringConstantExpression("UpperCase"), new StringConstantExpression("UC"));
            lookup.Add(new StringConstantExpression("lowercase"), new StringConstantExpression("lc"));
            lookup.Add(new StringConstantExpression("lowerCase"), new StringConstantExpression("lC"));
            lookup.Add(new StringConstantExpression("LowerCase"), new StringConstantExpression("LC"));

            var builder = rp.Evaluate("rich_presence_ascii_string_lookup(\"Name\", 0x1234, lookup)");
            Assert.That(builder.ToString(), Is.EqualTo("Lookup:Name\r\n-657345593=UC\r\n-657345561=uC\r\n-657337369=uc\r\n-656887106=LC\r\n-656887074=lC\r\n-656878882=lc\r\n\r\nDisplay:\r\n@Name(0xX001234_0xX001238)\r\n"));
        }

        [Test]
        public void TestSummedKeyPointer()
        {
            var rp = new RichPresenceAsciiStringLookupFunctionHarness();
            var lookup = rp.DefineLookup("lookup");
            // there is no unique set of four characters, so the lookup will need to combine multiple groups of four.
            lookup.Add(new StringConstantExpression("uppercase"), new StringConstantExpression("uc"));
            lookup.Add(new StringConstantExpression("upperCase"), new StringConstantExpression("uC"));
            lookup.Add(new StringConstantExpression("UpperCase"), new StringConstantExpression("UC"));
            lookup.Add(new StringConstantExpression("lowercase"), new StringConstantExpression("lc"));
            lookup.Add(new StringConstantExpression("lowerCase"), new StringConstantExpression("lC"));
            lookup.Add(new StringConstantExpression("LowerCase"), new StringConstantExpression("LC"));

            var builder = rp.Evaluate("rich_presence_ascii_string_lookup(\"Name\", dword(0x1234), lookup)");
            var serializationContext = new SerializationContext { MinimumVersion = builder.MinimumVersion() };
            Assert.That(builder.Serialize(serializationContext), Is.EqualTo("Lookup:Name\r\n-657345593=UC\r\n-657345561=uC\r\n-657337369=uc\r\n-656887106=LC\r\n-656887074=lC\r\n-656878882=lc\r\n\r\nDisplay:\r\n@Name(I:0xX001234_A:0xX000000_I:0xX001234_M:0xX000004)\r\n"));
        }
    }
}
