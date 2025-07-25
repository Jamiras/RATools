using Jamiras.Components;
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

        private static Field GetField(TestField field)
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
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, 1, "byte(0x001234) == 99 (1)")]
        [TestCase(RequirementType.None, TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, 2, "byte(0x001234) == 99 (2)")]
        [TestCase(RequirementType.ResetIf, TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, 0, "ResetIf byte(0x001234) == 99")]
        [TestCase(RequirementType.PauseIf, TestField.Byte1234, RequirementOperator.Equal, TestField.Value99, 0, "PauseIf byte(0x001234) == 99")]
        [TestCase(RequirementType.AddSource, TestField.Byte1234, RequirementOperator.None, TestField.None, 0, "AddSource byte(0x001234)")]
        [TestCase(RequirementType.SubSource, TestField.Byte1234, RequirementOperator.None, TestField.None, 0, "SubSource byte(0x001234)")]
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
        [TestCase(RequirementType.None, "")]
        [TestCase(RequirementType.ResetIf, "R:")]
        [TestCase(RequirementType.PauseIf, "P:")]
        [TestCase(RequirementType.AddSource, "A:")]
        [TestCase(RequirementType.SubSource, "B:")]
        [TestCase(RequirementType.AddHits, "C:")]
        [TestCase(RequirementType.SubHits, "D:")]
        [TestCase(RequirementType.AndNext, "N:")]
        [TestCase(RequirementType.OrNext, "O:")]
        [TestCase(RequirementType.Measured, "M:")]
        [TestCase(RequirementType.MeasuredPercent, "G:")]
        [TestCase(RequirementType.MeasuredIf, "Q:")]
        [TestCase(RequirementType.AddAddress, "I:")]
        [TestCase(RequirementType.ResetNextIf, "Z:")]
        [TestCase(RequirementType.Trigger, "T:")]
        [TestCase(RequirementType.Remember, "K:")]
        public void TestSerializeRequirementType(RequirementType type, string expectedPrefix)
        {
            var requirement = new Requirement
            {
                Type = type,
                Left = GetField(TestField.Byte1234),
                Operator = RequirementOperator.Equal,
                Right = GetField(TestField.Value99),
            };

            var builder = new StringBuilder();
            var context = new SerializationContext { AddressWidth = 4 };
            requirement.Serialize(builder, context);

            if (type.IsScalable())
                Assert.That(builder.ToString(), Is.EqualTo(expectedPrefix + "0xH1234=0"));
            else
                Assert.That(builder.ToString(), Is.EqualTo(expectedPrefix + "0xH1234=99"));
        }

        [Test]
        [TestCase(RequirementOperator.None, "0xH1234")]
        [TestCase(RequirementOperator.Equal, "0xH1234=99")]
        [TestCase(RequirementOperator.NotEqual, "0xH1234!=99")]
        [TestCase(RequirementOperator.LessThan, "0xH1234<99")]
        [TestCase(RequirementOperator.LessThanOrEqual, "0xH1234<=99")]
        [TestCase(RequirementOperator.GreaterThan, "0xH1234>99")]
        [TestCase(RequirementOperator.GreaterThanOrEqual, "0xH1234>=99")]
        [TestCase(RequirementOperator.Add, "0xH1234+99")]
        [TestCase(RequirementOperator.Subtract, "0xH1234-99")]
        [TestCase(RequirementOperator.Multiply, "0xH1234*99")]
        [TestCase(RequirementOperator.Divide, "0xH1234/99")]
        [TestCase(RequirementOperator.Modulus, "0xH1234%99")]
        [TestCase(RequirementOperator.BitwiseAnd, "0xH1234&99")]
        [TestCase(RequirementOperator.BitwiseXor, "0xH1234^99")]
        public void TestSerializeOperatorComparison(RequirementOperator requirementOperator, string expected)
        {
            var requirement = new Requirement
            {
                Left = GetField(TestField.Byte1234),
                Operator = requirementOperator,
                Right = GetField(TestField.Value99),
            };

            var builder = new StringBuilder();
            var context = new SerializationContext { AddressWidth = 4 };
            requirement.Serialize(builder, context);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(RequirementOperator.None, "A:0xH1234=0")]
        [TestCase(RequirementOperator.Equal, "A:0xH1234=0")]
        [TestCase(RequirementOperator.NotEqual, "A:0xH1234=0")]
        [TestCase(RequirementOperator.LessThan, "A:0xH1234=0")]
        [TestCase(RequirementOperator.LessThanOrEqual, "A:0xH1234=0")]
        [TestCase(RequirementOperator.GreaterThan, "A:0xH1234=0")]
        [TestCase(RequirementOperator.GreaterThanOrEqual, "A:0xH1234=0")]
        [TestCase(RequirementOperator.Add, "A:0xH1234+99")]
        [TestCase(RequirementOperator.Subtract, "A:0xH1234-99")]
        [TestCase(RequirementOperator.Multiply, "A:0xH1234*99")]
        [TestCase(RequirementOperator.Divide, "A:0xH1234/99")]
        [TestCase(RequirementOperator.Modulus, "A:0xH1234%99")]
        [TestCase(RequirementOperator.BitwiseAnd, "A:0xH1234&99")]
        [TestCase(RequirementOperator.BitwiseXor, "A:0xH1234^99")]
        public void TestSerializeOperatorScaler(RequirementOperator requirementOperator, string expected)
        {
            var requirement = new Requirement
            {
                Type = RequirementType.AddSource,
                Left = GetField(TestField.Byte1234),
                Operator = requirementOperator,
                Right = GetField(TestField.Value99),
            };

            var builder = new StringBuilder();
            var context = new SerializationContext { AddressWidth = 4 };
            requirement.Serialize(builder, context);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(0U, "0xH1234=99")]
        [TestCase(1U, "0xH1234=99.1.")]
        [TestCase(1000U, "0xH1234=99.1000.")]
        public void TestSerializeHitTarget(uint hitTarget, string expected)
        {
            var requirement = new Requirement
            {
                Left = GetField(TestField.Byte1234),
                Operator = RequirementOperator.Equal,
                Right = GetField(TestField.Value99),
                HitCount = hitTarget,
            };

            var builder = new StringBuilder();
            var context = new SerializationContext { AddressWidth = 4 };
            requirement.Serialize(builder, context);
            Assert.That(builder.ToString(), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(RequirementType.None, "")]
        [TestCase(RequirementType.ResetIf, "R:")]
        [TestCase(RequirementType.PauseIf, "P:")]
        [TestCase(RequirementType.AddSource, "A:")]
        [TestCase(RequirementType.SubSource, "B:")]
        [TestCase(RequirementType.AddHits, "C:")]
        [TestCase(RequirementType.SubHits, "D:")]
        [TestCase(RequirementType.AndNext, "N:")]
        [TestCase(RequirementType.OrNext, "O:")]
        [TestCase(RequirementType.Measured, "M:")]
        [TestCase(RequirementType.MeasuredPercent, "G:")]
        [TestCase(RequirementType.MeasuredIf, "Q:")]
        [TestCase(RequirementType.AddAddress, "I:")]
        [TestCase(RequirementType.ResetNextIf, "Z:")]
        [TestCase(RequirementType.Trigger, "T:")]
        [TestCase(RequirementType.Remember, "K:")]
        public void TestDeserializeRequirementType(RequirementType type, string prefix)
        {
            var requirement = Requirement.Deserialize(Tokenizer.CreateTokenizer(prefix + "0xH1234=99"));

            Assert.That(requirement.Type, Is.EqualTo(type));
            Assert.That(requirement.Left, Is.EqualTo(GetField(TestField.Byte1234)));

            if (type.IsScalable())
            {
                Assert.That(requirement.Operator, Is.EqualTo(RequirementOperator.None));
                Assert.That(requirement.Right, Is.EqualTo(GetField(TestField.None)));
            }
            else
            {
                Assert.That(requirement.Operator, Is.EqualTo(RequirementOperator.Equal));
                Assert.That(requirement.Right, Is.EqualTo(GetField(TestField.Value99)));
            }

            Assert.That(requirement.HitCount, Is.EqualTo(0));
        }

        [Test]
        [TestCase(RequirementOperator.None, "0xH1234")]
        [TestCase(RequirementOperator.Equal, "0xH1234=99")]
        [TestCase(RequirementOperator.NotEqual, "0xH1234!=99")]
        [TestCase(RequirementOperator.LessThan, "0xH1234<99")]
        [TestCase(RequirementOperator.LessThanOrEqual, "0xH1234<=99")]
        [TestCase(RequirementOperator.GreaterThan, "0xH1234>99")]
        [TestCase(RequirementOperator.GreaterThanOrEqual, "0xH1234>=99")]
        [TestCase(RequirementOperator.Add, "0xH1234+99")]
        [TestCase(RequirementOperator.Subtract, "0xH1234-99")]
        [TestCase(RequirementOperator.Multiply, "0xH1234*99")]
        [TestCase(RequirementOperator.Divide, "0xH1234/99")]
        [TestCase(RequirementOperator.Modulus, "0xH1234%99")]
        [TestCase(RequirementOperator.BitwiseAnd, "0xH1234&99")]
        [TestCase(RequirementOperator.BitwiseXor, "0xH1234^99")]
        public void TestDeserializeOperatorComparison(RequirementOperator requirementOperator, string input)
        {
            var requirement = Requirement.Deserialize(Tokenizer.CreateTokenizer(input));

            Assert.That(requirement.Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirement.Left, Is.EqualTo(GetField(TestField.Byte1234)));
            Assert.That(requirement.Operator, Is.EqualTo(requirementOperator));

            if (requirementOperator != RequirementOperator.None)
                Assert.That(requirement.Right, Is.EqualTo(GetField(TestField.Value99)));
            else
                Assert.That(requirement.Right, Is.EqualTo(GetField(TestField.None)));

            Assert.That(requirement.HitCount, Is.EqualTo(0));
        }

        [Test]
        [TestCase(0U, "0xH1234=99")]
        [TestCase(1U, "0xH1234=99.1.")]
        [TestCase(1000U, "0xH1234=99.1000.")]
        public void TestDeserializeHitTarget(uint hitTarget, string input)
        {
            var requirement = Requirement.Deserialize(Tokenizer.CreateTokenizer(input));

            Assert.That(requirement.Type, Is.EqualTo(RequirementType.None));
            Assert.That(requirement.Left, Is.EqualTo(GetField(TestField.Byte1234)));
            Assert.That(requirement.Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirement.Right, Is.EqualTo(GetField(TestField.Value99)));
            Assert.That(requirement.HitCount, Is.EqualTo(hitTarget));
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
        [TestCase(TestField.Byte1234, RequirementOperator.Modulus, TestField.Value99, null)]
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
