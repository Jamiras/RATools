﻿using NUnit.Framework;
using RATools.Data;
using System.Text;

namespace RATools.Parser.Tests.Internal
{
    [TestFixture]
    class ScriptBuilderContextTests
    {
        [Test]
        [TestCase("0xH001234=1", "byte(0x001234) == 1")]
        [TestCase("P:0xH001234=1", "unless(byte(0x001234) == 1)")]
        [TestCase("R:0xH001234=1", "never(byte(0x001234) == 1)")]
        [TestCase("0xH001234=1.1.", "once(byte(0x001234) == 1)")]
        [TestCase("0xH001234=1.99.", "repeated(99, byte(0x001234) == 1)")]
        [TestCase("0xH001234=1_0xH002345=2", "byte(0x001234) == 1|byte(0x002345) == 2")]
        [TestCase("A:0xH001234=0_0xH002345=2", "(byte(0x001234) + byte(0x002345)) == 2")]
        [TestCase("B:0xH001234=0_0xH002345=2", "(byte(0x002345) - byte(0x001234)) == 2")]
        [TestCase("A:0xH001234=0_R:0xH002345=2", "never((byte(0x001234) + byte(0x002345)) == 2)")]
        [TestCase("I:0x 001234_0xH002345=2", "byte(word(0x001234) + 0x002345) == 2")]
        [TestCase("I:0x 001234_I:0x 002222_0xH002345=2", "byte(word(word(0x001234) + 0x002222) + 0x002345) == 2")]
        [TestCase("O:0xH001234=1_0xH002345=2", "(byte(0x001234) == 1 || byte(0x002345) == 2)")]
        [TestCase("O:0xH001234=1_0=1", "byte(0x001234) == 1")]
        [TestCase("O:0=1_0xH002345=2", "byte(0x002345) == 2")]
        [TestCase("O:0xH001234=1_0=1.15.", "repeated(15, byte(0x001234) == 1)")]
        [TestCase("O:0=1_0xH002345=2.15.", "repeated(15, byte(0x002345) == 2)")]
        [TestCase("O:0=1.12._0xH002345=2.15.", "repeated(15, byte(0x002345) == 2)")]
        [TestCase("O:0xH001234=1.15._0=1", "repeated(15, byte(0x001234) == 1)")]
        [TestCase("O:0=1.15._0xH002345=2", "byte(0x002345) == 2")]
        [TestCase("N:0xH001234=1_0xH002345=2", "(byte(0x001234) == 1 && byte(0x002345) == 2)")]
        [TestCase("N:0xH001234=1_1=1", "byte(0x001234) == 1")]
        [TestCase("N:1=1_0xH002345=2", "byte(0x002345) == 2")]
        [TestCase("N:0xH001234=1_1=1.15.", "repeated(15, byte(0x001234) == 1)")]
        [TestCase("N:1=1_0xH002345=2.15.", "repeated(15, byte(0x002345) == 2)")]
        [TestCase("N:0xH001234=1.15._1=1", "repeated(15, byte(0x001234) == 1)")]
        [TestCase("N:1=1.15._0xH002345=2", "(repeated(15, always_true()) && byte(0x002345) == 2)")]
        [TestCase("M:0xH001234>=100", "measured(byte(0x001234) >= 100)")]
        [TestCase("Z:0xH002345=2_0xH001234=1.2.", "repeated(2, byte(0x001234) == 1 && never(byte(0x002345) == 2))")]
        [TestCase("Z:0xH002345=2_P:0xH001234=1.1.", "disable_when(byte(0x001234) == 1, until=byte(0x002345) == 2)")]
        [TestCase("Z:0xH002345=2_P:0xH001234=1.2.", "disable_when(repeated(2, byte(0x001234) == 1), until=byte(0x002345) == 2)")]
        [TestCase("C:0xH001234=1.10._C:0xH001234=2.10._0=1.15.", "tally(15, repeated(10, byte(0x001234) == 1), repeated(10, byte(0x001234) == 2))")]
        [TestCase("A:9=0_C:d0xH001234<=0xH001234_A:10=0_d0xH001234<=0xH001234.70.", "tally(70, (9 + prev(byte(0x001234))) <= byte(0x001234), (10 + prev(byte(0x001234))) <= byte(0x001234))")]
        [TestCase("A:10_T:d0xH001234<=0xH001234.70.", "trigger_when(repeated(70, (10 + prev(byte(0x001234))) <= byte(0x001234)))")]
        [TestCase("A:9_C:d0xH001234<=0xH001234_A:10_T:d0xH001234<=0xH001234.70.", "trigger_when(tally(70, (9 + prev(byte(0x001234))) <= byte(0x001234), (10 + prev(byte(0x001234))) <= byte(0x001234)))")]
        [TestCase("N:0xU001234=d0xU001234_C:d0xL001234<=0xL001234_T:d0xH001234<=0xH001234.70.",
                  "trigger_when(tally(70, (high4(0x001234) == prev(high4(0x001234)) && prev(low4(0x001234)) <= low4(0x001234)), prev(byte(0x001234)) <= byte(0x001234)))")]
        [TestCase("N:0xU001234=d0xU001234_A:2_C:d0xL001234<=0xL001234_T:d0xH001234<=0xH001234.70.",
                  "trigger_when(tally(70, (high4(0x001234) == prev(high4(0x001234)) && (2 + prev(low4(0x001234))) <= low4(0x001234)), prev(byte(0x001234)) <= byte(0x001234)))")]
        [TestCase("A:2_C:d0xL001234<=0xL001234_N:0xU001234=d0xU001234_T:d0xH001234<=0xH001234.70.",
                  "trigger_when(tally(70, (2 + prev(low4(0x001234))) <= low4(0x001234), high4(0x001234) == prev(high4(0x001234)) && prev(byte(0x001234)) <= byte(0x001234)))")]
        [TestCase("C:0xH001234=1.1._P:1=1.1.",
                  "disable_when(tally(1, once(byte(0x001234) == 1), always_true()))")]
        [TestCase("C:0xH001234=1.1._D:0xH002345=1.1._R:0=1.1.",
                  "never(tally(1, once(byte(0x001234) == 1), deduct(once(byte(0x002345) == 1))))")]
        [TestCase("R:0xH001234<5_R:0xH001234>8_N:0xH002345=1_0xH003456=2.10.",
                  "never(byte(0x001234) < 5)|never(byte(0x001234) > 8)|repeated(10, byte(0x002345) == 1 && byte(0x003456) == 2)")]
        [TestCase("O:0xH001234<5_Z:0xH001234>8_N:0xH002345=1_0xH003456=2.10.",
                  "repeated(10, byte(0x002345) == 1 && byte(0x003456) == 2 && never(byte(0x001234) < 5 || byte(0x001234) > 8))")]
        [TestCase("Z:0xH004567=4_O:0xH001234=1_0xH002345=2.1.",
                  "once((byte(0x001234) == 1 || byte(0x002345) == 2) && never(byte(0x004567) == 4))")]
        [TestCase("N:0xH001234=1_M:0xH002345=2.100.",
                  "measured(repeated(100, byte(0x001234) == 1 && byte(0x002345) == 2))")]
        [TestCase("N:0xH001234=1_C:0xH002345=2_N:0xH001234=2_M:0xH002345=3",
                  "measured(tally(0, byte(0x001234) == 1 && byte(0x002345) == 2, byte(0x001234) == 2 && byte(0x002345) == 3))")]
        [TestCase("K:0xH001234*2_I:0xX002345+{recall}_0xH000000=3", "byte(dword(0x002345) + (byte(0x001234) * 2)) == 3")]
        [TestCase("K:0xH001234*2_I:0xX002345+{recall}_0xH000000={recall}", "byte(dword(0x002345) + (byte(0x001234) * 2)) == (byte(0x001234) * 2)")]
        public void TestAppendRequirements(string input, string expected)
        {
            var trigger = Trigger.Deserialize(input);
            var groups = RequirementEx.Combine(trigger.Core.Requirements);
            var context = new ScriptBuilderContext();

            var builder = new StringBuilder();
            foreach (var group in groups)
            {
                if (builder.Length > 0)
                    builder.Append('|');

                context.AppendRequirements(builder, group.Requirements);
            }

            Assert.That(builder.ToString(), Is.EqualTo(expected));

            // make sure we didn't modify the source requirements
            Assert.That(trigger.Serialize(new SerializationContext()), Is.EqualTo(input));
        }

        [Test]
        public void TestAppendRequirementsPauseRememberUsedByEarlierNonPauseRecall()
        {
            string input = "{recall}=5_K:0xH001234*2_I:0xX002345+{recall}_P:0xH000000=3";
            var context = new ScriptBuilderContext();
            var builder = new StringBuilder();

            var trigger = Trigger.Deserialize(input);
            context.AppendRequirements(builder, trigger.Core.Requirements);

            // the PauseIf will be moved forward in the logic chain so it's remember will precede the recall using it
            var expected = "unless(byte(dword(0x002345) + (byte(0x001234) * 2)) == 3) && (byte(0x001234) * 2) == 5";
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void TestAppendRequirementsAddSourceSubSourceChained()
        {
            string input = "B:0xM001234_A:0xK001234_3=9_B:d0xM001234_A:d0xK001234_3=8";
            var context = new ScriptBuilderContext();
            var builder = new StringBuilder();

            var trigger = Trigger.Deserialize(input);
            context.AppendRequirements(builder, trigger.Core.Requirements);

            var expected = "(bitcount(0x001234) + 3 - bit0(0x001234)) == 9 && (prev(bitcount(0x001234)) + 3 - prev(bit0(0x001234))) == 8";
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [TestCase("N:0xH001234=1_M:0xH002345=2",
          "measured(tally(0, byte(0x001234) == 1 && byte(0x002345) == 2))")]
        [TestCase("O:0xH001234=1_M:0xH002345=2",
          "measured(tally(0, byte(0x001234) == 1 || byte(0x002345) == 2))")]
        public void TestAppendRequirementsValue(string input, string expected)
        {
            var trigger = Trigger.Deserialize(input);
            var groups = RequirementEx.Combine(trigger.Core.Requirements);
            var context = new ScriptBuilderContext() { IsValue = true };

            var builder = new StringBuilder();
            foreach (var group in groups)
            {
                if (builder.Length > 0)
                    builder.Append('|');

                context.AppendRequirements(builder, group.Requirements);
            }

            Assert.That(builder.ToString(), Is.EqualTo(expected));

            // make sure we didn't modify the source requirements
            Assert.That(trigger.Serialize(new SerializationContext()), Is.EqualTo(input));
        }
    }
}
