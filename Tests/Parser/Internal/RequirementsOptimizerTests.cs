using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;

namespace RATools.Parser.Tests.Internal
{
    [TestFixture]
    class RequirementsOptimizerTests
    {
        private static AchievementBuilder CreateAchievement(string input, string expectedError = null)
        {
            // NOTE: these are integration tests as they rely on ExpressionBase.Parse and 
            // AchievementScriptInterpreter.ScriptInterpreterAchievementBuilder, but using string 
            // inputs /output makes reading the tests and validating the behavior easier for humans.
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expression = ExpressionBase.Parse(tokenizer);
            if (expression is ErrorExpression)
                Assert.Fail(((ErrorExpression)expression).Message);

            var achievement = new ScriptInterpreterAchievementBuilder();
            var error = achievement.PopulateFromExpression(expression);
            if (expectedError != null)
                Assert.That(error, Is.EqualTo(expectedError));
            else
                Assert.That(error, Is.Null.Or.Empty);

            return achievement;
        }

        [Test]
        // ==== NormalizeLimits ===
        [TestCase("byte(0x001234) == 1 && byte(0x004567) >= 0", "byte(0x001234) == 1")] // greater than or equal to 0 is always true, ignore it
        [TestCase("byte(0x001234) >= 0 && byte(0x001234) <= 15", "byte(0x001234) <= 15")] // greater than or equal to 0 is always true, ignore it
        [TestCase("byte(0x001234) == 1 && prev(byte(0x004567)) >= 0", "byte(0x001234) == 1")] // greater than or equal to 0 is always true, ignore it
        [TestCase("byte(0x001234) == 1 || prev(byte(0x004567)) < 0", "byte(0x001234) == 1")] // less than 0 is always false, ignore it
        [TestCase("byte(0x001234) <= 0", "byte(0x001234) == 0")] // less than 0 can never be true, only keep the equals
        [TestCase("byte(0x001234) > 0", "byte(0x001234) != 0")] // less than 0 can never be true, so if it's greater than 0, it's just not zero
        [TestCase("bit0(0x001234) <= 0", "bit0(0x001234) == 0")] // less than 0 can never be true, only keep the equals
        [TestCase("bit0(0x001234) != 0", "bit0(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit1(0x001234) != 0", "bit1(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit2(0x001234) != 0", "bit2(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit3(0x001234) != 0", "bit3(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit4(0x001234) != 0", "bit4(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit5(0x001234) != 0", "bit5(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit6(0x001234) != 0", "bit6(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit7(0x001234) != 0", "bit7(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit0(0x001234) > 0", "bit0(0x001234) == 1")] // bit greater than zero must be 1
        [TestCase("bit0(0x001234) != 1", "bit0(0x001234) == 0")] // bit not equal to one must be 0
        [TestCase("bit0(0x001234) < 1", "bit0(0x001234) == 0")] // bit less than one must be 0
        [TestCase("byte(0x001234) < 1", "byte(0x001234) == 0")] // byte less than one must be 0
        [TestCase("8 > byte(0x001234)", "byte(0x001234) < 8")] // prefer value on the right
        [TestCase("byte(0x001234) == 1 && byte(0x004567) < 0", "always_false()")] // less than 0 can never be true, replace with always_false
        [TestCase("byte(0x001234) == 1 && low4(0x004567) > 15", "always_false()")] // nibble cannot be greater than 15, replace with always_false
        [TestCase("byte(0x001234) == 1 && high4(0x004567) > 15", "always_false()")] // nibble cannot be greater than 15, replace with always_false
        [TestCase("byte(0x001234) == 1 && byte(0x004567) > 255", "always_false()")] // byte cannot be greater than 255, replace with always_false
        [TestCase("byte(0x001234) == 1 && word(0x004567) > 65535", "always_false()")] // word cannot be greater than 255, replace with always_false
        [TestCase("byte(0x001234) == 1 && dword(0x004567) > 4294967295", "always_false()")] // dword cannot be greater than 4294967295, replace with always_false
        [TestCase("byte(0x001234) == 1 && low4(0x004567) >= 15", "byte(0x001234) == 1 && low4(0x004567) == 15")] // nibble cannot be greater than 15, change to equals
        [TestCase("byte(0x001234) == 1 && high4(0x004567) >= 15", "byte(0x001234) == 1 && high4(0x004567) == 15")] // nibble cannot be greater than 15, change to equals
        [TestCase("byte(0x001234) == 1 && byte(0x004567) >= 255", "byte(0x001234) == 1 && byte(0x004567) == 255")] // byte cannot be greater than 255, change to equals
        [TestCase("byte(0x001234) == 1 && word(0x004567) >= 65535", "byte(0x001234) == 1 && word(0x004567) == 65535")] // word cannot be greater than 255, change to equals
        [TestCase("byte(0x001234) == 1 && dword(0x004567) >= 4294967295", "byte(0x001234) == 1 && dword(0x004567) == 4294967295")] // dword cannot be greater than 4294967295, change to equals
        [TestCase("byte(0x001234) == 1 && bit0(0x004567) <= 1", "byte(0x001234) == 1")] // bit always less than or equal to 1, ignore it
        [TestCase("byte(0x001234) == 1 && low4(0x004567) <= 15", "byte(0x001234) == 1")] // nibble always less than or equal to 15, ignore it
        [TestCase("byte(0x001234) == 1 && high4(0x004567) <= 15", "byte(0x001234) == 1")] // nibble always less than or equal to 15, ignore it
        [TestCase("byte(0x001234) == 1 && byte(0x004567) <= 255", "byte(0x001234) == 1")] // byte always less than or equal to 255, ignore it
        [TestCase("byte(0x001234) == 1 && word(0x004567) <= 65535", "byte(0x001234) == 1")] // word always less than or equal to 255, ignore it
        [TestCase("byte(0x001234) == 1 && dword(0x004567) <= 4294967295", "byte(0x001234) == 1")] // dword always less than or equal to 4294967295, ignore it
        [TestCase("byte(0x001234) == 1 && low4(0x004567) < 15", "byte(0x001234) == 1 && low4(0x004567) != 15")] // nibble cannot be greater than 15, change to not equals
        [TestCase("byte(0x001234) == 1 && high4(0x004567) < 15", "byte(0x001234) == 1 && high4(0x004567) != 15")] // nibble cannot be greater than 15, change to not equals
        [TestCase("byte(0x001234) == 1 && byte(0x004567) < 255", "byte(0x001234) == 1 && byte(0x004567) != 255")] // byte cannot be greater than 255, change to not equals
        [TestCase("byte(0x001234) == 1 && word(0x004567) < 65535", "byte(0x001234) == 1 && word(0x004567) != 65535")] // word cannot be greater than 255, change to not equals
        [TestCase("byte(0x001234) == 1 && dword(0x004567) < 4294967295", "byte(0x001234) == 1 && dword(0x004567) != 4294967295")] // dword cannot be greater than 4294967295, change to not equals
        [TestCase("bit0(0x001234) + bit1(0x001234) == 2", "(bit0(0x001234) + bit1(0x001234)) == 2")] // addition can exceed max size of source
        [TestCase("byte(0x001234) == 1000", "always_false()")] // can never be true
        [TestCase("byte(0x001234) > 256", "always_false()")] // can never be true
        [TestCase("byte(0x001234) < 256", "always_true()")] // always true
        [TestCase("bitcount(0x1234) == 9", "always_false()")] // bitcount can never return more than 8
        [TestCase("bitcount(0x1234) + bitcount(0x1235) == 9", "(bitcount(0x001234) + bitcount(0x001235)) == 9")] // multiple bitcounts can be more than 8
        [TestCase("bitcount(0x1234) >= 8", "byte(0x001234) == 255")] // bitcount == 8 is all bits set
        [TestCase("measured(bitcount(0x1234) >= 8)", "measured(bitcount(0x001234) == 8)")] // don't convert bitcount to byte checks when wrapped in measured
        [TestCase("bitcount(0x1234) == 0", "byte(0x001234) == 0")] // bitcount == 0 is no bits set
        [TestCase("once(bit1(0x001234) == 255) && byte(0x002345) == 4", "always_false()")] // bit can never be 255, entire group is false
        // ==== NormalizeBCD ===
        [TestCase("bcd(byte(0x1234)) == 20", "byte(0x001234) == 32")]
        [TestCase("bcd(byte(0x1234)) == 100", "always_false()")] // BCD of a byte cannot exceed 99
        [TestCase("bcd(byte(0x1234)) < 100", "always_true()")] // BCD of a byte cannot exceed 99
        [TestCase("bcd(byte(0x1234)) >= 99", "byte(0x001234) >= 153")] // BCD of a byte can exceed 99, but it's not a valid BCD entry
        [TestCase("bcd(dword(0x1234)) == 12345678", "dword(0x001234) == 305419896")]
        [TestCase("bcd(dword(0x1234)) == 100000000", "always_false()")] // BCD of a dword cannot exceed 99999999
        [TestCase("bcd(byte(0x1234)) == bcd(byte(0x2345))", "byte(0x001234) == byte(0x002345)")] // BCD can be removed from both sides of the comparison
        [TestCase("bcd(byte(0x1234)) == byte(0x2345)", "bcd(byte(0x001234)) == byte(0x002345)")] // BCD cannot be removed when comparing to another memory address
        [TestCase("bcd(low4(0x1234)) == low4(0x2345)", "low4(0x001234) == low4(0x002345)")] // BCD can be removed for memory accessors of 4 bits or less
        [TestCase("low4(0x1234) == bcd(low4(0x2345))", "low4(0x001234) == low4(0x002345)")] // BCD can be removed for memory accessors of 4 bits or less
        [TestCase("bcd(low4(0x1234)) == 6", "low4(0x001234) == 6")] // BCD can be removed for memory accessors of 4 bits or less
        [TestCase("bcd(low4(0x1234)) == 10", "always_false()")] // BCD of a nummber cannot exceed 9
        // ==== NormalizeComparisons ===
        [TestCase("byte(0x001234) == prev(byte(0x001234))", "byte(0x001234) == prev(byte(0x001234))")] // non-deterministic
        [TestCase("byte(0x001234) == word(0x001234)", "byte(0x001234) == word(0x001234)")] // non-deterministic
        [TestCase("byte(0x001234) == byte(0x001234)", "always_true()")] // always true
        [TestCase("byte(0x001234) != byte(0x001234)", "always_false()")] // never true
        [TestCase("byte(0x001234) <= byte(0x001234)", "always_true()")] // always true
        [TestCase("byte(0x001234) < byte(0x001234)", "always_false()")] // never true
        [TestCase("byte(0x001234) >= byte(0x001234)", "always_true()")] // always true
        [TestCase("byte(0x001234) > byte(0x001234)", "always_false()")] // never true
        [TestCase("once(byte(0x001234) == byte(0x001234))", "always_true()")] // always true
        [TestCase("repeated(3, byte(0x001234) == byte(0x001234))", "repeated(3, always_true())")] // always true, but ignored for two frames
        [TestCase("never(repeated(3, always_true()))", "never(repeated(3, always_true()))")] // always true, but ignored for two frames
        [TestCase("byte(0x001234) == 1 && never(repeated(3, always_true()))", "byte(0x001234) == 1 && never(repeated(3, always_true()))")] // always true, but ignored for two frames
        [TestCase("repeated(3, byte(0x001234) != byte(0x001234))", "always_false()")] // always false will never be true, regardless of how many frames it's false
        [TestCase("always_false() && byte(0x001234) == 1", "always_false()")] // always false and anything is always false
        [TestCase("always_false() && (byte(0x001234) == 1 || byte(0x001234) == 2)", "always_false()")] // always false and anything is always false
        [TestCase("always_true() && byte(0x001234) == 1", "byte(0x001234) == 1")] // always true and anything is the anything clause
        [TestCase("once(byte(0x004567) == 2) && (byte(0x002345) == 3 || (always_false() && never(byte(0x001234) == 1) && byte(0x001235) == 2))",
                  "once(byte(0x004567) == 2) && (byte(0x002345) == 3 || (never(byte(0x001234) == 1) && always_false()))")] // always_false paired with ResetIf does not eradicate the ResetIf
        [TestCase("once(byte(0x001234) == 1) && never(always_false())", "once(byte(0x001234) == 1)")] // a ResetIf for a condition that can never be true is redundant
        [TestCase("once(byte(0x001234) == 1) && unless(always_false())", "once(byte(0x001234) == 1)")] // a PauseIf for a condition that can never be true is redundant
        [TestCase("once(byte(0x001234) == 1) && never(always_true())", "always_false()")] // a ResetIf for a condition that is always true will never let the trigger fire
        [TestCase("once(byte(0x001234) == 1) && unless(always_true())", "always_false()")] // a PauseIf for a condition that is always true will prevent the trigger from firing
        [TestCase("once(byte(0x001234) == 1) && never(once(bit2(0x1234) == 255))", "once(byte(0x001234) == 1)")] // condition becomes always_false(), and never(always_false()) can be eliminated
        [TestCase("byte(0x001234) == 1 && never(byte(0x2345) > 0x2345)", "byte(0x001234) == 1")] // condition becomes always_false(), and never(always_false()) can be eliminated
        [TestCase("byte(0x001234) == 1 && unless(once(byte(0x2345) == 0x2345))", "byte(0x001234) == 1")] // condition becomes always_false(), and unless(always_false()) can be eliminated
        [TestCase("float(0x001234) < 0.0", "float(0x001234) < 0.0")]
        [TestCase("float(0x001234) < 0", "float(0x001234) < 0")]
        [TestCase("(byte(0x001234) == 1 && byte(0x002345) != 1) || (byte(0x001234) != 1 && byte(0x002345) == 1)",
                  "(byte(0x001234) == 1 && byte(0x002345) != 1) || (byte(0x001234) != 1 && byte(0x002345) == 1)")]
        [TestCase("once(byte(0x001234) == 1) && never((byte(0x002345) & 0x0000001F) == 0 && byte(0x003456) == 6)", // masking creates an AddSource followed by a constant/constant comparison that should not be eliminated
                  "once(byte(0x001234) == 1) && never(((byte(0x002345) & 0x0000001F) == 0) && byte(0x003456) == 6)")]
        public void TestOptimizeNormalizeComparisons(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2))",
                  "never(byte(0x002345) == 2) && repeated(2, byte(0x001234) == 1)")] // ResetNextIf can be turned into a ResetIf
        [TestCase("never(byte(0x2222) == 2) && unless(repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2)))",
                  "never(byte(0x002222) == 2) && disable_when(repeated(2, byte(0x001234) == 1), until=byte(0x002345) == 2)")] // ResetNextIf attached to a PauseIf cannot be converted
        [TestCase("byte(0x2222) == 2 || unless(repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2)))",
                  "byte(0x002222) == 2 || (disable_when(repeated(2, byte(0x001234) == 1), until=byte(0x002345) == 2))")] // ResetNextIf attached to a PauseIf cannot be converted
        [TestCase("byte(0x2222) == 2 || never(byte(0x3333) == 1) && unless(repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2)))",
                  "byte(0x002222) == 2 || (never(byte(0x003333) == 1) && disable_when(repeated(2, byte(0x001234) == 1), until=byte(0x002345) == 2))")] // ResetNextIf attached to a PauseIf cannot be converted
        [TestCase("once(byte(0x2222) == 2) && repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2))",
                  "once(byte(0x002222) == 2) && repeated(2, byte(0x001234) == 1 && never(byte(0x002345) == 2))")] // ResetNextIf cannot be turned into a ResetIf
        [TestCase("repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2)) && repeated(3, byte(0x1234) == 3 && never(byte(0x2345) == 2))",
                  "never(byte(0x002345) == 2) && repeated(2, byte(0x001234) == 1) && repeated(3, byte(0x001234) == 3)")] // similar ResetNextIfs can be turned into a ResetIf
        [TestCase("repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2)) && repeated(3, byte(0x1234) == 3 && never(byte(0x2345) == 3))",
                  "repeated(2, byte(0x001234) == 1 && never(byte(0x002345) == 2)) && repeated(3, byte(0x001234) == 3 && never(byte(0x002345) == 3))")] // dissimilar ResetNextIfs cannot be turned into a ResetIf
        [TestCase("disable_when(byte(0x1234) == 1, until=byte(0x2345) == 2)",
                  "disable_when(byte(0x001234) == 1, until=byte(0x002345) == 2)")] // ResetNextIf attached to PauseIf should not be converted
        [TestCase("disable_when(tally(2, byte(0x1234) == 1, byte(0x1234) == 2), until=byte(0x2345) == 2)",
                  "disable_when(tally(2, byte(0x001234) == 1 && never(byte(0x002345) == 2), byte(0x001234) == 2), until=byte(0x002345) == 2)")] // ResetNextIf attached to a tallied PauseIf gets distributed across the tallied items. TODO: collapse them back together when converting back to a string
        [TestCase("never(byte(0x001234) != 5)", "byte(0x001234) == 5")]
        [TestCase("never(byte(0x001234) == 5)", "byte(0x001234) != 5")]
        [TestCase("never(byte(0x001234) >= 5)", "byte(0x001234) < 5")]
        [TestCase("never(byte(0x001234) > 5)", "byte(0x001234) <= 5")]
        [TestCase("never(byte(0x001234) <= 5)", "byte(0x001234) > 5")]
        [TestCase("never(byte(0x001234) < 5)", "byte(0x001234) >= 5")]
        [TestCase("never(byte(0x001234) != 1 && byte(0x002345) == 2)", "byte(0x001234) == 1 || byte(0x002345) != 2")] // AndNext becomes OrNext, both operators inverted
        [TestCase("unless(byte(0x001234) != 5)", "byte(0x001234) == 5")]
        [TestCase("unless(byte(0x001234) == 5)", "byte(0x001234) != 5")]
        [TestCase("unless(byte(0x001234) >= 5)", "byte(0x001234) < 5")]
        [TestCase("unless(byte(0x001234) > 5)", "byte(0x001234) <= 5")]
        [TestCase("unless(byte(0x001234) <= 5)", "byte(0x001234) > 5")]
        [TestCase("unless(byte(0x001234) < 5)", "byte(0x001234) >= 5")]
        [TestCase("unless(byte(0x001234) != 1 && byte(0x002345) == 2)", "byte(0x001234) == 1 || byte(0x002345) != 2")] // AndNext becomes OrNext, both operators inverted
        [TestCase("unless(byte(0x001234) == 5) && byte(0x002345) == 1", "byte(0x001234) != 5 && byte(0x002345) == 1")] // unless without HitCount should be inverted to a requirement
        [TestCase("unless(byte(0x001234) != 1) && unless(once(byte(0x002345) == 1))", // PauseLock is affected by Pause, so other Pause won't be inverted
                  "unless(byte(0x001234) != 1) && disable_when(byte(0x002345) == 1)")]
        [TestCase("byte(0x001234) == 5 && never(byte(0x001234) != 5)", "byte(0x001234) == 5")] // common pattern in older achievements to fix HitCount at 0, the ResetIf is functionally redundant
        [TestCase("(byte(0x002345) == 5 && never(byte(0x001234) == 6)) || (byte(0x002345) == 6 && never(byte(0x001235) == 3))", 
                  "(byte(0x002345) == 5 && byte(0x001234) != 6) || (byte(0x002345) == 6 && byte(0x001235) != 3)")] // same logic applies to alt groups
        [TestCase("once(byte(0x002345) == 5) && never(byte(0x001234) != 5)", "once(byte(0x002345) == 5) && never(byte(0x001234) != 5)")] // if there's a HitCount, leave the ResetIf alone
        [TestCase("never(byte(0x001234) != 5) && (byte(0x002345) == 6 || once(byte(0x002345) == 7))", "never(byte(0x001234) != 5) && (byte(0x002345) == 6 || once(byte(0x002345) == 7))")] // if there's a HitCount anywhere, leave the ResetIf alone
        [TestCase("(measured(byte(0x1234) < 100) && unless(byte(0x1235) == 1)) || (measured(byte(0x1236) < 100) && unless(byte(0x1235) == 2))",
                  "(measured(byte(0x001234) < 100) && unless(byte(0x001235) == 1)) || (measured(byte(0x001236) < 100) && unless(byte(0x001235) == 2))")] // measured should prevent unless from being inverted
        [TestCase("trigger_when(repeated(3, byte(0x1234) == 1) && never(byte(0x2345) == 2)", // don't convert ResetNextIf to ResetIf when attached to a challenge indicator
                  "trigger_when(repeated(3, byte(0x001234) == 1 && never(byte(0x002345) == 2)))")]
        [TestCase("trigger_when(repeated(3, byte(0x1234) == 1) && never(byte(0x2345) == 2)) && byte(0x3456) == 3", // don't convert ResetNextIf to ResetIf when attached to a challenge indicator
                  "trigger_when(repeated(3, byte(0x001234) == 1 && never(byte(0x002345) == 2))) && byte(0x003456) == 3")]
        [TestCase("byte(0x001234) == 1 || (unless(byte(0x002345) == 1) && never(always_true()))", // ResetIf guarded by PauseIf should be kept
                  "byte(0x001234) == 1 || (unless(byte(0x002345) == 1) && never(always_true()))")]
        [TestCase("once(byte(0x001234) == 1) && disable_when(byte(0x002345) == 2, until=byte(0x003456) == 1) && never(byte(0x003456) == 1)", // inner ResetNextIf must be kept to reset PauseIf even if handled by outer ResetIf
                  "once(byte(0x001234) == 1) && disable_when(byte(0x002345) == 2, until=byte(0x003456) == 1) && never(byte(0x003456) == 1)")]
        public void TestOptimizeNormalizeResetIfsAndPauseIfs(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("byte(0x001234) == 1 && ((byte(0x004567) == 1 && byte(0x004568) == 0) || (byte(0x004568) == 0 && byte(0x004569) == 1))", 
                  "byte(0x001234) == 1 && byte(0x004568) == 0 && (byte(0x004567) == 1 || byte(0x004569) == 1)")] // memory check in both alts is promoted to core
        [TestCase("byte(0x001234) == 1 && ((once(byte(0x004567) == 1) && never(byte(0x004568) == 0)) || (never(byte(0x004568) == 0) && once(byte(0x004569) == 1)))",
                  "byte(0x001234) == 1 && never(byte(0x004568) == 0) && (once(byte(0x004567) == 1) || once(byte(0x004569) == 1))")] // ResetIf in both alts is promoted to core
        [TestCase("byte(0x001234) == 1 && ((once(byte(0x004567) == 1) && unless(byte(0x004568) == 0)) || (unless(byte(0x004568) == 0) && once(byte(0x004569) == 1)))",
                  "byte(0x001234) == 1 && ((once(byte(0x004567) == 1) && unless(byte(0x004568) == 0)) || (unless(byte(0x004568) == 0) && once(byte(0x004569) == 1)))")] // PauseIf is not promoted if any part of group differs from other alts
        [TestCase("byte(0x001234) == 1 && ((once(byte(0x004567) == 1) && unless(byte(0x004568) == 0)) || (unless(byte(0x004568) == 0) && once(byte(0x004567) == 1)))",
                  "byte(0x001234) == 1 && once(byte(0x004567) == 1) && unless(byte(0x004568) == 0)")] // PauseIf is only promoted if entire group is duplicated in all alts
        [TestCase("byte(0x001234) == 1 && ((byte(0x004567) == 1 && byte(0x004568) == 0) || (byte(0x004568) == 0))",
                  "byte(0x001234) == 1 && byte(0x004568) == 0")] // entire second alt is subset of first alt, eliminate first alt. remaining alt promoted to core
        [TestCase("once(byte(0x001234) == 1) && ((never(byte(0x002345) + byte(0x002346) == 2)) || (never(byte(0x002345) + byte(0x002347) == 2)))",
                  "once(byte(0x001234) == 1) && ((never((byte(0x002345) + byte(0x002346)) == 2)) || (never((byte(0x002345) + byte(0x002347)) == 2)))")] // partial AddSource cannot be promoted
        [TestCase("once(byte(0x001234) == 1) && ((never(byte(0x002345) == 1) && unless(byte(0x003456) == 3)) || (never(byte(0x002345) == 1) && unless(byte(0x003456) == 1)))",
                  "once(byte(0x001234) == 1) && ((never(byte(0x002345) == 1) && unless(byte(0x003456) == 3)) || (never(byte(0x002345) == 1) && unless(byte(0x003456) == 1)))")] // resetif cannot be promoted if pauseif is present
        [TestCase("once(byte(0x001234) == 1) && ((once(byte(0x002345) == 1) && unless(byte(0x003456) == 3)) || (once(byte(0x002345) == 1) && unless(byte(0x003456) == 1)))",
                  "once(byte(0x001234) == 1) && ((once(byte(0x002345) == 1) && unless(byte(0x003456) == 3)) || (once(byte(0x002345) == 1) && unless(byte(0x003456) == 1)))")] // item with hitcount cannot be promoted if pauseif is present
        [TestCase("1 == 1 && ((byte(0x001234) == 1 && byte(0x002345) == 1) || (byte(0x001234) == 1 && byte(0x002345) == 2))",
                  "byte(0x001234) == 1 && (byte(0x002345) == 1 || byte(0x002345) == 2)")] // core "1=1" should be removed by promotion of another condition
        [TestCase("measured(repeated(6, byte(0x1234) == 10), when=byte(0x2345) == 7) || measured(repeated(6, byte(0x2345) == 4), when=byte(0x2345) == 7)",
                  "(measured(repeated(6, byte(0x001234) == 10), when=byte(0x002345) == 7)) || (measured(repeated(6, byte(0x002345) == 4), when=byte(0x002345) == 7))")] // measured_if must stay with measured
        [TestCase("measured(repeated(6, byte(0x1234) == 10), when=byte(0x2345) == 7) || measured(repeated(6, byte(0x1234) == 10), when=byte(0x2345) == 7)",
                  "measured(repeated(6, byte(0x001234) == 10), when=byte(0x002345) == 7)")] // measured_if must stay with measured
        [TestCase("once(byte(0x001234) == 1) && (always_false() || never(byte(0x002345) == 1))", // always_false group is discarded, never is promoted
                  "once(byte(0x001234) == 1) && never(byte(0x002345) == 1)")]
        [TestCase("once(byte(0x001234) == 1) && (never(byte(0x002345) == 1) || never(byte(0x002345) == 1))", // duplicate alt is merge, never can be promoted to core
                  "once(byte(0x001234) == 1) && never(byte(0x002345) == 1)")]
        [TestCase("byte(0x001234) == 1 && (always_false() || once(byte(0x002345) == 2) && unless(byte(0x002345) == 1))", // always_false group is discarded, unless can be promoted because core won't be affected
                  "byte(0x001234) == 1 && once(byte(0x002345) == 2) && unless(byte(0x002345) == 1)")]
        [TestCase("once(byte(0x001234) == 1) && (always_false() || once(byte(0x002345) == 2) && unless(byte(0x002345) == 1))", // always_false group is discarded, unless is not promoted because of hit target
                  "once(byte(0x001234) == 1) && ((once(byte(0x002345) == 2) && unless(byte(0x002345) == 1)))")]
        [TestCase("once(byte(0x001234) == 1) && unless(once(byte(0x001234) == 1)) && (always_false() || never(byte(0x002345) == 1))", // never should not be promoted to core containing unless
                  "once(byte(0x001234) == 1) && disable_when(byte(0x001234) == 1) && (never(byte(0x002345) == 1))")]
        public void TestOptimizePromoteCommonAltsToCore(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("byte(0x001234) == 1 && byte(0x001234) == 1", "byte(0x001234) == 1")]
        [TestCase("byte(0x001234) < 8 && 8 >= byte(0x001234)", "byte(0x001234) < 8")] // prefer value on the right
        [TestCase("prev(byte(0x001234)) == 1 && prev(byte(0x001234)) == 1", "prev(byte(0x001234)) == 1")]
        [TestCase("once(byte(0x001234) == 1) && once(byte(0x001234) == 1)", "once(byte(0x001234) == 1)")]
        [TestCase("never(byte(0x001234) != prev(byte(0x001234))) && never(byte(0x001234) != prev(byte(0x001234)))", "byte(0x001234) == prev(byte(0x001234))")]
        [TestCase("never(byte(0x001234) != 1) && byte(0x001234) != 3 && once(byte(0x002345) == 1)",
                  "never(byte(0x001234) != 1) && once(byte(0x002345) == 1)")] // never(a!=1) is effectively a==1, so a!=3 can be eliminated
        [TestCase("never(byte(0x001234) == 1) && byte(0x001234) == 3 && once(byte(0x002345) == 1)",
                  "never(byte(0x001234) == 1) && byte(0x001234) == 3 && once(byte(0x002345) == 1)")] // never(a==1) is effectively a!=1, a==3 is more specific and cannot be eliminated
        [TestCase("never(byte(0x001234) == 1) && byte(0x001234) != 3 && once(byte(0x002345) == 1)",
                  "never(byte(0x001234) == 1) && byte(0x001234) != 3 && once(byte(0x002345) == 1)")] // never(a==1) is effectively a!=1, so a!=3 cannot be eliminated
        [TestCase("byte(0x001234) == 1 && byte(0x002345) + byte(0x001234) == 1", "byte(0x001234) == 1 && (byte(0x002345) + byte(0x001234)) == 1")] // duplicate in AddSource clause should be ignored
        [TestCase("byte(0x002345) + byte(0x001234) == 1 && byte(0x002345) + byte(0x001234) == 1", "(byte(0x002345) + byte(0x001234)) == 1")] // complete AddSource duplicate should be elimiated
        [TestCase("byte(0x001234) == 1 && once(byte(0x002345) == 1 && byte(0x001234) == 1)", "byte(0x001234) == 1 && once(byte(0x002345) == 1 && byte(0x001234) == 1)")] // duplicate in AndNext clause should be ignored
        [TestCase("once(byte(0x002345) == 1 && byte(0x001234) == 1) && once(byte(0x002345) == 1 && byte(0x001234) == 1)", "once(byte(0x002345) == 1 && byte(0x001234) == 1)")] // complete AndNext duplicate should be elimiated
        [TestCase("byte(0x001234) == 2 && ((byte(0x001234) == 2 && byte(0x004567) == 3) || (byte(0x001234) == 2 && byte(0x004567) == 4))",
                  "byte(0x001234) == 2 && (byte(0x004567) == 3 || byte(0x004567) == 4)")] // alts in core are redundant
        [TestCase("unless(byte(0x001234) == 1) && never(byte(0x002345) == 1) && ((unless(byte(0x001234) == 1) && once(byte(0x002345) == 2)) || (unless(byte(0x001234) == 1) && never(byte(0x002345) == 3)))", // PauseIf guarding once or never should not be promoted even if duplicated
                  "unless(byte(0x001234) == 1) && never(byte(0x002345) == 1) && ((unless(byte(0x001234) == 1) && once(byte(0x002345) == 2)) || (unless(byte(0x001234) == 1) && never(byte(0x002345) == 3)))")]
        public void TestOptimizeRemoveDuplicates(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }
        
        [TestCase("byte(0x001234) > 1 && byte(0x001234) > 2", "byte(0x001234) > 2")] // >1 && >2 is only >2
        [TestCase("byte(0x001234) > 3 && byte(0x001234) < 2", "always_false()")] // cannot both be true
        [TestCase("byte(0x001234) - prev(byte(0x001234)) == 1 && byte(0x001234) == 2", "(byte(0x001234) - prev(byte(0x001234))) == 1 && byte(0x001234) == 2")] // conflict with part of a SubSource clause should not be treated as wholly conflicting
        [TestCase("byte(0x001234) <= 2 && byte(0x001234) >= 2", "byte(0x001234) == 2")] // only overlap is the one value
        [TestCase("byte(0x001234) >= 2 && byte(0x001234) <= 2", "byte(0x001234) == 2")] // only overlap is the one value
        [TestCase("once(byte(0x001234) == 2) && once(byte(0x001234) == 3)", "once(byte(0x001234) == 2) && once(byte(0x001234) == 3)")] // cannot both be true at the same time, but can each be true once
        [TestCase("never(byte(0x001234) == 2) && never(byte(0x001234) == 3)", "byte(0x001234) != 2 && byte(0x001234) != 3")] // cannot both be true at the same time, but can each be true once
        [TestCase("never(byte(0x001234) == 2) && byte(0x001234) != 2", "byte(0x001234) != 2")] // no hitcount, only keep non-resetif
        [TestCase("byte(0x001234) == 2 && never(byte(0x001234) != 2)", "byte(0x001234) == 2")] // no hitcount, only keep non-resetif
        [TestCase("never(byte(0x001234) == 2) && byte(0x001234) != 2 && once(byte(0x001235) == 3)", "never(byte(0x001234) == 2) && once(byte(0x001235) == 3)")] // hitcount, only keep resetif
        [TestCase("byte(0x001234) == 2 && never(byte(0x001234) != 2) && once(byte(0x001235) == 3)", "never(byte(0x001234) != 2) && once(byte(0x001235) == 3)")] // hitcount, only keep resetif
        [TestCase("never(byte(0x001234) < 2) && repeated(10, byte(0x001234) >= 2)", "never(byte(0x001234) < 2) && repeated(10, byte(0x001234) >= 2)")] // HitCount on same field as ResetIf should not be optimized away
        [TestCase("once(byte(0x1234) == 6) && repeated(5, byte(0x1234) == 6)", "repeated(5, byte(0x001234) == 6)")] // same condition with different hitcounts only honors higher hitcount
        [TestCase("repeated(5, byte(0x1234) == 6) && once(byte(0x1234) == 6)", "repeated(5, byte(0x001234) == 6)")] // same condition with different hitcounts only honors higher hitcount
        [TestCase("byte(0x1234) == 6 && repeated(5, byte(0x1234) == 6)", "byte(0x001234) == 6 && repeated(5, byte(0x001234) == 6)")] // without hitcount, cannot be merged
        [TestCase("once(byte(0x1234) == byte(0x1234)) && repeated(5, byte(0x2345) == byte(0x2345))", "repeated(5, always_true())")] // different conditions evaluate to always true, only capture higher hitcount
        [TestCase("once(byte(0x1234) == 1) && never(byte(0x2345) != 12) && never(byte(0x2345) == 0)", "once(byte(0x001234) == 1) && never(byte(0x002345) != 12)")] // never should keep the less restrictive condition
        [TestCase("measured(byte(0x1234) == 50, when=byte(0x2345) == 1) && byte(0x2345) == 1", "measured(byte(0x001234) == 50, when=byte(0x002345) == 1)")] // non-when condition redundant with when condition
        [TestCase("measured(byte(0x1234) == 50, when=byte(0x2345) == 1) && byte(0x2345) == 1 && byte(0x2345) == 1", "measured(byte(0x001234) == 50, when=byte(0x002345) == 1)")] // non-when condition redundant with when condition
        [TestCase("measured(byte(0x1234) == 50, when=byte(0x2345) == 1) && byte(0x2345) == 1 && byte(0x2345) == 1", "measured(byte(0x001234) == 50, when=byte(0x002345) == 1)")] // non-when condition redundant with when condition
        [TestCase("measured(byte(0x1234) == 50, when=byte(0x2345) < 5) && byte(0x2345) == 2", "measured(byte(0x001234) == 50, when=byte(0x002345) < 5) && byte(0x002345) == 2")] // non-when condition not redundant and must be kept
        [TestCase("measured(byte(0x1234) == 50, when=byte(0x2345) == 2) && byte(0x2345) < 5", "measured(byte(0x001234) == 50, when=byte(0x002345) == 2)")] // non-when condition redundant with when condition
        public void TestOptimizeRemoveRedundancies(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("byte(0x001234) > 1 || byte(0x001234) > 2", "byte(0x001234) > 1")] // >1 || >2 is only >1
        [TestCase("byte(0x001234) > 1 || byte(0x001235) > 2", "byte(0x001234) > 1 || byte(0x001235) > 2")] // different addresses
        [TestCase("byte(0x001234) > 1 || byte(0x001234) >= 2", "byte(0x001234) > 1")] // >1 || >=2 is only >1
        [TestCase("byte(0x001234) >= 1 || byte(0x001234) > 2", "byte(0x001234) >= 1")] // >=1 && >2 is only >=1
        [TestCase("byte(0x001234) < 3 || byte(0x001234) < 2", "byte(0x001234) < 3")] // <3 || <2 is only <3
        [TestCase("byte(0x001234) < 3 || byte(0x001235) < 2", "byte(0x001234) < 3 || byte(0x001235) < 2")] // different addresses
        [TestCase("byte(0x001234) < 3 || byte(0x001234) <= 2", "byte(0x001234) < 3")] // <3 || <=2 is only <3
        [TestCase("byte(0x001234) <= 3 || byte(0x001234) < 2", "byte(0x001234) <= 3")] // <=3 || <2 is only <=3
        [TestCase("byte(0x001234) != 3 || byte(0x001234) == 2", "byte(0x001234) != 3")] // =2 is implicitly !=3
        [TestCase("byte(0x001234) == 3 || byte(0x001234) != 2", "byte(0x001234) != 2")] // =3 is implicitly !=2
        [TestCase("byte(0x001234) == 3 || byte(0x001234) == 3", "byte(0x001234) == 3")] // redundant
        [TestCase("byte(0x001234) == 3 || byte(0x001234) == 2", "byte(0x001234) == 3 || byte(0x001234) == 2")] // either can be true separately
        [TestCase("byte(0x001234) > 3 || byte(0x001234) < 2", "byte(0x001234) > 3 || byte(0x001234) < 2")] // either can be true separately
        [TestCase("byte(0x001234) > 2 || byte(0x001234) < 2", "byte(0x001234) != 2")] // <2 or >2 is just != 2
        [TestCase("byte(0x001234) < 2 || byte(0x001234) > 2", "byte(0x001234) != 2")] // either can be true separately
        [TestCase("byte(0x001234) < 2 || byte(0x001234) > 3", "byte(0x001234) < 2 || byte(0x001234) > 3")] // either can be true separately
        [TestCase("byte(0x001234) >= 3 || byte(0x001234) <= 2", "byte(0x001234) >= 3 || byte(0x001234) <= 2")] // always true, can't collapse without overlap
        [TestCase("byte(0x001234) <= 2 || byte(0x001234) >= 3", "byte(0x001234) <= 2 || byte(0x001234) >= 3")] // always true, can't collapse without overlap
        [TestCase("byte(0x001234) <= 2 || byte(0x001234) >= 2", "always_true()")]
        [TestCase("byte(0x001234) >= 2 || byte(0x001234) <= 2", "always_true()")]
        [TestCase("always_false() || byte(0x001234) == 2 || byte(0x001234) == 3", "byte(0x001234) == 2 || byte(0x001234) == 3")] // always_false group can be removed
        [TestCase("always_false() || byte(0x001234) == 2", "byte(0x001234) == 2")] // always_false group can be removed
        [TestCase("always_true() || byte(0x001234) == 2 || byte(0x001234) == 3", "always_true()")] // always_true group causes other groups to be ignored if they don't have a resetif
        [TestCase("always_true() || byte(0x001234) == 2 || (byte(0x001234) == 3 && unless(byte(0x002345) == 1)) || (once(byte(0x001234) == 4) && never(byte(0x002345) == 1))",
            "always_true() || (once(byte(0x001234) == 4) && never(byte(0x002345) == 1))")] // always_true alt causes groups without resetif to be removed
        [TestCase("tally(2, once(byte(0x1111) == 1 && byte(0x2222) == 0), once(byte(0x1111) == 2 && byte(0x2222) == 0))",
            "tally(2, once(byte(0x001111) == 1 && byte(0x002222) == 0), once(byte(0x001111) == 2 && byte(0x002222) == 0))")]
        public void TestOptimizeMergeDuplicateAlts(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("bit0(0x001234) == 1 && bit1(0x001234) == 1 && bit2(0x001234) == 0 && bit3(0x001234) == 1", "low4(0x001234) == 11")]
        [TestCase("bit4(0x001234) == 1 && bit5(0x001234) == 1 && bit6(0x001234) == 0 && bit7(0x001234) == 1", "high4(0x001234) == 11")]
        [TestCase("low4(0x001234) == 12 && high4(0x001234) == 8", "byte(0x001234) == 140")]
        public void TestOptimizeMergeBits(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("word(word(0x001234) + 138) + 1 >= word(0x2345)",             // AddAddress compared to a non-AddAddress will generate an extra condition
                  "((word(word(0x001234) + 0x00008A)) + 1) >= word(0x002345)")] // to prevent the AddAddress from affecting the non-AddAddress. merge the +1 into that
        [TestCase("never(once(prev(byte(1)) - 1 == byte(1)) && repeated(10, always_true())",
                  "never(repeated(10, (once((prev(byte(0x000001)) - 1) == byte(0x000001))) && always_true()))")] // don't merge the -1 in the prev clause with the 1 in the always_true clause
        public void TestOptimizeMergeAddSourceConstants(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("repeated(2, byte(0x1234) == 120 || byte(0x1234) == 126)",
                  "repeated(2, byte(0x001234) == 120 || byte(0x001234) == 126)")]
        [TestCase("repeated(2, byte(0x1234) == 120 || byte(0x1234) == 126)",
                  "repeated(2, byte(0x001234) == 120 || byte(0x001234) == 126)")]
        [TestCase("measured(repeated(2, byte(0x1234) == 120 || byte(0x1234) == 126))",
                  "measured(repeated(2, byte(0x001234) == 120 || byte(0x001234) == 126))")]
        [TestCase("once(byte(0x2345) == 1) && never(!(byte(0x1234) <= 8 && byte(0x1234) >= 6))",
                  "once(byte(0x002345) == 1) && never(byte(0x001234) > 8) && never(byte(0x001234) < 6)")]
        [TestCase("never(!(byte(0x1234) <= 8 && byte(0x1234) >= 6) && byte(0x2345) >= 10)", 
                  "never((byte(0x001234) > 8 || byte(0x001234) < 6) && byte(0x002345) >= 10)")]
        [TestCase("repeated(10, byte(0x2345) == 1 && byte(0x3456) == 2 && never(byte(0x1234) < 5 || byte(0x1234) > 8))", // never will be converted to a ResetNextIf (before repeated), which will be further converted to two ResetIfs
                  "never(byte(0x001234) < 5) && never(byte(0x001234) > 8) && repeated(10, byte(0x002345) == 1 && byte(0x003456) == 2)")]
        [TestCase("repeated(10, byte(0x2345) == 1 && byte(0x3456) == 2 && never(byte(0x1234) < 5 || byte(0x1234) > 8)) && once(byte(0x5678) == 2)",
                  "repeated(10, byte(0x002345) == 1 && byte(0x003456) == 2 && never(byte(0x001234) < 5 || byte(0x001234) > 8)) && once(byte(0x005678) == 2)")]
        public void TestOptimizeDenormalizeOrNexts(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("byte(0x001234) == 1 && ((low4(0x004567) == 1 && high4(0x004567) >= 12) || (low4(0x004567) == 9 && high4(0x004567) >= 12) || (low4(0x004567) == 1 && high4(0x004567) >= 13))",
                  "byte(0x001234) == 1 && high4(0x004567) >= 12 && (low4(0x004567) == 1 || low4(0x004567) == 9)")] // alts 1 + 3 can be merged together, then the high4 extracted
        [TestCase("always_false() && never(byte(0x001234) == 1)", "always_false()")] // ResetIf without available HitCount inverted, then can be eliminated by always false
        [TestCase("once(always_false() || word(0x1234) >= 284 && word(0x1234) <= 301)",
                  "once(word(0x001234) >= 284 && word(0x001234) <= 301)")] // OrNext will move always_false to end, which will have the HitCount, HitCount should be kept when always_false is eliminated
        [TestCase("tally(2, always_false() || word(0x1234) >= 284 && word(0x1234) <= 301)",
                  "repeated(2, word(0x001234) >= 284 && word(0x001234) <= 301)")] // always_false() inside once() is optimized out
        [TestCase("measured(byte(0x1234) == 120, when = (byte(0x2345) == 6 || byte(0x2346) == 7))", // OrNext in MeasuredIf should not be split into alts
                  "measured(byte(0x001234) == 120, when=(byte(0x002345) == 6 || byte(0x002346) == 7))")]
        public void TestOptimizeComplex(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [Test]
        // ==== CrossMultiplyOrConditions ====
        [TestCase("(A || B) && (C || D)", "(A && C) || (A && D) || (B && C) || (B && D)")]
        [TestCase("(A || B) && (A || D)",
                //"(A && A) || (A && D) || (B && A) || (B && D)"
                  "A || (B && D)")] // "A || (A && D)" and "A || (B && A)" are both just "A"
        [TestCase("(A || B) && (A || C) && (B || C)",
                //"(A && B) || (A && C) || (A && C && B) || (A && C) || (B && A) || (B && A && C) || (B && C) || (B && C) 
                  "(A && B) || (A && C) || (B && C)")] // three part clauses reduce to two parts, and those are repeated
        [TestCase("((A && B) || (C && D)) && ((A && C) || (B && D))",
                  "(A && B && C) || (A && B && D) || (C && D && A) || (C && D && B)")]
        [TestCase("(A || B || C) && (D || E || F)",
                  "(A && D) || (A && E) || (A && F) || (B && D) || (B && E) || (B && F) || (C && D) || (C && E) || (C && F)")]
        [TestCase("(A && (B || C)) && (D || E)",
                  "A && ((B && D) || (B && E) || (C && D) || (C && E))")]
        [TestCase("A && (B || (C && D)) && (E || F)",
                  "A && ((B && E) || (B && F) || (C && D && E) || (C && D && F))")]
        [TestCase("(always_false() || always_false()) && (A || D)",
                  "A || D")] // always_false() cross multiples can be eliminated
        public void TestOrExpansion(string input, string expected)
        {
            input = input.Replace("A", "byte(0x00000A) == 1");
            input = input.Replace("B", "byte(0x00000B) == 1");
            input = input.Replace("C", "byte(0x00000C) == 1");
            input = input.Replace("D", "byte(0x00000D) == 1");
            input = input.Replace("E", "byte(0x00000E) == 1");
            input = input.Replace("F", "byte(0x00000F) == 1");

            var achievement = CreateAchievement(input);

            // NOTE: not optimized - that's tested separately in TestOptimize
            var result = achievement.RequirementsDebugString;
            result = result.Replace("byte(0x00000A) == 1", "A");
            result = result.Replace("byte(0x00000B) == 1", "B");
            result = result.Replace("byte(0x00000C) == 1", "C");
            result = result.Replace("byte(0x00000D) == 1", "D");
            result = result.Replace("byte(0x00000E) == 1", "E");
            result = result.Replace("byte(0x00000F) == 1", "F");

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void TestOrExpansionLarge()
        {
            var largeExpression = "(A || B || C) && (D || E || F) && (A || C || E)";

            // cross-multiplication would result in 27 clauses. if more than 20 would be generated,
            // the code switches to using OrNext and cross-multiplying simpler clauses.
            // in this case, OrNext can be used in all clauses, so the output should match the input.
            TestOrExpansion(largeExpression, largeExpression);
        }

        [Test]
        public void TestNotAlwaysFalse()
        {
            var achievement = CreateAchievement("!always_false()");
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo("always_true()"));
        }

        [Test]
        public void TestNotAlwaysTrue()
        {
            var achievement = CreateAchievement("!always_true()");
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo("always_false()"));
        }

        [Test]
        public void TestNotAlwaysFalseChain()
        {
            var achievement = CreateAchievement("!(always_false() || byte(0x1234) == 2 || byte(0x1234) == 5)");
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo("byte(0x001234) != 2 && byte(0x001234) != 5"));
        }

        [Test]
        public void TestMemoryReferenceWithoutComparison()
        {
            CreateAchievement("byte(0x1234)", "expression is not a requirement expression");
            CreateAchievement("byte(0x1234) && byte(0x2345) == 1", "expression is not a requirement expression");
            CreateAchievement("byte(0x1234) == 1 && byte(0x2345)", "expression is not a requirement expression");
            CreateAchievement("byte(0x1234) || byte(0x2345) == 1", "expression is not a requirement expression");
            CreateAchievement("byte(0x1234) == 1 || byte(0x2345)", "expression is not a requirement expression");
            CreateAchievement("byte(0x1234) * 2", "expression is not a requirement expression");
            CreateAchievement("byte(0x1234) * byte(0x1234)", "expression is not a requirement expression");
            CreateAchievement("byte(0x1234) / 2", "expression is not a requirement expression");
            CreateAchievement("byte(0x1234) / byte(0x1234)", "expression is not a requirement expression");
            CreateAchievement("byte(0x1234) & 15", "expression is not a requirement expression");
            CreateAchievement("byte(0x1234) & byte(0x1234)", "expression is not a requirement expression");
        }

        [Test]
        public void TestNestedComparison()
        {
            CreateAchievement("(byte(0x1234) == 2) == 1", "Cannot chain comparisons");
        }

        [Test]
        public void TestTriggerWhenDistribution()
        {
            var achievement = CreateAchievement("once(byte(0x1234) == 1) && " +
                "trigger_when(byte(0x2345) == 2 && byte(0x2346) == 3 && (byte(0x2347) == 0 || byte(0x2347) == 1)) && " +
                "never(byte(0x3456) == 2)");
            achievement.Optimize();
            Assert.That(achievement.SerializeRequirements(new SerializationContext()), 
                Is.EqualTo("0xH001234=1.1._T:0xH002345=2_T:0xH002346=3_R:0xH003456=2ST:0xH002347=0ST:0xH002347=1"));
        }

        [Test]
        public void TestOrFirst()
        {
            var achievement = CreateAchievement("(byte(0x1234) == 1 || byte(0x1234) == 2) && " +
                "never(byte(0x2345) == 3) && once(byte(0x3456) == 4)");
            achievement.Optimize();
            Assert.That(achievement.SerializeRequirements(new SerializationContext()),
                Is.EqualTo("R:0xH002345=3_0xH003456=4.1.S0xH001234=1S0xH001234=2"));
        }

        [Test]
        public void TestGuardedResetIfWithAlts()
        {
            var achievement = CreateAchievement("once(byte(0x1234) == 1) && " +
                "(always_true() || (always_false() &&" +
                " never(byte(0x2345) == 2) && unless(byte(0x3456) == 3))) && " +
                "(byte(0x4567) == 1 || byte(0x004567) == 2)");
            achievement.Optimize();
            Assert.That(achievement.SerializeRequirements(new SerializationContext()),
                Is.EqualTo("0xH001234=1.1.S0xH004567=1S0xH004567=2SR:0xH002345=2_P:0xH003456=3_0=1"));
        }

        [Test]
        public void TestTriggerWhenMeasured()
        {
            var achievement = CreateAchievement("byte(0x1234) == 1 && trigger_when(measured(repeated(3, byte(0x2345) == 6)))");
            achievement.Optimize();
            Assert.That(achievement.SerializeRequirements(new SerializationContext()),
                Is.EqualTo("0xH001234=1SM:0xH002345=6.3.ST:0=1"));
        }

        [Test]
        public void TestResetNextIfMultiple()
        {
            var achievement = CreateAchievement("once(byte(0x1234) == 1) && " +
                "tally(2," +
                "  byte(0x2345) == 2 &&" +
                "  never(byte(0x3456) == 1) && never(byte(0x3456) == 2)" +
                ")");
            achievement.Optimize();
            Assert.That(achievement.SerializeRequirements(new SerializationContext()),
                Is.EqualTo("0xH001234=1.1._O:0xH003456=1_Z:0xH003456=2_0xH002345=2.2."));
        }

        [Test]
        public void TestResetNextIfOr()
        {
            var achievement = CreateAchievement("once(byte(0x1234) == 1) && " +
                "tally(2," +
                "  byte(0x2345) == 2 &&" +
                "  never(byte(0x3456) == 1 || byte(0x3456) == 2)" +
                ")");
            achievement.Optimize();
            Assert.That(achievement.SerializeRequirements(new SerializationContext()),
                Is.EqualTo("0xH001234=1.1._O:0xH003456=1_Z:0xH003456=2_0xH002345=2.2."));
        }

        [Test]
        public void TestResetNextIfOrNext()
        {
            var achievement = CreateAchievement("once(byte(0x1234) == 1) && " +
                "tally(2," +
                "  byte(0x2345) == 2 &&" +
                "  never(__ornext(byte(0x3456) == 1 || byte(0x3456) == 2))" +
                ")");
            achievement.Optimize();
            Assert.That(achievement.SerializeRequirements(new SerializationContext()),
                Is.EqualTo("0xH001234=1.1._O:0xH003456=1_Z:0xH003456=2_0xH002345=2.2."));
        }

        [Test]
        public void TestResetNextIfRecall()
        {
            // when ResetNextIf converted to ResetIf, it has to pull the Remember with it.
            var achievement = CreateAchievement(
                "never(byte(0x1234) == 1) && (" +
                "  measured(repeated(3," +
                "    byte(0x2345) == 2 && (byte(0x1111) + byte(0x2222)) % 2 == 1 &&" +
                "    never(byte(0x2345) == 2 && (byte(0x1111) + byte(0x2222)) % 2 != 1)" +
                "  ))" +
                ")");
            achievement.Optimize();
            Assert.That(achievement.SerializeRequirements(new SerializationContext()),
                Is.EqualTo("R:0xH001234=1_N:0xH002345=2_A:0xH001111_K:0xH002222_A:{recall}%2_R:0!=1_N:0xH002345=2_A:0xH001111_K:0xH002222_A:{recall}%2_M:0=1.3."));
        }
    }
}
