using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Test.ViewModels
{
    [TestFixture]
    class RequirementComparisonViewModelTests
    {
        [Test]
        [TestCase("1=1", "2=2", "always_true()", "always_true()", false)]
        [TestCase("1=1", "0=1", "always_true()", "always_false()", true)]
        [TestCase("0xH1234=7", "0xH1234=7", "byte(0x001234) == 7", "byte(0x001234) == 7", false)]
        [TestCase("0xH1234=7", "0xH2345=7", "byte(0x001234) == 7", "byte(0x002345) == 7", true)]
        [TestCase("0xH1234=7", "0xH1234=6", "byte(0x001234) == 7", "byte(0x001234) == 6", true)]
        [TestCase("0xH1234=7", "0x 1234=7", "byte(0x001234) == 7", "word(0x001234) == 7", true)]
        [TestCase("0xH1234=7", "R:0xH1234=7", "byte(0x001234) == 7", "never(byte(0x001234) == 7)", true)]
        [TestCase("0xH1234=7", "0xH1234=7.1.", "byte(0x001234) == 7", "once(byte(0x001234) == 7)", true)]
        [TestCase("0xH1234>0.1.", "0xH1234!=0.1.", "once(byte(0x001234) > 0)", "once(byte(0x001234) != 0)", true)]
        [TestCase("0xM1234>0.1.", "0xM1234=1.1.", "once(bit0(0x001234) > 0)", "once(bit0(0x001234) == 1)", true)]
        public void TestDefinitions(string leftSerialized, string rightSerialized, 
            string expectedDefinition, string expectedOtherDefinition, bool expectedModified)
        {
            var notes = new Dictionary<int, string>();

            var builder = new AchievementBuilder();
            builder.ParseRequirements(Tokenizer.CreateTokenizer(leftSerialized));
            var requirement = builder.ToAchievement().CoreRequirements.First();

            builder = new AchievementBuilder();
            builder.ParseRequirements(Tokenizer.CreateTokenizer(rightSerialized));
            var compareRequirement = builder.ToAchievement().CoreRequirements.First();

            var vmRequirement = new RequirementComparisonViewModel(requirement, compareRequirement, NumberFormat.Decimal, notes);

            Assert.That(vmRequirement.Definition, Is.EqualTo(expectedDefinition));
            Assert.That(vmRequirement.OtherDefinition, Is.EqualTo(expectedOtherDefinition));
            Assert.That(vmRequirement.IsModified, Is.EqualTo(expectedModified));
        }

        [Test]
        public void TestAddedRequirement()
        {
            var notes = new Dictionary<int, string>();

            var builder = new AchievementBuilder();
            builder.ParseRequirements(Tokenizer.CreateTokenizer("0xH1234=7"));
            var requirement = builder.ToAchievement().CoreRequirements.First();

            var vmRequirement = new RequirementComparisonViewModel(requirement, null, NumberFormat.Decimal, notes);

            Assert.That(vmRequirement.Definition, Is.EqualTo("byte(0x001234) == 7"));
            Assert.That(vmRequirement.OtherDefinition, Is.EqualTo(""));
            Assert.That(vmRequirement.IsModified, Is.True);
        }

        [Test]
        public void TestRemovedRequirement()
        {
            var notes = new Dictionary<int, string>();

            var builder = new AchievementBuilder();
            builder.ParseRequirements(Tokenizer.CreateTokenizer("0xH1234=7"));
            var requirement = builder.ToAchievement().CoreRequirements.First();

            var vmRequirement = new RequirementComparisonViewModel(null, requirement, NumberFormat.Decimal, notes);

            Assert.That(vmRequirement.Definition, Is.EqualTo(""));
            Assert.That(vmRequirement.OtherDefinition, Is.EqualTo("byte(0x001234) == 7"));
            Assert.That(vmRequirement.IsModified, Is.True);
        }
    }
}
