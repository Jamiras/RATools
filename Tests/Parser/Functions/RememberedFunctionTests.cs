using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using RATools.Parser.Functions;
using RATools.Parser.Internal;
using RATools.Parser.Tests.Expressions;
using RATools.Parser.Tests.Expressions.Trigger;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Parser.Tests.Functions
{
    [TestFixture]
    class RememberedFunctionTests
    {
        [Test]
        public void TestDefinition()
        {
            var def = new RememberedFunction();
            Assert.That(def.Name.Name, Is.EqualTo("remembered"));
            Assert.That(def.Parameters.Count, Is.EqualTo(1));
            Assert.That(def.Parameters.ElementAt(0).Name, Is.EqualTo("accessor"));
        }

        [Test]
        // Constants and comparisons can't be remembered
        [TestCase("1", "1")]
        [TestCase("always_false()", "0=1")]
        [TestCase("byte(0x1234) == 6", "0xH001234=6")]
        // Unmodified memory accessors doesn't need to be remembered
        [TestCase("byte(0x1234)", "0xH001234")]
        // Modified memory accessors and pointed-at values can be remembered.
        // NOTE: The output of the serializer is the {recall} field, but the remember chain
        //       will also be output if it hasn't already.
        [TestCase("byte(0x1234) * 2", "K:0xH001234*2_{recall}")]
        [TestCase("word(0x1234) + 2", "A:2_K:0x 001234_{recall}")]
        [TestCase("word(0x1234) - 2", "B:2_K:0x 001234_{recall}")]
        [TestCase("dword(dword(0x1234))", "I:0xX001234_K:0xX000000_{recall}")]
        [TestCase("remembered(dword(0x1234)+8)*2", "A:8_K:0xX001234_K:{recall}*2_{recall}")]
        public void TestRemember(string input, string expected)
        {
            var expr = TriggerExpressionTests.Parse("remembered(" + input + ")");

            var error = expr as ErrorExpression;
            if (error != null)
            {
                ExpressionTests.AssertError(error, expected);
            }
            else
            {
                var triggerExpr = expr as ITriggerExpression;
                if (triggerExpr != null)
                    TriggerExpressionTests.AssertSerialize(triggerExpr, expected);
                else
                    ExpressionTests.AssertAppendString(expr, expected);
            }
        }

        [Test]
        [TestCase("word(0x1234) + 2", "K:0x 001234+2_{recall}")]
        [TestCase("word(0x1234) - 2", "K:0x 001234-2_{recall}")]
        public void TestRememberSimplifiedAddition(string input, string expected)
        {
            var expr = TriggerExpressionTests.Parse("remembered(" + input + ")");

            var error = expr as ErrorExpression;
            if (error != null)
            {
                ExpressionTests.AssertError(error, expected);
            }
            else
            {
                var triggerExpr = expr as ITriggerExpression;
                if (triggerExpr != null)
                    TriggerExpressionTests.AssertSerialize(triggerExpr, expected, new TriggerBuilderContext { MinimumVersion = Data.Version._1_3_1 });
                else
                    ExpressionTests.AssertAppendString(expr, expected);
            }
        }

        [Test]
        public void TestScriptInline()
        {
            var parser = AchievementScriptTests.Parse(
                "achievement(\"t\", \"d\", 5, byte(remembered(dword(dword(0x1234) + 8)) + 4) > 5 && " +
                                             "word(remembered(dword(dword(0x1234) + 8)) + 8) == 0)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            var serialized = achievement.Trigger.Serialize(new SerializationContext());
            Assert.That(serialized, Is.EqualTo("I:0xX001234_K:0xX000008_I:{recall}_0xH000004>5_I:{recall}_0x 000008=0"));
        }

        [Test]
        public void TestScriptVariable()
        {
            var parser = AchievementScriptTests.Parse(
                "ptr = remembered(dword(dword(0x1234) + 8))\n" +
                "achievement(\"t\", \"d\", 5, byte(ptr + 4) > 5 && word(ptr + 8) == 0)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            var serialized = achievement.Trigger.Serialize(new SerializationContext());
            Assert.That(serialized, Is.EqualTo("I:0xX001234_K:0xX000008_I:{recall}_0xH000004>5_I:{recall}_0x 000008=0"));
        }
    }
}
