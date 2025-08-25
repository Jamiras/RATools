using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Tests.ViewModels
{
    [TestFixture]
    class RequirementGroupViewModelTests
    {
        [Test]
        [TestCase("1=1", "2=2", "always_true()|always_true()")]
        [TestCase("1=1", "0=1", "always_true()|always_false()")]
        [TestCase("0xH1234=7", "0xH1234=7", "byte(0x001234) == 7|byte(0x001234) == 7")]
        [TestCase("0xH1234=7", "0xH2345=7", "byte(0x001234) == 7|byte(0x002345) == 7")]
        [TestCase("0xH1234=7", "0xH1234=6", "byte(0x001234) == 7|byte(0x001234) == 6")]
        [TestCase("0xH1234=7", "0x 1234=7", "byte(0x001234) == 7|word(0x001234) == 7")]
        [TestCase("0xH1234=7", "R:0xH1234=7", "byte(0x001234) == 7|never(byte(0x001234) == 7)")]
        [TestCase("0xH1234=7", "0xH1234=7.1.", "byte(0x001234) == 7|once(byte(0x001234) == 7)")]
        [TestCase("0xH1234=7_0xH1235=6", "0xH1234=6_R:0xH1235=7", // match by address
            "byte(0x001234) == 7|byte(0x001234) == 6\nbyte(0x001235) == 6|never(byte(0x001235) == 7)")]
        [TestCase("0xH1234=7_0xH1235=6", "0xH1234=6_R:0x 1235=7", // just a little too different
            "byte(0x001234) == 7|byte(0x001234) == 6\nbyte(0x001235) == 6|\n|never(word(0x001235) == 7)")]
        [TestCase("0xH1234=7_0xH1235=6", "0xH1234<7_0xH1238=2", // match first by address, second is unique
            "byte(0x001234) == 7|byte(0x001234) < 7\nbyte(0x001235) == 6|\n|byte(0x001238) == 2")]
        [TestCase("0xH1234=7_0xH1235=6", "0xH1235=6_0xH1234=7", // order changed (use left order)
            "byte(0x001234) == 7|byte(0x001234) == 7\nbyte(0x001235) == 6|byte(0x001235) == 6")]
        [TestCase("0xH1234=7_0xH1235=6", "0xH1235=6", // item added
            "byte(0x001234) == 7|\nbyte(0x001235) == 6|byte(0x001235) == 6")]
        [TestCase("0xH1234=7", "0xH1235=6_0xH1234=7", // item removed (keep original position of removed item)
            "|byte(0x001235) == 6\nbyte(0x001234) == 7|byte(0x001234) == 7")]
        [TestCase("P:0xH1234=7_P:0xH1235=6", "O:0xH1234=7_P:0xH1235=6", // unless(a||b) => unless(a) && unless(b)
            "unless(byte(0x001234) == 7)|OrNext byte(0x001234) == 7\nunless(byte(0x001235) == 6)|unless(byte(0x001235) == 6)")]
        [TestCase("0xH1234=7_0xH1235=6", "N:0xH1234=7_0xH1235=6", // AndNext a,b => a,b
            "byte(0x001234) == 7|AndNext byte(0x001234) == 7\nbyte(0x001235) == 6|byte(0x001235) == 6")]
        public void TestDiff(string leftSerialized, string rightSerialized, string expected)
        {
            var notes = new Dictionary<uint, CodeNote>();

            var builder = new AchievementBuilder();
            builder.ParseRequirements(Tokenizer.CreateTokenizer(leftSerialized));
            var leftRequirements = builder.ToAchievement().CoreRequirements;

            builder = new AchievementBuilder();
            builder.ParseRequirements(Tokenizer.CreateTokenizer(rightSerialized));
            var rightRequirements = builder.ToAchievement().CoreRequirements;

            var vmRequirementGroup = new RequirementGroupViewModel("Group", leftRequirements, rightRequirements, NumberFormat.Decimal, notes);

            var strBuilder = new StringBuilder();
            foreach (var vmRequirement in vmRequirementGroup.Requirements)
            {
                var vmComparison = vmRequirement as RequirementComparisonViewModel;
                strBuilder.Append(vmComparison.Definition);
                strBuilder.Append('|');
                strBuilder.Append(vmComparison.OtherDefinition);
                strBuilder.Append('\n');
            }
            strBuilder.Length--;

            Assert.That(strBuilder.ToString(), Is.EqualTo(expected));
        }
    }
}
