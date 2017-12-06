using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using System;
using System.Text;

namespace RATools.Tests.Data
{
    [TestFixture]
    class FieldTests
    {
        [Test]
        [TestCase(FieldSize.Bit0, FieldType.MemoryAddress, 0x1234, "bit0(0x001234)")]
        [TestCase(FieldSize.Bit1, FieldType.MemoryAddress, 0x1234, "bit1(0x001234)")]
        [TestCase(FieldSize.Bit2, FieldType.MemoryAddress, 0x1234, "bit2(0x001234)")]
        [TestCase(FieldSize.Bit3, FieldType.MemoryAddress, 0x1234, "bit3(0x001234)")]
        [TestCase(FieldSize.Bit4, FieldType.MemoryAddress, 0x1234, "bit4(0x001234)")]
        [TestCase(FieldSize.Bit5, FieldType.MemoryAddress, 0x1234, "bit5(0x001234)")]
        [TestCase(FieldSize.Bit6, FieldType.MemoryAddress, 0x1234, "bit6(0x001234)")]
        [TestCase(FieldSize.Bit7, FieldType.MemoryAddress, 0x1234, "bit7(0x001234)")]
        [TestCase(FieldSize.LowNibble, FieldType.MemoryAddress, 0x1234, "low4(0x001234)")]
        [TestCase(FieldSize.HighNibble, FieldType.MemoryAddress, 0x1234, "high4(0x001234)")]
        [TestCase(FieldSize.Byte, FieldType.MemoryAddress, 0x1234, "byte(0x001234)")]
        [TestCase(FieldSize.Word, FieldType.MemoryAddress, 0x1234, "word(0x001234)")]
        [TestCase(FieldSize.DWord, FieldType.MemoryAddress, 0x1234, "dword(0x001234)")]
        [TestCase(FieldSize.Byte, FieldType.PreviousValue, 0x1234, "prev(byte(0x001234))")]
        [TestCase(FieldSize.Word, FieldType.Value, 0x1234, "4660")]
        [TestCase(FieldSize.Byte, FieldType.MemoryAddress, 0x123456, "byte(0x123456)")]
        public void TestToString(FieldSize fieldSize, FieldType fieldType, int value, string expected)
        {
            var field = new Field { Size = fieldSize, Type = fieldType, Value = (uint)value };
            Assert.That(field.ToString(), Is.EqualTo(expected));
        }

        [TestCase(FieldSize.Bit0, 0, "0x0")]
        [TestCase(FieldSize.Bit0, 1, "0x1")]
        [TestCase(FieldSize.Bit1, 1, "0x1")]
        [TestCase(FieldSize.Bit2, 1, "0x1")]
        [TestCase(FieldSize.Bit3, 1, "0x1")]
        [TestCase(FieldSize.Bit4, 1, "0x1")]
        [TestCase(FieldSize.Bit5, 1, "0x1")]
        [TestCase(FieldSize.Bit6, 1, "0x1")]
        [TestCase(FieldSize.Bit7, 1, "0x1")]
        [TestCase(FieldSize.LowNibble, 0, "0x0")]
        [TestCase(FieldSize.LowNibble, 15, "0xF")]
        [TestCase(FieldSize.HighNibble, 0, "0x0")]
        [TestCase(FieldSize.HighNibble, 15, "0xF")]
        [TestCase(FieldSize.Byte, 0, "0x00")]
        [TestCase(FieldSize.Byte, 255, "0xFF")]
        [TestCase(FieldSize.Word, 0, "0x0000")]
        [TestCase(FieldSize.Word, 65535, "0xFFFF")]
        [TestCase(FieldSize.DWord, 0, "0x00000000")]
        [TestCase(FieldSize.DWord, Int32.MaxValue, "0x7FFFFFFF")]
        public void TestAppendStringHex(FieldSize fieldSize, int value, string expected)
        {
            var field = new Field { Size = fieldSize, Type = FieldType.Value, Value = (uint)value };
            var builder = new StringBuilder();
            field.AppendString(builder, NumberFormat.Hexadecimal);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void TestToStringEmpty()
        {
            var field = new Field();
            Assert.That(field.ToString(), Is.EqualTo("none"));
        }

        [Test]
        [TestCase(FieldSize.Bit0, FieldType.MemoryAddress, 0x1234, "0xM001234")]
        [TestCase(FieldSize.Bit1, FieldType.MemoryAddress, 0x1234, "0xN001234")]
        [TestCase(FieldSize.Bit2, FieldType.MemoryAddress, 0x1234, "0xO001234")]
        [TestCase(FieldSize.Bit3, FieldType.MemoryAddress, 0x1234, "0xP001234")]
        [TestCase(FieldSize.Bit4, FieldType.MemoryAddress, 0x1234, "0xQ001234")]
        [TestCase(FieldSize.Bit5, FieldType.MemoryAddress, 0x1234, "0xR001234")]
        [TestCase(FieldSize.Bit6, FieldType.MemoryAddress, 0x1234, "0xS001234")]
        [TestCase(FieldSize.Bit7, FieldType.MemoryAddress, 0x1234, "0xT001234")]
        [TestCase(FieldSize.LowNibble, FieldType.MemoryAddress, 0x1234, "0xL001234")]
        [TestCase(FieldSize.HighNibble, FieldType.MemoryAddress, 0x1234, "0xU001234")]
        [TestCase(FieldSize.Byte, FieldType.MemoryAddress, 0x1234, "0xH001234")]
        [TestCase(FieldSize.Word, FieldType.MemoryAddress, 0x1234, "0x 001234")]
        [TestCase(FieldSize.DWord, FieldType.MemoryAddress, 0x1234, "0xX001234")]
        [TestCase(FieldSize.Byte, FieldType.PreviousValue, 0x1234, "d0xH001234")]
        [TestCase(FieldSize.Word, FieldType.Value, 0x1234, "4660")]
        [TestCase(FieldSize.Byte, FieldType.MemoryAddress, 0x123456, "0xH123456")]
        public void TestSerialize(FieldSize fieldSize, FieldType fieldType, int value, string expected)
        {
            var field = new Field { Size = fieldSize, Type = fieldType, Value = (uint)value };
            var builder = new StringBuilder();
            field.Serialize(builder);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase("0xM001234", FieldSize.Bit0, FieldType.MemoryAddress, 0x1234)]
        [TestCase("0xN001234", FieldSize.Bit1, FieldType.MemoryAddress, 0x1234)]
        [TestCase("0xO001234", FieldSize.Bit2, FieldType.MemoryAddress, 0x1234)]
        [TestCase("0xP001234", FieldSize.Bit3, FieldType.MemoryAddress, 0x1234)]
        [TestCase("0xQ001234", FieldSize.Bit4, FieldType.MemoryAddress, 0x1234)]
        [TestCase("0xR001234", FieldSize.Bit5, FieldType.MemoryAddress, 0x1234)]
        [TestCase("0xS001234", FieldSize.Bit6, FieldType.MemoryAddress, 0x1234)]
        [TestCase("0xT001234", FieldSize.Bit7, FieldType.MemoryAddress, 0x1234)]
        [TestCase("0xL001234", FieldSize.LowNibble, FieldType.MemoryAddress, 0x1234)]
        [TestCase("0xU001234", FieldSize.HighNibble, FieldType.MemoryAddress, 0x1234)]
        [TestCase("0xH001234", FieldSize.Byte, FieldType.MemoryAddress, 0x1234)]
        [TestCase("0x 001234", FieldSize.Word, FieldType.MemoryAddress, 0x1234)]
        [TestCase("0xX001234", FieldSize.DWord, FieldType.MemoryAddress, 0x1234)]
        [TestCase("d0xH001234", FieldSize.Byte, FieldType.PreviousValue, 0x1234)]
        [TestCase("4660", FieldSize.None, FieldType.Value, 0x1234)]
        [TestCase("0xH123456", FieldSize.Byte, FieldType.MemoryAddress, 0x123456)]
        public void TestDeserialize(string serialized, FieldSize fieldSize, FieldType fieldType, int value)
        {
            var field = Field.Deserialize(Tokenizer.CreateTokenizer(serialized));
            Assert.That(field, Is.Not.Null);
            Assert.That(field.Size, Is.EqualTo(fieldSize));
            Assert.That(field.Type, Is.EqualTo(fieldType));
            Assert.That(field.Value, Is.EqualTo(value));
        }

        [Test]
        public void TestEquals()
        {
            var field1 = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x1234 };
            var field2 = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x1234 };

            Assert.That(field1, Is.EqualTo(field2));
            Assert.That(field1 == field2);
            Assert.That(field1.Equals(field2));
        }

        [Test]
        public void TestEqualsDifferentSize()
        {
            var field1 = new Field { Size = FieldSize.Bit1, Type = FieldType.MemoryAddress, Value = 0x1234 };
            var field2 = new Field { Size = FieldSize.Bit2, Type = FieldType.MemoryAddress, Value = 0x1234 };

            Assert.That(field1, Is.Not.EqualTo(field2));
            Assert.That(field1 != field2);
            Assert.That(!field1.Equals(field2));
        }

        [Test]
        public void TestEqualsDifferentType()
        {
            var field1 = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x1234 };
            var field2 = new Field { Size = FieldSize.Byte, Type = FieldType.PreviousValue, Value = 0x1234 };

            Assert.That(field1, Is.Not.EqualTo(field2));
            Assert.That(field1 != field2);
            Assert.That(!field1.Equals(field2));
        }

        [Test]
        public void TestEqualsDifferentAddress()
        {
            var field1 = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x1234 };
            var field2 = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x1235 };

            Assert.That(field1, Is.Not.EqualTo(field2));
            Assert.That(field1 != field2);
            Assert.That(!field1.Equals(field2));
        }

        [Test]
        public void TestEqualsDifferentValue()
        {
            var field1 = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 0x1234 };
            var field2 = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 0x1235 };

            Assert.That(field1, Is.Not.EqualTo(field2));
            Assert.That(field1 != field2);
            Assert.That(!field1.Equals(field2));
        }
    }
}
