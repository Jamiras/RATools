using NUnit.Framework;
using RATools.Data;
using System.Text;

namespace RATools.Tests.Data
{
    [TestFixture]
    class RequirementTests
    {
        public enum TestField
        {
            None,
            Byte1234,
            Byte2345,
            Word2345,
            Value99,
        }

        private Field GetField(TestField field)
        {
            switch (field)
            {
                case TestField.Byte1234:
                    return new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x1234 };
                case TestField.Byte2345:
                    return new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x2345 };
                case TestField.Word2345:
                    return new Field { Size = FieldSize.Word, Type = FieldType.MemoryAddress, Value = 0x2345 };
                case TestField.Value99:
                    return new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 99 };
                default:
                    return new Field();
            }
        }

        [Test]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, 0, "byte(0x001234) == 99")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.NotEqual, TestField.Value99, 0, "byte(0x001234) != 99")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.LessThan, TestField.Value99, 0, "byte(0x001234) < 99")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.LessThanOrEqual, TestField.Value99, 0, "byte(0x001234) <= 99")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.GreaterThan, TestField.Value99, 0, "byte(0x001234) > 99")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.GreaterThanOrEqual, TestField.Value99, 0, "byte(0x001234) >= 99")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.Equal, TestField.Byte2345, 0, "byte(0x001234) == byte(0x002345)")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, 1, "once(byte(0x001234) == 99)")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, 2, "repeated(2, byte(0x001234) == 99)")]
        [TestCase(RequirementType.ResetIf, TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, 0, "never(byte(0x001234) == 99)")]
        [TestCase(RequirementType.PauseIf, TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, 0, "unless(byte(0x001234) == 99)")]
        [TestCase(RequirementType.AddSource, TestField.Byte1234, RequirementOperator.None, TestField.None, 0, "byte(0x001234) + ")]
        [TestCase(RequirementType.SubSource, TestField.Byte1234, RequirementOperator.None, TestField.None, 0, " - byte(0x001234)")]
        public void TestToString(RequirementType requirementType, TestField left, RequirementOperator requirementOperator, TestField right, int hitCount, string expected)
        {
            var requirement = new Requirement
            {
                Type = requirementType,
                Left = GetField(left),
                Operator = requirementOperator,
                Right = GetField(right),
                HitCount = (ushort)hitCount
            };

            Assert.That(requirement.ToString(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, 0, "byte(0x001234) == 0x63")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.NotEqual, TestField.Value99, 0, "byte(0x001234) != 0x63")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.LessThan, TestField.Value99, 0, "byte(0x001234) < 0x63")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.LessThanOrEqual, TestField.Value99, 0, "byte(0x001234) <= 0x63")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.GreaterThan, TestField.Value99, 0, "byte(0x001234) > 0x63")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.GreaterThanOrEqual, TestField.Value99, 0, "byte(0x001234) >= 0x63")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.Equal, TestField.Byte2345, 0, "byte(0x001234) == byte(0x002345)")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, 1, "once(byte(0x001234) == 0x63)")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, 2, "repeated(2, byte(0x001234) == 0x63)")]
        [TestCase(RequirementType.ResetIf, TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, 0, "never(byte(0x001234) == 0x63)")]
        [TestCase(RequirementType.PauseIf, TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, 0, "unless(byte(0x001234) == 0x63)")]
        [TestCase(RequirementType.AddSource, TestField.Byte1234, RequirementOperator.None, TestField.None, 0, "byte(0x001234) + ")]
        [TestCase(RequirementType.SubSource, TestField.Byte1234, RequirementOperator.None, TestField.None, 0, " - byte(0x001234)")]
        public void TestAppendStringHex(RequirementType requirementType, TestField left, RequirementOperator requirementOperator, TestField right, int hitCount, string expected)
        {
            var requirement = new Requirement
            {
                Type = requirementType,
                Left = GetField(left),
                Operator = requirementOperator,
                Right = GetField(right),
                HitCount = (ushort)hitCount
            };

            var builder = new StringBuilder();
            requirement.AppendString(builder, NumberFormat.Hexadecimal);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void TestToStringEmpty()
        {
            var field = new Requirement();
            Assert.That(field.ToString(), Is.EqualTo("none"));
        }

        [Test]
        public void TestEquals()
        {
            var requirement1 = new Requirement { Left = GetField(TestField.Byte1234), Operator = RequirementOperator.Equal, Right = GetField(TestField.Value99) };
            var requirement2 = new Requirement { Left = GetField(TestField.Byte1234), Operator = RequirementOperator.Equal, Right = GetField(TestField.Value99) };

            Assert.That(requirement1, Is.EqualTo(requirement2));
            Assert.That(requirement1 == requirement2);
            Assert.That(requirement1.Equals(requirement2));
        }

        [Test]
        public void TestEqualsDifferentLeft()
        {
            var requirement1 = new Requirement { Left = GetField(TestField.Byte1234), Operator = RequirementOperator.Equal, Right = GetField(TestField.Value99) };
            var requirement2 = new Requirement { Left = GetField(TestField.Byte2345), Operator = RequirementOperator.Equal, Right = GetField(TestField.Value99) };

            Assert.That(requirement1, Is.Not.EqualTo(requirement2));
            Assert.That(requirement1 != requirement2);
            Assert.That(!requirement1.Equals(requirement2));
        }

        [Test]
        public void TestEqualsDifferentRight()
        {
            var requirement1 = new Requirement { Left = GetField(TestField.Byte1234), Operator = RequirementOperator.Equal, Right = GetField(TestField.Value99) };
            var requirement2 = new Requirement { Left = GetField(TestField.Byte1234), Operator = RequirementOperator.Equal, Right = GetField(TestField.Byte2345) };

            Assert.That(requirement1, Is.Not.EqualTo(requirement2));
            Assert.That(requirement1 != requirement2);
            Assert.That(!requirement1.Equals(requirement2));
        }

        [Test]
        public void TestEqualsDifferentOperator()
        {
            var requirement1 = new Requirement { Left = GetField(TestField.Byte1234), Operator = RequirementOperator.Equal, Right = GetField(TestField.Value99) };
            var requirement2 = new Requirement { Left = GetField(TestField.Byte1234), Operator = RequirementOperator.GreaterThanOrEqual, Right = GetField(TestField.Value99) };

            Assert.That(requirement1, Is.Not.EqualTo(requirement2));
            Assert.That(requirement1 != requirement2);
            Assert.That(!requirement1.Equals(requirement2));
        }

        [Test]
        public void TestEqualsDifferentType()
        {
            var requirement1 = new Requirement { Left = GetField(TestField.Byte1234), Operator = RequirementOperator.Equal, Right = GetField(TestField.Value99) };
            var requirement2 = new Requirement { Left = GetField(TestField.Byte1234), Operator = RequirementOperator.Equal, Right = GetField(TestField.Value99), Type = RequirementType.PauseIf };

            Assert.That(requirement1, Is.Not.EqualTo(requirement2));
            Assert.That(requirement1 != requirement2);
            Assert.That(!requirement1.Equals(requirement2));
        }

        [Test]
        public void TestEqualsDifferentHitCount()
        {
            var requirement1 = new Requirement { Left = GetField(TestField.Byte1234), Operator = RequirementOperator.Equal, Right = GetField(TestField.Value99) };
            var requirement2 = new Requirement { Left = GetField(TestField.Byte1234), Operator = RequirementOperator.Equal, Right = GetField(TestField.Value99), HitCount = 1 };

            Assert.That(requirement1, Is.Not.EqualTo(requirement2));
            Assert.That(requirement1 != requirement2);
            Assert.That(!requirement1.Equals(requirement2));
        }

        [Test]
        public void TestIsCombining()
        {
            foreach (RequirementType e in System.Enum.GetValues(typeof(RequirementType)))
            {
                var requirement = new Requirement { Type = e };

                // validate non-combining to invert logic and catch new items
                switch (e)
                {
                    case RequirementType.None:
                    case RequirementType.Measured:
                    case RequirementType.MeasuredIf:
                    case RequirementType.PauseIf:
                    case RequirementType.ResetIf:
                        Assert.IsFalse(requirement.IsCombining, e.ToString());
                        break;

                    default:
                        Assert.IsTrue(requirement.IsCombining, e.ToString());
                        break;
                }
            }
        }
    }
}
