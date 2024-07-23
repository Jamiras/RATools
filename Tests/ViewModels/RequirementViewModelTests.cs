using Jamiras.Components;
using Moq;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Expressions.Trigger;
using RATools.Services;
using RATools.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Tests.ViewModels
{
    [TestFixture]
    class RequirementViewModelTests
    {
        [Test]
        [TestCase("1=1", "always_true()")]
        [TestCase("0xH1234=7", "byte(0x001234) == 7")]
        [TestCase("R:0xH1234=7", "never(byte(0x001234) == 7)")]
        [TestCase("A:0xH1234=0_0xH2345=7", "byte(0x001234) + ")]
        [TestCase("B:0xH1234=0_0xH2345=7", "-byte(0x001234) + ")]
        [TestCase("I:0xH1234_0xH2345=7", "AddAddress byte(0x001234)")]
        [TestCase("C:0xH1234=7_0xH2345=7", "AddHits byte(0x001234) == 7")]
        [TestCase("D:0xH1234=7_0xH2345=7", "SubHits byte(0x001234) == 7")]
        [TestCase("N:0xH1234=7_0xH2345=7", "AndNext byte(0x001234) == 7")]
        [TestCase("O:0xH1234=7_0xH2345=7", "OrNext byte(0x001234) == 7")]
        public void TestDefinition(string serialized, string expected)
        {
            var builder = new AchievementBuilder();
            builder.ParseRequirements(Tokenizer.CreateTokenizer(serialized));

            var requirement = builder.ToAchievement().CoreRequirements.First();
            var notes = new Dictionary<uint, string>();
            var vmRequirement = new RequirementViewModel(requirement, NumberFormat.Decimal, notes);

            Assert.That(vmRequirement.Definition, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("0xH1111=7", "")]
        [TestCase("0xH1234=7", "Addr1")]
        [TestCase("7=0xH1234", "Addr1")]
        [TestCase("0xH1234=d0xH1234", "Addr1")]
        [TestCase("0xH1234=0xH2345", "0x001234:Addr1\r\n0x002345:Addr2")]
        [TestCase("0xH1234=0xH1111", "0x001234:Addr1")]
        [TestCase("0xH1111=0xH1234", "0x001234:Addr1")]
        [TestCase("0xH2222=0xH1111", "")]
        [TestCase("0xH3456", "This note is long enough that it will need to be wrapped.")]
        [TestCase("0xH4567", "This note\nis multiple\nlines.")]
        public void TestNotes(string serialized, string expected)
        {
            var notes = new Dictionary<uint, string>();
            notes[0x1234] = "Addr1";
            notes[0x2345] = "Addr2";
            notes[0x3456] = "This note is long enough that it will need to be wrapped.";
            notes[0x4567] = "This note\nis multiple\nlines.";

            var builder = new AchievementBuilder();
            builder.ParseRequirements(Tokenizer.CreateTokenizer(serialized));
            var requirement = builder.ToAchievement().CoreRequirements.First();
            var vmRequirement = new RequirementViewModel(requirement, NumberFormat.Decimal, notes);

            Assert.That(vmRequirement.Notes, Is.EqualTo(expected));
        }

        [TestCase("0xH1111=7", "")]
        [TestCase("0xH1234=7", "Addr1")]
        [TestCase("0xH2345=7", "Addr2")]
        public void TestNoteShortening(string serialized, string expected)
        {
            var notes = new Dictionary<uint, string>();
            notes[0x1234] = "Addr1";
            notes[0x2345] = "Addr2\r\n0=True\r\n1=False";

            var builder = new AchievementBuilder();
            builder.ParseRequirements(Tokenizer.CreateTokenizer(serialized));
            var requirement = builder.ToAchievement().CoreRequirements.First();
            var vmRequirement = new RequirementViewModel(requirement, NumberFormat.Decimal, notes);

            Assert.That(vmRequirement.NotesShort, Is.EqualTo(expected));

            if (expected == vmRequirement.Notes)
                Assert.IsFalse(vmRequirement.IsNoteShortened);
            else
                Assert.IsTrue(vmRequirement.IsNoteShortened);
        }

        [Test]
        public void TestAddSourceTrailingConstantComparison()
        {
            var mockSettings = new Mock<ISettings>();
            mockSettings.Setup(s => s.HexValues).Returns(false);
            ServiceRepository.Reset();
            ServiceRepository.Instance.RegisterInstance(mockSettings.Object);

            var requirement = new Requirement
            {
                Left = FieldFactory.CreateField(0),
                Operator = RequirementOperator.GreaterThan,
                Right = FieldFactory.CreateField(3)
            };

            var notes = new Dictionary<uint, string>();
            var vmRequirement = new RequirementViewModel(requirement, NumberFormat.Decimal, notes);

            Assert.That(vmRequirement.Definition, Is.EqualTo("always_false()"));

            vmRequirement.IsValueDependentOnPreviousRequirement = true;
            Assert.That(vmRequirement.Definition, Is.EqualTo("0 > 3"));

            ServiceRepository.Reset();
        }

        [Test]
        public void TestAddSourceTrailingConstantComparisonWithHits()
        {
            var mockSettings = new Mock<ISettings>();
            mockSettings.Setup(s => s.HexValues).Returns(false);
            ServiceRepository.Reset();
            ServiceRepository.Instance.RegisterInstance(mockSettings.Object);

            var requirement = new Requirement
            {
                Left = FieldFactory.CreateField(0),
                Operator = RequirementOperator.GreaterThan,
                Right = FieldFactory.CreateField(3),
                HitCount = 10,
            };

            var notes = new Dictionary<uint, string>();
            var vmRequirement = new RequirementViewModel(requirement, NumberFormat.Decimal, notes);

            Assert.That(vmRequirement.Definition, Is.EqualTo("repeated(10, always_false())"));

            vmRequirement.IsValueDependentOnPreviousRequirement = true;
            Assert.That(vmRequirement.Definition, Is.EqualTo("repeated(10, 0 > 3)"));

            ServiceRepository.Reset();
        }
    }
}
