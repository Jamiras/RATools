using Jamiras.Components;
using Jamiras.Core.Tests;
using Jamiras.Services;
using Moq;
using NUnit.Framework;
using RATools.Data;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RATools.Parser.Tests
{
    [TestFixture]
    class MemoryAccessorAliasTests
    {
        [Test]
        public void TestInitialize()
        {
            var alias = new MemoryAccessorAlias(0x1234);
            Assert.That(alias.Alias, Is.EqualTo(""));
            Assert.That(alias.Address, Is.EqualTo(0x1234));
            Assert.That(alias.Children, Is.Empty);
            Assert.That(alias.HasMultipleReferencedSizes, Is.False);
            Assert.That(alias.Note, Is.Null);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.None));
        }

        [Test]
        public void TestInitializeFromNote()
        {
            var note = new CodeNote(0x1230, "[32-bit] This is a note.");
            var alias = new MemoryAccessorAlias(0x1234, note);
            Assert.That(alias.Alias, Is.EqualTo(""));
            Assert.That(alias.Address, Is.EqualTo(0x1234));
            Assert.That(alias.Children, Is.Empty);
            Assert.That(alias.HasMultipleReferencedSizes, Is.False);
            Assert.That(alias.Note, Is.SameAs(note));
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.DWord));
        }

        [Test]
        public void TestReferenceSize()
        {
            var alias = new MemoryAccessorAlias(0x1234);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.None));
            Assert.That(alias.ReferencedSizes, Is.Empty);
            Assert.That(alias.HasMultipleReferencedSizes, Is.False);
            Assert.That(alias.HasReferencedSize(FieldSize.None), Is.False);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.None), Is.False);

            alias.ReferenceSize(FieldSize.Byte);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.Byte));
            Assert.That(alias.ReferencedSizes.Count(), Is.EqualTo(1));
            Assert.That(alias.ReferencedSizes.First(), Is.EqualTo(FieldSize.Byte));
            Assert.That(alias.HasMultipleReferencedSizes, Is.False);
            Assert.That(alias.HasReferencedSize(FieldSize.Byte), Is.True);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.Byte), Is.True);

            alias.ReferenceSize(FieldSize.Byte);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.Byte));
            Assert.That(alias.ReferencedSizes.Count(), Is.EqualTo(1));
            Assert.That(alias.ReferencedSizes.First(), Is.EqualTo(FieldSize.Byte));
            Assert.That(alias.HasMultipleReferencedSizes, Is.False);
            Assert.That(alias.HasReferencedSize(FieldSize.Byte), Is.True);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.Byte), Is.True);

            alias.ReferenceSize(FieldSize.Word);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.Byte));
            Assert.That(alias.ReferencedSizes.Count(), Is.EqualTo(2));
            Assert.That(alias.ReferencedSizes.First(), Is.EqualTo(FieldSize.Byte));
            Assert.That(alias.ReferencedSizes.Last(), Is.EqualTo(FieldSize.Word));
            Assert.That(alias.HasMultipleReferencedSizes, Is.True);
            Assert.That(alias.HasReferencedSize(FieldSize.Byte), Is.True);
            Assert.That(alias.HasReferencedSize(FieldSize.Word), Is.True);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.Byte), Is.False);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.Word), Is.False);
        }

        [Test]
        public void TestReferenceSizeBits()
        {
            var alias = new MemoryAccessorAlias(0x1234);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.None));
            Assert.That(alias.ReferencedSizes, Is.Empty);
            Assert.That(alias.HasMultipleReferencedSizes, Is.False);
            Assert.That(alias.HasReferencedSize(FieldSize.None), Is.False);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.None), Is.False);

            alias.ReferenceSize(FieldSize.Bit5);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.Byte));
            Assert.That(alias.ReferencedSizes.Count(), Is.EqualTo(1));
            Assert.That(alias.ReferencedSizes.First(), Is.EqualTo(FieldSize.Bit5));
            Assert.That(alias.HasMultipleReferencedSizes, Is.False);
            Assert.That(alias.HasReferencedSize(FieldSize.Byte), Is.False);
            Assert.That(alias.HasReferencedSize(FieldSize.Bit5), Is.True);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.Byte), Is.False);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.Bit5), Is.True);

            alias.ReferenceSize(FieldSize.Bit2);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.Byte));
            Assert.That(alias.ReferencedSizes.Count(), Is.EqualTo(2));
            Assert.That(alias.ReferencedSizes.First(), Is.EqualTo(FieldSize.Bit2));
            Assert.That(alias.ReferencedSizes.Last(), Is.EqualTo(FieldSize.Bit5));
            Assert.That(alias.HasMultipleReferencedSizes, Is.True);
            Assert.That(alias.HasReferencedSize(FieldSize.Byte), Is.False);
            Assert.That(alias.HasReferencedSize(FieldSize.Bit2), Is.True);
            Assert.That(alias.HasReferencedSize(FieldSize.Bit5), Is.True);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.Byte), Is.False);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.Bit2), Is.False);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.Bit5), Is.False);

            alias.ReferenceSize(FieldSize.Word);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.Byte));
            Assert.That(alias.ReferencedSizes.Count(), Is.EqualTo(3));
            Assert.That(alias.HasMultipleReferencedSizes, Is.True);
            Assert.That(alias.HasReferencedSize(FieldSize.Byte), Is.False);
            Assert.That(alias.HasReferencedSize(FieldSize.Bit2), Is.True);
            Assert.That(alias.HasReferencedSize(FieldSize.Bit5), Is.True);
            Assert.That(alias.HasReferencedSize(FieldSize.Word), Is.True);
        }

        [Test]
        public void TestReferenceSizeBitCount()
        {
            var alias = new MemoryAccessorAlias(0x1234);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.None));
            Assert.That(alias.ReferencedSizes, Is.Empty);
            Assert.That(alias.HasMultipleReferencedSizes, Is.False);
            Assert.That(alias.HasReferencedSize(FieldSize.None), Is.False);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.None), Is.False);

            alias.ReferenceSize(FieldSize.BitCount);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.Byte));
            Assert.That(alias.ReferencedSizes.Count(), Is.EqualTo(1));
            Assert.That(alias.ReferencedSizes.First(), Is.EqualTo(FieldSize.BitCount));
            Assert.That(alias.HasMultipleReferencedSizes, Is.False);
            Assert.That(alias.HasReferencedSize(FieldSize.Byte), Is.False);
            Assert.That(alias.HasReferencedSize(FieldSize.BitCount), Is.True);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.Byte), Is.False);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.BitCount), Is.True);

            alias.ReferenceSize(FieldSize.Bit2);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.Byte));
            Assert.That(alias.ReferencedSizes.Count(), Is.EqualTo(2));
            Assert.That(alias.ReferencedSizes.First(), Is.EqualTo(FieldSize.Bit2));
            Assert.That(alias.ReferencedSizes.Last(), Is.EqualTo(FieldSize.BitCount));
            Assert.That(alias.HasMultipleReferencedSizes, Is.True);
            Assert.That(alias.HasReferencedSize(FieldSize.Byte), Is.False);
            Assert.That(alias.HasReferencedSize(FieldSize.Bit2), Is.True);
            Assert.That(alias.HasReferencedSize(FieldSize.BitCount), Is.True);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.Byte), Is.False);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.Bit2), Is.False);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.BitCount), Is.False);
        }


        [Test]
        public void TestReferenceSizeEndianness()
        {
            var alias = new MemoryAccessorAlias(0x1234);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.None));
            Assert.That(alias.ReferencedSizes, Is.Empty);
            Assert.That(alias.HasMultipleReferencedSizes, Is.False);
            Assert.That(alias.HasReferencedSize(FieldSize.None), Is.False);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.None), Is.False);

            alias.ReferenceSize(FieldSize.Word);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.Word));
            Assert.That(alias.ReferencedSizes.Count(), Is.EqualTo(1));
            Assert.That(alias.ReferencedSizes.First(), Is.EqualTo(FieldSize.Word));
            Assert.That(alias.HasMultipleReferencedSizes, Is.False);
            Assert.That(alias.HasReferencedSize(FieldSize.Word), Is.True);
            Assert.That(alias.HasReferencedSize(FieldSize.BigEndianWord), Is.False);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.Word), Is.True);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.BigEndianWord), Is.False);

            alias.ReferenceSize(FieldSize.BigEndianWord);
            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.Word));
            Assert.That(alias.ReferencedSizes.Count(), Is.EqualTo(2));
            Assert.That(alias.ReferencedSizes.First(), Is.EqualTo(FieldSize.Word));
            Assert.That(alias.ReferencedSizes.Last(), Is.EqualTo(FieldSize.BigEndianWord));
            Assert.That(alias.HasMultipleReferencedSizes, Is.True);
            Assert.That(alias.HasReferencedSize(FieldSize.Word), Is.True);
            Assert.That(alias.HasReferencedSize(FieldSize.BigEndianWord), Is.True);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.Word), Is.False);
            Assert.That(alias.IsOnlyReferencedSize(FieldSize.BigEndianWord), Is.False);
        }

        [Test]
        [TestCase("Test", "test")]
        [TestCase("Stage/Level", "stage_level")]
        [TestCase("[16-bit] Score", "score")] // size stripped by code note processing
        [TestCase("Mario's Lives", "marios_lives")] // apostrophe doesn't separate words
        [TestCase("Score (BCD)", "score")] // "BCD" stored in subtext for conflict resolution
        [TestCase("Score (US)", "score_us")] // regional markers are kept
        [TestCase("Score (EU)", "score_eu")] // regional markers are kept
        [TestCase("Score (JP)", "score_jp")] // regional markers are kept
        [TestCase("Stage (1=First, 2=Second)", "stage")] // value clause will be stripped by code note processing
        [TestCase("Stage (1=First, 2=Second) - monday", "stage")] // "- monday" consumed by code note processing
        [TestCase("Stage - 1=First, 2=Second - monday", "stage")] // assume "- monday" is part of 2=
        [TestCase("1-1 Time Attack", "time_attack")] // "1-1" stored in subtext for conflict resolution
        [TestCase("Bit7=Villager 1 exists", "villager_1_exists")] // use bit description if single entry and no header
        [TestCase("01=Happy", "happy")] // use enum value if single entry and no header
        public void TestUpdateAliasFromNote(string note, string expected)
        {
            var codeNote = new CodeNote(0x1234, note);
            var alias = new MemoryAccessorAlias(0x1234, codeNote);
            alias.ReferenceSize(FieldSize.Byte);
            alias.UpdateAliasFromNote(NameStyle.SnakeCase);

            Assert.That(alias.Alias, Is.EqualTo(expected));
        }

        [Test]
        public void TestUpdateAliasFromNoteBitSubset()
        {
            var codeNote = new CodeNote(0x1234, "Header\r\nupper4=hi\r\nlower4=lo");
            var alias = new MemoryAccessorAlias(0x1234, codeNote);
            alias.ReferenceSize(FieldSize.HighNibble);
            alias.UpdateAliasFromNote(NameStyle.SnakeCase);

            Assert.That(alias.Alias, Is.EqualTo("header_hi"));
        }

        [Test]
        public void TestUpdateAliasFromNoteNibbleSubset()
        {
            var codeNote = new CodeNote(0x1234, "Header\r\nupper4=hi\r\nlower4=lo");
            var alias = new MemoryAccessorAlias(0x1234, codeNote);
            alias.ReferenceSize(FieldSize.HighNibble);
            alias.UpdateAliasFromNote(NameStyle.SnakeCase);

            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.HighNibble));
            Assert.That(alias.Alias, Is.EqualTo("header_hi"));
        }

        [Test]
        public void TestGetAliasLargeSizes()
        {
            var codeNote = new CodeNote(0x1234, "[16-bit] Header");
            var alias = new MemoryAccessorAlias(0x1234, codeNote);
            alias.ReferenceSize(FieldSize.Word);
            alias.ReferenceSize(FieldSize.DWord);
            alias.ReferenceSize(FieldSize.Byte);
            alias.UpdateAliasFromNote(NameStyle.SnakeCase);

            Assert.That(alias.PrimarySize, Is.EqualTo(FieldSize.Word));
            Assert.That(alias.Alias, Is.EqualTo("header"));
            Assert.That(alias.GetAlias(FieldSize.Word), Is.EqualTo("header"));
            Assert.That(alias.GetAlias(FieldSize.DWord), Is.EqualTo("header_dword"));
            Assert.That(alias.GetAlias(FieldSize.Byte), Is.EqualTo("header_byte"));
            Assert.That(alias.GetAlias(FieldSize.BigEndianWord), Is.Null);
        }

        [Test]
        public void TestResolveConflictingAliases()
        {
            var list = new List<MemoryAccessorAlias>();
            list.Add(new MemoryAccessorAlias(0x1234, new CodeNote(0x1234, "Score")));
            list.Add(new MemoryAccessorAlias(0x1235, new CodeNote(0x1235, "Score")));
            list.Add(new MemoryAccessorAlias(0x1236, new CodeNote(0x1236, "Score")));
            list.Add(new MemoryAccessorAlias(0x1240, new CodeNote(0x1240, "Lives")));
            list.Add(new MemoryAccessorAlias(0x1248, new CodeNote(0x1248, "Level")));
            list.Add(new MemoryAccessorAlias(0x124C, new CodeNote(0x1248, "Sub-level")));
            list.Add(new MemoryAccessorAlias(0x1280, new CodeNote(0x1280, "Level (New Game+)")));
            list.Add(new MemoryAccessorAlias(0x12C0, new CodeNote(0x12C0, "Character (New Game+)")));

            foreach (var alias in list)
                alias.UpdateAliasFromNote(NameStyle.CamelCase);

            Assert.That(list[0].Alias, Is.EqualTo("score"));
            Assert.That(list[1].Alias, Is.EqualTo("score"));
            Assert.That(list[2].Alias, Is.EqualTo("score"));
            Assert.That(list[3].Alias, Is.EqualTo("lives"));
            Assert.That(list[4].Alias, Is.EqualTo("level"));
            Assert.That(list[5].Alias, Is.EqualTo("sublevel"));
            Assert.That(list[6].Alias, Is.EqualTo("level"));
            Assert.That(list[7].Alias, Is.EqualTo("character"));

            MemoryAccessorAlias.ResolveConflictingAliases(list);

            Assert.That(list[0].Alias, Is.EqualTo("score"));
            Assert.That(list[1].Alias, Is.EqualTo("score_2"));
            Assert.That(list[2].Alias, Is.EqualTo("score_3"));
            Assert.That(list[3].Alias, Is.EqualTo("lives"));
            Assert.That(list[4].Alias, Is.EqualTo("level"));
            Assert.That(list[5].Alias, Is.EqualTo("sublevel"));
            Assert.That(list[6].Alias, Is.EqualTo("levelNewGame"));
            Assert.That(list[7].Alias, Is.EqualTo("character"));
        }

        [Test]
        public void TestAddMemoryAccessorsAchievement()
        {
            var definition = "A:0xX1234_R:d0xH1111=67S0x 2000!=d0x 2000S0xH2000=0xH1337";
            var builder = new AchievementBuilder();
            builder.ParseRequirements(Tokenizer.CreateTokenizer(definition));
            var achievement = builder.ToAchievement();

            var codeNotes = new Dictionary<uint, CodeNote>();
            codeNotes[0x2000] = new CodeNote(0x2000, "[8-bit] Column");
            codeNotes[0x1111] = new CodeNote(0x1111, "[8-bit] Row");

            var list = new List<MemoryAccessorAlias>();
            MemoryAccessorAlias.AddMemoryAccessors(list, achievement, codeNotes);

            Assert.That(list.Count, Is.EqualTo(4));
            Assert.That(list[0].Address, Is.EqualTo(0x1111));
            Assert.That(list[0].HasReferencedSize(FieldSize.Byte), Is.True);
            Assert.That(list[0].Note, Is.SameAs(codeNotes[0x1111]));
            Assert.That(list[1].Address, Is.EqualTo(0x1234));
            Assert.That(list[1].HasReferencedSize(FieldSize.DWord), Is.True);
            Assert.That(list[1].Note, Is.Null);
            Assert.That(list[2].Address, Is.EqualTo(0x1337));
            Assert.That(list[2].HasReferencedSize(FieldSize.Byte), Is.True);
            Assert.That(list[2].Note, Is.Null);
            Assert.That(list[3].Address, Is.EqualTo(0x2000));
            Assert.That(list[3].HasReferencedSize(FieldSize.Word), Is.True);
            Assert.That(list[3].HasReferencedSize(FieldSize.Byte), Is.True);
            Assert.That(list[3].Note, Is.SameAs(codeNotes[0x2000]));
        }
    }
}
