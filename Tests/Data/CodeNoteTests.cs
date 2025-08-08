using NUnit.Framework;
using System.Linq;

namespace RATools.Data.Tests
{
    [TestFixture]
    class CodeNoteTests
    {
        [Test]
        [TestCase("", 1, FieldSize.None)]
        [TestCase("Test", 1, FieldSize.None)]
        [TestCase("16-bit Test", 2, FieldSize.Word)]
        [TestCase("Test 16-bit", 2, FieldSize.Word)]
        [TestCase("Test 16-bi", 1, FieldSize.None)]
        [TestCase("[16-bit] Test", 2, FieldSize.Word)]
        [TestCase("[16 bit] Test", 2, FieldSize.Word)]
        [TestCase("[16 Bit] Test", 2, FieldSize.Word)]
        [TestCase("[24-bit] Test", 3, FieldSize.TByte)]
        [TestCase("[32-bit] Test", 4, FieldSize.DWord)]
        [TestCase("[32 bit] Test", 4, FieldSize.DWord)]
        [TestCase("[32bit] Test", 4, FieldSize.DWord)]
        [TestCase("Test [16-bit]", 2, FieldSize.Word)]
        [TestCase("Test (16-bit)", 2, FieldSize.Word)]
        [TestCase("Test (16 bits)", 2, FieldSize.Word)]
        [TestCase("[64-bit] Test", 8, FieldSize.Array)]
        [TestCase("[128-bit] Test", 16, FieldSize.Array)]
        [TestCase("[17-bit] Test", 3, FieldSize.TByte)]
        [TestCase("[100-bit] Test", 13, FieldSize.Array)]
        [TestCase("[0-bit] Test", 1, FieldSize.None)]
        [TestCase("[1-bit] Test", 1, FieldSize.Byte)]
        [TestCase("[4-bit] Test", 1, FieldSize.Byte)]
        [TestCase("[8-bit] Test", 1, FieldSize.Byte)]
        [TestCase("[9-bit] Test", 2, FieldSize.Word)]
        [TestCase("bit", 1, FieldSize.None)]
        [TestCase("9bit", 2, FieldSize.Word)]
        [TestCase("-bit", 1, FieldSize.None)]

        [TestCase("[16-bit BE] Test", 2, FieldSize.BigEndianWord)]
        [TestCase("[24-bit BE] Test", 3, FieldSize.BigEndianTByte)]
        [TestCase("[32-bit BE] Test", 4, FieldSize.BigEndianDWord)]
        [TestCase("Test [32-bit BE]", 4, FieldSize.BigEndianDWord)]
        [TestCase("Test (32-bit BE)", 4, FieldSize.BigEndianDWord)]
        [TestCase("Test 32-bit BE", 4, FieldSize.BigEndianDWord)]
        [TestCase("[16-bit BigEndian] Test", 2, FieldSize.BigEndianWord)]
        [TestCase("[16-bit-BE] Test", 2, FieldSize.BigEndianWord)]
        [TestCase("[8-bit BE] Test", 1, FieldSize.Byte)]
        [TestCase("[4-bit BE] Test", 1, FieldSize.Byte)]

        [TestCase("8 BYTE Test", 8, FieldSize.Array)]
        [TestCase("Test 8 BYTE", 8, FieldSize.Array)]
        [TestCase("Test 8 BYT", 1, FieldSize.None)]
        [TestCase("[2 Byte] Test", 2, FieldSize.Word)]
        [TestCase("[4 Byte] Test", 4, FieldSize.DWord)]
        [TestCase("[4 Byte - Float] Test", 4, FieldSize.Float)]
        [TestCase("[8 Byte] Test", 8, FieldSize.Array)]
        [TestCase("[100 Bytes] Test", 100, FieldSize.Array)]
        [TestCase("[2 byte] Test", 2, FieldSize.Word)]
        [TestCase("[2-byte] Test", 2, FieldSize.Word)]
        [TestCase("Test (6 bytes)", 6, FieldSize.Array)]
        [TestCase("[2byte] Test", 2, FieldSize.Word)]

        [TestCase("[float] Test", 4, FieldSize.Float)]
        [TestCase("[float32] Test", 4, FieldSize.Float)]
        [TestCase("Test float", 4, FieldSize.Float)]
        [TestCase("Test floa", 1, FieldSize.None)]
        [TestCase("is floating", 1, FieldSize.None)]
        [TestCase("has floated", 1, FieldSize.None)]
        [TestCase("16-afloat", 1, FieldSize.None)]

        [TestCase("[MBF32] Test", 4, FieldSize.MBF32)]
        [TestCase("[MBF40] Test", 5, FieldSize.MBF32)]
        [TestCase("[MBF32 float] Test", 4, FieldSize.MBF32)]
        [TestCase("[MBF80] Test", 1, FieldSize.None)]
        [TestCase("[MBF-32] Test", 4, FieldSize.MBF32)]
        [TestCase("[32-bit MBF] Test", 4, FieldSize.MBF32)]
        [TestCase("[40-bit MBF] Test", 5, FieldSize.MBF32)]
        [TestCase("[MBF] Test", 1, FieldSize.None)]
        [TestCase("Test MBF32", 4, FieldSize.MBF32)]
        [TestCase("[MBF32LE] Test", 4, FieldSize.LittleEndianMBF32)]
        [TestCase("[MBF40LE] Test", 5, FieldSize.LittleEndianMBF32)]
        [TestCase("[MBF32-LE] Test", 4, FieldSize.LittleEndianMBF32)]
        [TestCase("[MBF40-LE] Test", 5, FieldSize.LittleEndianMBF32)]
        [TestCase("[MBF32 LE] Test", 4, FieldSize.LittleEndianMBF32)]
        [TestCase("[MBF40 LE] Test", 5, FieldSize.LittleEndianMBF32)]
        [TestCase("[MBF32 LittleEndian] Test", 4, FieldSize.LittleEndianMBF32)]
        [TestCase("[MBF40 LittleEndian] Test", 5, FieldSize.LittleEndianMBF32)]
        [TestCase("[floatBE] Test", 1, FieldSize.None)]
        [TestCase("[float BE] Test", 4, FieldSize.BigEndianFloat)]
        [TestCase("[float-BE] Test", 4, FieldSize.BigEndianFloat)]
        [TestCase("[float BigEndian] Test", 4, FieldSize.BigEndianFloat)]

        [TestCase("42=bitten", 1, FieldSize.None)]
        [TestCase("42-bitten", 1, FieldSize.None)]
        [TestCase("bit by bit", 1, FieldSize.None)]
        [TestCase("bit1=chest", 1, FieldSize.None)]

        [TestCase("Bite count (16-bit)", 2, FieldSize.Word)]
        [TestCase("Number of bits collected (32 bits)", 4, FieldSize.DWord)]

        [TestCase("100 32-bit pointers [400 bytes]", 400, FieldSize.Array)]
        [TestCase("[400 bytes] 100 32-bit pointers", 400, FieldSize.Array)]

        [TestCase("Bitflags (array of ~40 bytes)", 40, FieldSize.Array)]
        public void TestGetSizeFromNote(string note, int expectedLength, FieldSize expectedFieldSize)
        {
            var n = new CodeNote(1, note);
            Assert.That(n.Size, Is.EqualTo(expectedFieldSize));
            Assert.That(n.Length, Is.EqualTo(expectedLength));
        }

        [Test]
        public void TestPointerNote()
        {
            var n = new CodeNote(4,
                "Bomb Timer Pointer (24-bit)\r\n" +
                "+03 - [8-bit] Bombs Defused\r\n" +
                "+04 - Bomb Timer");

            Assert.That(n.Size, Is.EqualTo(FieldSize.TByte));
            Assert.That(n.Length, Is.EqualTo(3));
            Assert.That(n.Summary, Is.EqualTo("Bomb Timer Pointer"));
            Assert.That(n.IsPointer, Is.True);

            Assert.That(n.OffsetNotes.Count(), Is.EqualTo(2));

            var n2 = n.OffsetNotes.ElementAt(0);
            Assert.That(n2.Address, Is.EqualTo(3));
            Assert.That(n2.Size, Is.EqualTo(FieldSize.Byte));
            Assert.That(n2.Length, Is.EqualTo(1));
            Assert.That(n2.Summary, Is.EqualTo("Bombs Defused"));
            Assert.That(n2.IsPointer, Is.False);

            var n3 = n.OffsetNotes.ElementAt(1);
            Assert.That(n3.Address, Is.EqualTo(4));
            Assert.That(n3.Size, Is.EqualTo(FieldSize.None));
            Assert.That(n3.Length, Is.EqualTo(1));
            Assert.That(n3.Summary, Is.EqualTo("Bomb Timer"));
            Assert.That(n3.IsPointer, Is.False);
        }

        [Test]
        public void TestPointerNoteNestedMultiline()
        {
            var n = new CodeNote(4,
                "Pointer [32bit]\r\n" +
                "+0x428 | Obj1 pointer\r\n" +
                "++0x24C | [16-bit] State\r\n" +
                "-- Increments\r\n" +
                "+0x438 | Obj2 pointer\r\n" +
                "++0x08 | Flag\r\n" +
                "-- b0=quest1 complete\r\n" +
                "-- b1=quest2 complete\r\n" +
                "+0x448 | [32-bit BE] Not-nested number");

            Assert.That(n.Size, Is.EqualTo(FieldSize.DWord));
            Assert.That(n.Length, Is.EqualTo(4));
            Assert.That(n.Summary, Is.EqualTo("Pointer"));
            Assert.That(n.IsPointer, Is.True);

            Assert.That(n.OffsetNotes.Count(), Is.EqualTo(3));

            var n428 = n.OffsetNotes.ElementAt(0);
            Assert.That(n428.Address, Is.EqualTo(0x428));
            Assert.That(n428.Size, Is.EqualTo(FieldSize.DWord));
            Assert.That(n428.Length, Is.EqualTo(4));
            Assert.That(n428.Summary, Is.EqualTo("Obj1 pointer"));
            Assert.That(n428.IsPointer, Is.True);

            var n438 = n.OffsetNotes.ElementAt(1);
            Assert.That(n438.Address, Is.EqualTo(0x438));
            Assert.That(n438.Size, Is.EqualTo(FieldSize.DWord));
            Assert.That(n438.Length, Is.EqualTo(4));
            Assert.That(n438.Summary, Is.EqualTo("Obj2 pointer"));
            Assert.That(n438.IsPointer, Is.True);

            var n448 = n.OffsetNotes.ElementAt(2);
            Assert.That(n448.Address, Is.EqualTo(0x448));
            Assert.That(n448.Size, Is.EqualTo(FieldSize.BigEndianDWord));
            Assert.That(n448.Length, Is.EqualTo(4));
            Assert.That(n448.Summary, Is.EqualTo("Not-nested number"));
            Assert.That(n448.IsPointer, Is.False);

            Assert.That(n428.OffsetNotes.Count(), Is.EqualTo(1));
            var n24c = n428.OffsetNotes.First();
            Assert.That(n24c.Address, Is.EqualTo(0x24c));
            Assert.That(n24c.Size, Is.EqualTo(FieldSize.Word));
            Assert.That(n24c.Length, Is.EqualTo(2));
            Assert.That(n24c.Summary, Is.EqualTo("State"));
            Assert.That(n24c.Note, Is.EqualTo("[16-bit] State\r\n-- Increments"));
            Assert.That(n24c.IsPointer, Is.False);

            Assert.That(n438.OffsetNotes.Count(), Is.EqualTo(1));
            var n8 = n438.OffsetNotes.First();
            Assert.That(n8.Address, Is.EqualTo(8));
            Assert.That(n8.Size, Is.EqualTo(FieldSize.None));
            Assert.That(n8.Length, Is.EqualTo(1));
            Assert.That(n8.Summary, Is.EqualTo("Flag"));
            Assert.That(n8.Note, Is.EqualTo("Flag\r\n-- b0=quest1 complete\r\n-- b1=quest2 complete"));
            Assert.That(n8.IsPointer, Is.False);
        }

        [Test]
        public void TestGetSubNote()
        {
            var n = new CodeNote(4,
                "Item flags\r\n" +
                "b0: found\r\n" +
                "bit1 = collected\r\n" +
                "B2-3=color\r\n" +
                "b4 - b7 -> count\r\n");

            Assert.That(n.GetSubNote(FieldSize.Bit0), Is.EqualTo("found"));
            Assert.That(n.GetSubNote(FieldSize.Bit1), Is.EqualTo("collected"));
            Assert.That(n.GetSubNote(FieldSize.Bit2), Is.EqualTo("color"));
            Assert.That(n.GetSubNote(FieldSize.Bit3), Is.EqualTo("color"));
            Assert.That(n.GetSubNote(FieldSize.Bit4), Is.EqualTo("count"));
            Assert.That(n.GetSubNote(FieldSize.Bit5), Is.EqualTo("count"));
            Assert.That(n.GetSubNote(FieldSize.Bit6), Is.EqualTo("count"));
            Assert.That(n.GetSubNote(FieldSize.Bit7), Is.EqualTo("count"));
            Assert.That(n.GetSubNote(FieldSize.HighNibble), Is.EqualTo("count"));
            Assert.That(n.GetSubNote(FieldSize.LowNibble), Is.Null);
        }
    }
}
