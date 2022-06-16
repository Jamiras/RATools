using NUnit.Framework;
using RATools.Data;

namespace RATools.Tests.Data
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
        [TestCase("[64-bit] Test", 8, FieldSize.None)]
        [TestCase("[128-bit] Test", 16, FieldSize.None)]
        [TestCase("[17-bit] Test", 3, FieldSize.None)]
        [TestCase("[100-bit] Test", 13, FieldSize.None)]
        [TestCase("[0-bit] Test", 1, FieldSize.None)]
        [TestCase("[1-bit] Test", 1, FieldSize.None)]
        [TestCase("[4-bit] Test", 1, FieldSize.None)]
        [TestCase("[8-bit] Test", 1, FieldSize.Byte)]
        [TestCase("[9-bit] Test", 2, FieldSize.None)]
        [TestCase("bit", 1, FieldSize.None)]
        [TestCase("9bit", 2, FieldSize.None)]
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
        [TestCase("[4-bit BE] Test", 1, FieldSize.None)]

        [TestCase("8 BYTE Test", 8, FieldSize.None)]
        [TestCase("Test 8 BYTE", 8, FieldSize.None)]
        [TestCase("Test 8 BYT", 1, FieldSize.None)]
        [TestCase("[2 Byte] Test", 2, FieldSize.Word)]
        [TestCase("[4 Byte] Test", 4, FieldSize.DWord)]
        [TestCase("[4 Byte - Float] Test", 4, FieldSize.Float)]
        [TestCase("[8 Byte] Test", 8, FieldSize.None)]
        [TestCase("[100 Bytes] Test", 100, FieldSize.None)]
        [TestCase("[2 byte] Test", 2, FieldSize.Word)]
        [TestCase("[2-byte] Test", 2, FieldSize.Word)]
        [TestCase("Test (6 bytes)", 6, FieldSize.None)]
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
        [TestCase("[MBF-32] Test", 1, FieldSize.None)]
        [TestCase("[32-bit MBF] Test", 4, FieldSize.MBF32)]
        [TestCase("[40-bit MBF] Test", 5, FieldSize.MBF32)]
        [TestCase("[MBF] Test", 1, FieldSize.None)]
        [TestCase("Test MBF32", 4, FieldSize.MBF32)]

        [TestCase("42=bitten", 1, FieldSize.None)]
        [TestCase("42-bitten", 1, FieldSize.None)]
        [TestCase("bit by bit", 1, FieldSize.None)]
        [TestCase("bit1=chest", 1, FieldSize.None)]

        [TestCase("Bite count (16-bit)", 2, FieldSize.Word)]
        [TestCase("Number of bits collected (32 bits)", 4, FieldSize.DWord)]

        [TestCase("100 32-bit pointers [400 bytes]", 400, FieldSize.DWord)]
        [TestCase("[400 bytes] 100 32-bit pointers", 400, FieldSize.None)]
        public void TestGetSizeFromNote(string note, int expectedLength, FieldSize expectedFieldSize)
        {
            var n = new CodeNote(1, note);
            Assert.That(n.FieldSize, Is.EqualTo(expectedFieldSize));
            Assert.That(n.Length, Is.EqualTo(expectedLength));
        }
    }
}
