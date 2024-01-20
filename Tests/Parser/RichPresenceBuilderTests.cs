using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
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

        [Test]
        public void TestConditionDisplayString()
        {
            var builder = new RichPresenceBuilder();
            builder.AddConditionalDisplayString("0xH1234=1", "One");
            builder.AddConditionalDisplayString("0xH1234=2", "Two");
            builder.DisplayString = "Something Else";

            Assert.That(builder.ToString().Replace("\r\n", "\n"), Is.EqualTo(
                "Display:\n" +
                "?0xH1234=1?One\n" +
                "?0xH1234=2?Two\n" +
                "Something Else\n"
            ));
        }

        [Test]
        public void TestValueFields()
        {
            // explicitly initialize out of order
            var builder = new RichPresenceBuilder();
            builder.DisableBuiltInMacros = true;
            builder.AddValueField(null, "Val", ValueFormat.Value);
            builder.AddValueField(null, "Score", ValueFormat.Score);
            builder.DisplayString = "@Val(0xH1234) @Score(0xH2345)";

            Assert.That(builder.ToString().Replace("\r\n", "\n"), Is.EqualTo(
                "Format:Val\n" +
                "FormatType=VALUE\n" +
                "\n" +
                "Format:Score\n" +
                "FormatType=SCORE\n" +
                "\n" +
                "Display:\n" +
                "@Val(0xH1234) @Score(0xH2345)\n"
            ));
        }

        [Test]
        public void TestValueFieldsBuiltIn()
        {
            var builder = new RichPresenceBuilder();
            builder.DisableBuiltInMacros = false;
            builder.AddValueField(null, "Val", ValueFormat.Value);
            builder.AddValueField(null, "Score", ValueFormat.Score);
            builder.DisplayString = "@Val(0xH1234) @Score(0xH2345)";

            Assert.That(builder.ToString().Replace("\r\n", "\n"), Is.EqualTo(
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
            Assert.That(builder.AddLookupField(null, "L", dict, new StringConstantExpression("")), Is.Null);
            builder.DisplayString = "@L(0xH1234)";

            Assert.That(builder.ToString().Replace("\r\n", "\n"), Is.EqualTo(
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
            Assert.That(builder.AddLookupField(null, "YesNo", 
                CreateDictionaryExpression(dict), new StringConstantExpression("?")), Is.Null);
            builder.DisplayString = "@YesNo(0xH1234)";

            Assert.That(builder.ToString().Replace("\r\n", "\n"), Is.EqualTo(
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
            Assert.That(builder.AddLookupField(null, "LCF", CreateDictionaryExpression(dict), 
                new StringConstantExpression("")), Is.Null);
            builder.DisplayString = "@LCF(0xH1234)";

            // 4 of 5 items are unique - don't collapse
            Assert.That(builder.ToString().Replace("\r\n", "\n"), Is.EqualTo(
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
            Assert.That(builder.AddLookupField(null, "LCF", CreateDictionaryExpression(dict),
                new StringConstantExpression("")), Is.Null);
            Assert.That(builder.ToString().Replace("\r\n", "\n"), Is.EqualTo(
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
            Assert.That(builder.AddLookupField(null, "LCF", CreateDictionaryExpression(dict),
                new StringConstantExpression("")), Is.Null);
            Assert.That(builder.ToString().Replace("\r\n", "\n"), Is.EqualTo(
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
            Assert.That(builder.AddLookupField(null, "LCF", CreateDictionaryExpression(dict),
                new StringConstantExpression("")), Is.Null);
            Assert.That(builder.ToString().Replace("\r\n", "\n"), Is.EqualTo(
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
            Assert.That(builder.AddLookupField(null, "OddOrEven", CreateDictionaryExpression(dict),
                new StringConstantExpression("")), Is.Null);
            builder.DisplayString = "@OddOrEven(0xH1234)";

            Assert.That(builder.DisableLookupCollapsing, Is.False);
            Assert.That(builder.ToString().Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:OddOrEven\n" +
                "1,3,5=Odd\n" +
                "2,4,6=Even\n" +
                "\n" +
                "Display:\n" +
                "@OddOrEven(0xH1234)\n"
            ));

            builder.DisableLookupCollapsing = true;
            Assert.That(builder.DisableLookupCollapsing, Is.True);
            Assert.That(builder.ToString().Replace("\r\n", "\n"), Is.EqualTo(
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
            Assert.That(builder.AddLookupField(null, "T", CreateDictionaryExpression(dict),
                new StringConstantExpression("")), Is.Null);
            builder.DisplayString = "@T(0xH1234)";

            // 4 of 5 items are unique - don't collapse
            Assert.That(builder.ToString().Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:T\n" +
                "1-5=Test\n" +
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
            Assert.That(builder.AddLookupField(null, "OddOrEven", CreateDictionaryExpression(dict),
                new StringConstantExpression("Even")), Is.Null);
            builder.DisplayString = "@OddOrEven(0xH1234)";

            Assert.That(builder.DisableLookupCollapsing, Is.False);
            Assert.That(builder.ToString().Replace("\r\n", "\n"), Is.EqualTo(
                "Lookup:OddOrEven\n" +
                "1,3,5=Odd\n" +
                "*=Even\n" +
                "\n" +
                "Display:\n" +
                "@OddOrEven(0xH1234)\n"
            ));

            builder.DisableLookupCollapsing = true;
            Assert.That(builder.DisableLookupCollapsing, Is.True);
            Assert.That(builder.ToString().Replace("\r\n", "\n"), Is.EqualTo(
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
