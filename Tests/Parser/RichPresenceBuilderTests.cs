using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Expressions.Trigger;
using System.Collections.Generic;

namespace RATools.Parser.Tests
{
    [TestFixture]
    class RichPresenceBuilderTests
    {
        private static DictionaryExpression CreateDictionaryExpression(IDictionary<int, string> dict)
        {
            var expr = new DictionaryExpression();
            foreach (var kvp in dict)
                expr.Add(new IntegerConstantExpression(kvp.Key), new StringConstantExpression(kvp.Value));

            return expr;
        }

        private static void AddMacroReference(RichPresenceBuilder builder,
            RichPresenceBuilder.ConditionalDisplayString displayString, string macroName, ValueFormat format)
        {
            var macroExpression = new RichPresenceValueExpression(new StringConstantExpression(macroName), null) { Format = format };
            macroExpression.Attach(builder);

            displayString.AddParameter(displayString.ParameterCount, macroExpression, null);
        }

        private static void AddLookupReference(RichPresenceBuilder builder,
            RichPresenceBuilder.ConditionalDisplayString displayString, string macroName,
            DictionaryExpression values, string fallbackValue = "")
        {
            var macroExpression = new RichPresenceLookupExpression(new StringConstantExpression(macroName), null)
            {
                Items = values,
                Fallback = new StringConstantExpression(fallbackValue),
            };
            macroExpression.Attach(builder);

            displayString.AddParameter(displayString.ParameterCount, macroExpression, null);
        }

        [Test]
        public void TestConditionDisplayString()
        {
            var builder = new RichPresenceBuilder();
            builder.AddDisplayString(Trigger.Deserialize("0xH1234=1"), new StringConstantExpression("One"));
            builder.AddDisplayString(Trigger.Deserialize("0xH1234=2"), new StringConstantExpression("Two"));
            builder.AddDisplayString(null, new StringConstantExpression("Something Else"));

            var serializationContext = new SerializationContext { MinimumVersion = Version.MinimumVersion };
            Assert.That(builder.Serialize(serializationContext).Replace("\r\n", "\n"), Is.EqualTo(
                "Display:\n" +
                "?0xH001234=1?One\n" +
                "?0xH001234=2?Two\n" +
                "Something Else\n"
            ));
        }

        [Test]
        public void TestValueFields()
        {
            // explicitly initialize out of order
            var builder = new RichPresenceBuilder();
            var displayString = builder.AddDisplayString(null, new StringConstantExpression("@Val(0xH1234) @Score(0xH2345)"));
            AddMacroReference(builder, displayString, "Val", ValueFormat.Value);
            AddMacroReference(builder, displayString, "Score", ValueFormat.Score);

            Assert.That(builder.Serialize(new SerializationContext()).Replace("\r\n", "\n"), Is.EqualTo(
                "Format:Score\n" +
                "FormatType=SCORE\n" +
                "\n" +
                "Format:Val\n" +
                "FormatType=VALUE\n" +
                "\n" +
                "Display:\n" +
                "@Val(0xH1234) @Score(0xH2345)\n"
            ));
        }

        [Test]
        public void TestValueFieldsBuiltIn()
        {
            var builder = new RichPresenceBuilder();
            var displayString = builder.AddDisplayString(null, new StringConstantExpression("@Val(0xH1234) @Score(0xH2345)"));
            AddMacroReference(builder, displayString, "Val", ValueFormat.Value);
            AddMacroReference(builder, displayString, "Score", ValueFormat.Score);

            var serializationContext = new SerializationContext { MinimumVersion = Version._1_0 };
            Assert.That(builder.Serialize(serializationContext).Replace("\r\n", "\n"), Is.EqualTo(
                "Format:Val\n" +
                "FormatType=VALUE\n" +
                "\n" +
                "Display:\n" +
                "@Val(0xH1234) @Score(0xH2345)\n"
            ));
        }

        [Test]
        public void TestLookupFieldSimple()
        {
            // explicitly initialize out of order
            var dict = new DictionaryExpression();
            dict.Add(new IntegerConstantExpression(1), new StringConstantExpression("One"));
            dict.Add(new IntegerConstantExpression(3), new StringConstantExpression("Three"));
            dict.Add(new IntegerConstantExpression(5), new StringConstantExpression("Five"));
            dict.Add(new IntegerConstantExpression(4), new StringConstantExpression("Four"));
            dict.Add(new IntegerConstantExpression(2), new StringConstantExpression("Two"));

            var builder = new RichPresenceBuilder();
            var displayString = builder.AddDisplayString(null, new StringConstantExpression("@L(0xH1234)"));
            AddLookupReference(builder, displayString, "L", dict);

            var serializationContext = new SerializationContext { MinimumVersion = Version.MinimumVersion };
            Assert.That(builder.Serialize(serializationContext).Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:L\n" +
                "1=One\n" +
                "2=Two\n" +
                "3=Three\n" +
                "4=Four\n" +
                "5=Five\n" +
                "\n" +
                "Display:\n" +
                "@L(0xH1234)\n"
            ));
        }

        [Test]
        public void TestLookupFieldWithFallbackValue()
        {
            var dict = new Dictionary<int, string>
            {
                { 1, "Yes" },
                { 0, "No" },
            };

            var builder = new RichPresenceBuilder();
            var displayString = builder.AddDisplayString(null, new StringConstantExpression("@YesNo(0xH1234)"));
            AddLookupReference(builder, displayString, "YesNo", CreateDictionaryExpression(dict), "?");

            var serializationContext = new SerializationContext { MinimumVersion = Version.MinimumVersion };
            Assert.That(builder.Serialize(serializationContext).Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:YesNo\n" +
                "0=No\n" +
                "1=Yes\n" +
                "*=?\n" +
                "\n" +
                "Display:\n" +
                "@YesNo(0xH1234)\n"
            ));
        }

        [Test]
        public void TestLookupFieldWithSharedEntries()
        {
            var dict = new Dictionary<int, string>
            {
                { 1, "One" },
                { 2, "Two" },
                { 3, "Three" },
                { 4, "Two" },
                { 5, "Five" },
            };

            var builder = new RichPresenceBuilder();
            var displayString = builder.AddDisplayString(null, new StringConstantExpression("@LCF(0xH1234)"));
            AddLookupReference(builder, displayString, "LCF", CreateDictionaryExpression(dict));

            // 4 of 5 items are unique - don't collapse
            var serializationContext = new SerializationContext { MinimumVersion = Version._0_79 };
            Assert.That(builder.Serialize(serializationContext).Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:LCF\n" +
                "1=One\n" +
                "2=Two\n" +
                "3=Three\n" +
                "4=Two\n" +
                "5=Five\n" +
                "\n" +
                "Display:\n" +
                "@LCF(0xH1234)\n"
            ));

            // 5 of 9 items are unique - don't collapse
            dict[6] = "Two";
            dict[7] = "Seven";
            dict[8] = "Two";
            dict[9] = "Three";

            builder = new RichPresenceBuilder();
            displayString = builder.AddDisplayString(null, new StringConstantExpression("@LCF(0xH1234)"));
            AddLookupReference(builder, displayString, "LCF", CreateDictionaryExpression(dict));

            Assert.That(builder.Serialize(serializationContext).Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:LCF\n" +
                "1=One\n" +
                "2=Two\n" +
                "3=Three\n" +
                "4=Two\n" +
                "5=Five\n" +
                "6=Two\n" +
                "7=Seven\n" +
                "8=Two\n" +
                "9=Three\n" +
                "\n" +
                "Display:\n" +
                "@LCF(0xH1234)\n"
            ));

            // 4 of 9 items are unique - collapse
            dict[7] = "Two";

            builder = new RichPresenceBuilder();
            displayString = builder.AddDisplayString(null, new StringConstantExpression("@LCF(0xH1234)"));
            AddLookupReference(builder, displayString, "LCF", CreateDictionaryExpression(dict));

            Assert.That(builder.Serialize(serializationContext).Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:LCF\n" +
                "1=One\n" +
                "2,4,6-8=Two\n" +
                "3,9=Three\n" +
                "5=Five\n" +
                "\n" +
                "Display:\n" +
                "@LCF(0xH1234)\n"
            ));

            // with 10 items, only need two duplicates to collapse
            dict[7] = "Seven";
            dict[10] = "Two";
            dict[11] = "Eleven";

            builder = new RichPresenceBuilder();
            displayString = builder.AddDisplayString(null, new StringConstantExpression("@LCF(0xH1234)"));
            AddLookupReference(builder, displayString, "LCF", CreateDictionaryExpression(dict));

            Assert.That(builder.Serialize(serializationContext).Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:LCF\n" +
                "1=One\n" +
                "2,4,6,8,10=Two\n" +
                "3,9=Three\n" +
                "5=Five\n" +
                "7=Seven\n" +
                "11=Eleven\n" +
                "\n" +
                "Display:\n" +
                "@LCF(0xH1234)\n"
            ));
        }

        [Test]
        public void TestLookupFieldDisableCollapsing()
        {
            var dict = new Dictionary<int, string>
            {
                { 1, "Odd" },
                { 2, "Even" },
                { 3, "Odd" },
                { 4, "Even" },
                { 5, "Odd" },
                { 6, "Even" },                
            };

            var builder = new RichPresenceBuilder();
            var displayString = builder.AddDisplayString(null, new StringConstantExpression("@OddOrEven(0xH1234)"));
            AddLookupReference(builder, displayString, "OddOrEven", CreateDictionaryExpression(dict));

            var serializationContext = new SerializationContext { MinimumVersion = Version._0_79 };
            Assert.That(builder.Serialize(serializationContext).Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:OddOrEven\n" +
                "1,3,5=Odd\n" +
                "2,4,6=Even\n" +
                "\n" +
                "Display:\n" +
                "@OddOrEven(0xH1234)\n"
            ));

            serializationContext = new SerializationContext { MinimumVersion = Version.MinimumVersion };
            Assert.That(builder.Serialize(serializationContext).Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:OddOrEven\n" +
                "1=Odd\n" +
                "2=Even\n" +
                "3=Odd\n" +
                "4=Even\n" +
                "5=Odd\n" +
                "6=Even\n" +
                "\n" +
                "Display:\n" +
                "@OddOrEven(0xH1234)\n"
            ));
        }

        [Test]
        public void TestLookupFieldWithAllEntriesSame()
        {
            var dict = new Dictionary<int, string>
            {
                { 1, "Test" },
                { 2, "Test" },
                { 3, "Test" },
                { 4, "Test" },
                { 5, "Test" },
            };

            var builder = new RichPresenceBuilder();
            var displayString = builder.AddDisplayString(null, new StringConstantExpression("@T(0xH1234)"));
            AddLookupReference(builder, displayString, "T", CreateDictionaryExpression(dict));

            var serializationContext = new SerializationContext { MinimumVersion = Version._0_79 };
            Assert.That(builder.Serialize(serializationContext).Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:T\n" +
                "1-5=Test\n" +
                "\n" +
                "Display:\n" +
                "@T(0xH1234)\n"
            ));

            serializationContext = new SerializationContext { MinimumVersion = Version.MinimumVersion };
            Assert.That(builder.Serialize(serializationContext).Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:T\n" +
                "1=Test\n" +
                "2=Test\n" +
                "3=Test\n" +
                "4=Test\n" +
                "5=Test\n" +
                "\n" +
                "Display:\n" +
                "@T(0xH1234)\n"
            ));
        }

        [Test]
        public void TestLookupFieldFallbackCollapse()
        {
            var dict = new Dictionary<int, string>
            {
                { 1, "Odd" },
                { 2, "Even" },
                { 3, "Odd" },
                { 4, "Even" },
                { 5, "Odd" },
                { 6, "Even" },
            };

            var builder = new RichPresenceBuilder();
            var displayString = builder.AddDisplayString(null, new StringConstantExpression("@OddOrEven(0xH1234)"));
            AddLookupReference(builder, displayString, "OddOrEven", CreateDictionaryExpression(dict), "Even");

            var serializationContext = new SerializationContext { MinimumVersion = Version._0_79 };
            Assert.That(builder.Serialize(serializationContext).Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:OddOrEven\n" +
                "1,3,5=Odd\n" +
                "*=Even\n" +
                "\n" +
                "Display:\n" +
                "@OddOrEven(0xH1234)\n"
            ));

            serializationContext = new SerializationContext { MinimumVersion = Version.MinimumVersion };
            Assert.That(builder.Serialize(serializationContext).Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:OddOrEven\n" +
                "1=Odd\n" +
                "3=Odd\n" +
                "5=Odd\n" +
                "*=Even\n" +
                "\n" +
                "Display:\n" +
                "@OddOrEven(0xH1234)\n"
            ));
        }
    }
}
