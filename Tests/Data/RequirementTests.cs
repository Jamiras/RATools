using NUnit.Framework;
using System.Text;

namespace RATools.Data.Tests
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
            Value66,
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
                case TestField.Value66:
                    return new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 66 };
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
                HitCount = (uint)hitCount
            };

            Assert.That(requirement.ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void TestToStringEmpty()
        {
            var field = new Requirement();
            Assert.That(field.ToString(), Is.EqualTo("none"));
        }

        [Test]
        [TestCase(TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, null)]
        [TestCase(TestField.Byte1234, RequirementOperator.NotEqual, TestField.Value99, null)]
        [TestCase(TestField.Byte1234, RequirementOperator.LessThan, TestField.Value99, null)]
        [TestCase(TestField.Byte1234, RequirementOperator.LessThanOrEqual, TestField.Value99, null)]
        [TestCase(TestField.Byte1234, RequirementOperator.GreaterThan, TestField.Value99, null)]
        [TestCase(TestField.Byte1234, RequirementOperator.GreaterThanOrEqual, TestField.Value99, null)]
        [TestCase(TestField.Byte1234, RequirementOperator.Equal, TestField.Byte2345, null)]
        [TestCase(TestField.Byte2345, RequirementOperator.Equal, TestField.Word2345, null)]
        [TestCase(TestField.Byte1234, RequirementOperator.Equal, TestField.Byte1234, true)]
        [TestCase(TestField.Byte1234, RequirementOperator.NotEqual, TestField.Byte2345, null)]
        [TestCase(TestField.Byte2345, RequirementOperator.NotEqual, TestField.Word2345, null)]
        [TestCase(TestField.Byte1234, RequirementOperator.NotEqual, TestField.Byte1234, false)]
        [TestCase(TestField.Byte1234, RequirementOperator.NotEqual, TestField.Byte1234, false)]
        [TestCase(TestField.Byte1234, RequirementOperator.LessThan, TestField.Byte1234, false)]
        [TestCase(TestField.Byte1234, RequirementOperator.LessThanOrEqual, TestField.Byte1234, true)]
        [TestCase(TestField.Byte1234, RequirementOperator.GreaterThan, TestField.Byte1234, false)]
        [TestCase(TestField.Byte1234, RequirementOperator.GreaterThanOrEqual, TestField.Byte1234, true)]
        [TestCase(TestField.Value66, RequirementOperator.Equal, TestField.Value99, false)]
        [TestCase(TestField.Value66, RequirementOperator.NotEqual, TestField.Value99, true)]
        [TestCase(TestField.Value66, RequirementOperator.LessThan, TestField.Value99, true)]
        [TestCase(TestField.Value66, RequirementOperator.LessThanOrEqual, TestField.Value99, true)]
        [TestCase(TestField.Value66, RequirementOperator.GreaterThan, TestField.Value99, false)]
        [TestCase(TestField.Value66, RequirementOperator.GreaterThanOrEqual, TestField.Value99, false)]
        [TestCase(TestField.Value66, RequirementOperator.Equal, TestField.Value66, true)]
        [TestCase(TestField.Value66, RequirementOperator.NotEqual, TestField.Value66, false)]
        [TestCase(TestField.Byte1234, RequirementOperator.Multiply, TestField.Value99, null)]
        [TestCase(TestField.Byte1234, RequirementOperator.Multiply, TestField.Byte1234, null)]
        [TestCase(TestField.Byte1234, RequirementOperator.Divide, TestField.Value99, null)]
        [TestCase(TestField.Byte1234, RequirementOperator.BitwiseAnd, TestField.Value99, null)]
        [TestCase(TestField.Value99, RequirementOperator.None, TestField.None, null)]
        public void TestEvaluate(TestField left, RequirementOperator requirementOperator, TestField right, bool? expected)
        {
            var requirement = new Requirement
            {
                Left = GetField(left),
                Operator = requirementOperator,
                Right = GetField(right),
            };

            Assert.That(requirement.Evaluate(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(TestField.Byte1234, RequirementOperator.Equal, TestField.Byte1234, 0, true)]
        [TestCase(TestField.Byte1234, RequirementOperator.Equal, TestField.Byte1234, 1, true)]
        [TestCase(TestField.Byte1234, RequirementOperator.Equal, TestField.Byte1234, 2, null)]
        [TestCase(TestField.Byte1234, RequirementOperator.NotEqual, TestField.Byte1234, 0, false)]
        [TestCase(TestField.Byte1234, RequirementOperator.NotEqual, TestField.Byte1234, 1, false)]
        [TestCase(TestField.Byte1234, RequirementOperator.NotEqual, TestField.Byte1234, 2, false)]
        public void TestEvaluateHitCount(TestField left, RequirementOperator requirementOperator, TestField right, int hitTarget, bool? expected)
        {
            var requirement = new Requirement
            {
                Left = GetField(left),
                Operator = requirementOperator,
                Right = GetField(right),
                HitCount = (uint)hitTarget,
            };

            Assert.That(requirement.Evaluate(), Is.EqualTo(expected));
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
                    case RequirementType.MeasuredPercent:
                    case RequirementType.MeasuredIf:
                    case RequirementType.PauseIf:
                    case RequirementType.ResetIf:
                    case RequirementType.Trigger:
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
