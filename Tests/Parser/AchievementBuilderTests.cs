using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Test.Parser
{
    [TestFixture]
    class AchievementBuilderTests
    {
        [Test]
        public void TestDefaultConstructor()
        {
            var builder = new AchievementBuilder();
            Assert.That(builder.Title, Is.EqualTo(""));
            Assert.That(builder.Description, Is.EqualTo(""));
            Assert.That(builder.Points, Is.EqualTo(0));
            Assert.That(builder.Id, Is.EqualTo(0));
            Assert.That(builder.BadgeName, Is.EqualTo(""));
            Assert.That(builder.CoreRequirements.Count, Is.EqualTo(0));
            Assert.That(builder.AlternateRequirements.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestAchievementConstructor()
        {
            var address1 = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x1234 };
            var address2 = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x4567 };
            var address3 = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x8901 };
            var value1 = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 69 };
            var value2 = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 0 };
            var value3 = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 4 };
            var value4 = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 5 };

            var achievement = new Achievement();
            achievement.Title = "Title";
            achievement.Description = "Description";
            achievement.Points = 5;
            achievement.Id = 12345;
            achievement.BadgeName = "Badge";
            achievement.CoreRequirements = new Requirement[]
            {
                new Requirement { Left = address1, Operator = RequirementOperator.Equal, Right = value1 },
                new Requirement { Left = address2, Operator = RequirementOperator.Equal, Right = value2 },
            };
            achievement.AlternateRequirements = new Requirement[][]
            {
                new Requirement[]
                {
                    new Requirement { Left = address3, Operator = RequirementOperator.Equal, Right = value3 },
                },
                new Requirement[]
                {
                    new Requirement { Left = address3, Operator = RequirementOperator.Equal, Right = value4 },
                }
            };

            var builder = new AchievementBuilder(achievement);
            Assert.That(builder.Title, Is.EqualTo("Title"));
            Assert.That(builder.Description, Is.EqualTo("Description"));
            Assert.That(builder.Points, Is.EqualTo(5));
            Assert.That(builder.Id, Is.EqualTo(12345));
            Assert.That(builder.BadgeName, Is.EqualTo("Badge"));
            Assert.That(builder.CoreRequirements.Count, Is.EqualTo(2));
            Assert.That(builder.AlternateRequirements.Count, Is.EqualTo(2));
            Assert.That(builder.AlternateRequirements.First().Count, Is.EqualTo(1));
            Assert.That(builder.AlternateRequirements.Last().Count, Is.EqualTo(1));

            var requirement = builder.CoreRequirements.First();
            Assert.That(requirement.Left, Is.EqualTo(address1));
            Assert.That(requirement.Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirement.Right, Is.EqualTo(value1));

            requirement = builder.CoreRequirements.Last();
            Assert.That(requirement.Left, Is.EqualTo(address2));
            Assert.That(requirement.Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirement.Right, Is.EqualTo(value2));

            requirement = builder.AlternateRequirements.First().First();
            Assert.That(requirement.Left, Is.EqualTo(address3));
            Assert.That(requirement.Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirement.Right, Is.EqualTo(value3));

            requirement = builder.AlternateRequirements.Last().First();
            Assert.That(requirement.Left, Is.EqualTo(address3));
            Assert.That(requirement.Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirement.Right, Is.EqualTo(value4));
        }

        [Test]
        public void TestToAchievement()
        {
            var address1 = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x1234 };
            var address2 = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x4567 };
            var address3 = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x8901 };
            var value1 = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 69 };
            var value2 = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 0 };
            var value3 = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 4 };
            var value4 = new Field { Size = FieldSize.Byte, Type = FieldType.Value, Value = 5 };

            var builder = new AchievementBuilder();
            builder.Title = "Title";
            builder.Description = "Description";
            builder.Points = 5;
            builder.Id = 12345;
            builder.BadgeName = "Badge";

            builder.CoreRequirements.Add(new Requirement { Left = address1, Operator = RequirementOperator.Equal, Right = value1 });
            builder.CoreRequirements.Add(new Requirement { Left = address2, Operator = RequirementOperator.Equal, Right = value2 });
            builder.AlternateRequirements.Add(new [] { new Requirement { Left = address3, Operator = RequirementOperator.Equal, Right = value3 } });
            builder.AlternateRequirements.Add(new [] { new Requirement { Left = address3, Operator = RequirementOperator.Equal, Right = value4 } });

            var achievement = builder.ToAchievement();

            Assert.That(achievement.Title, Is.EqualTo("Title"));
            Assert.That(achievement.Description, Is.EqualTo("Description"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(achievement.Id, Is.EqualTo(12345));
            Assert.That(achievement.BadgeName, Is.EqualTo("Badge"));
            Assert.That(achievement.CoreRequirements.Count(), Is.EqualTo(2));
            Assert.That(achievement.AlternateRequirements.Count(), Is.EqualTo(2));
            Assert.That(achievement.AlternateRequirements.First().Count(), Is.EqualTo(1));
            Assert.That(achievement.AlternateRequirements.Last().Count(), Is.EqualTo(1));

            var requirement = achievement.CoreRequirements.First();
            Assert.That(requirement.Left, Is.EqualTo(address1));
            Assert.That(requirement.Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirement.Right, Is.EqualTo(value1));

            requirement = achievement.CoreRequirements.Last();
            Assert.That(requirement.Left, Is.EqualTo(address2));
            Assert.That(requirement.Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirement.Right, Is.EqualTo(value2));

            requirement = achievement.AlternateRequirements.First().First();
            Assert.That(requirement.Left, Is.EqualTo(address3));
            Assert.That(requirement.Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirement.Right, Is.EqualTo(value3));

            requirement = achievement.AlternateRequirements.Last().First();
            Assert.That(requirement.Left, Is.EqualTo(address3));
            Assert.That(requirement.Operator, Is.EqualTo(RequirementOperator.Equal));
            Assert.That(requirement.Right, Is.EqualTo(value4));
        }

        private static AchievementBuilder CreateAchievement(string input)
        {
            // NOTE: these are integration tests as they rely on ExpressionBase.Parse and 
            // AchievementScriptInterpreter.ScriptInterpreterAchievementBuilder, but using string 
            // inputs /output makes reading the tests and validating the behavior easier for humans.
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expression = ExpressionBase.Parse(tokenizer);

            var achievement = new ScriptInterpreterAchievementBuilder();
            var error = achievement.PopulateFromExpression(expression);
            Assert.That(error, Is.Null.Or.Empty);

            return achievement;
        }

        [Test]
        [TestCase("0xS00627e=1", "bit6(0x00627E) == 1")]
        [TestCase("d0xS00627e=0", "prev(bit6(0x00627E)) == 0")]
        [TestCase("0xS00627e=1_d0xS00627e=0", "bit6(0x00627E) == 1 && prev(bit6(0x00627E)) == 0")]
        [TestCase("0xH000028=3", "byte(0x000028) == 3")]
        [TestCase("0xU00616a=8", "high4(0x00616A) == 8")]
        [TestCase("0x 000042=5786", "word(0x000042) == 5786")]
        [TestCase("0x1234=5786", "word(0x001234) == 5786")]
        [TestCase("0xfedc=5786", "word(0x00FEDC) == 5786")]
        [TestCase("0xH000028=12_0x 000042=25959S0xH0062af!=0S0x 0062ad>=10000", "byte(0x000028) == 12 && word(0x000042) == 25959 && (byte(0x0062AF) != 0 || word(0x0062AD) >= 10000)")]
        [TestCase("0xH000440=140.1.", "once(byte(0x000440) == 140)")]
        [TestCase("0xH000440=140(1)", "once(byte(0x000440) == 140)")] // old format
        [TestCase("R:0xH000440=0", "never(byte(0x000440) == 0)")]
        [TestCase("P:0xH000440=0", "unless(byte(0x000440) == 0)")]
        [TestCase("A:0xN20770f=0_0xO20770f=0", "(bit1(0x20770F) + bit2(0x20770F)) == 0")]
        [TestCase("B:0xN20770f=0_0xO20770f=0", "(bit2(0x20770F) - bit1(0x20770F)) == 0")]
        [TestCase("C:0xN20770f=0_0xO20770f=0.4.", "repeated(4, bit1(0x20770F) == 0 || bit2(0x20770F) == 0)")]
        public void TestParseRequirements(string input, string expected)
        {
            var builder = new AchievementBuilder();
            builder.ParseRequirements(Tokenizer.CreateTokenizer(input));
            Assert.That(builder.RequirementsDebugString, Is.EqualTo(expected));
        }

        [Test]
        public void TestParseRequirementsAddSourceUninitializedRight()
        {
            // ensures requirement.Right is uninitialized for AddSource
            var builder = new AchievementBuilder();
            builder.ParseRequirements(Tokenizer.CreateTokenizer("A:0xN20770f=0"));
            var requirement = builder.CoreRequirements.First();
            Assert.That(requirement.Right.Size, Is.EqualTo(FieldSize.None));
            Assert.That(requirement.Right.Type, Is.EqualTo(FieldType.None));
            Assert.That(requirement.Right.Value, Is.EqualTo(0));
        }

        [Test]
        [TestCase("bit6(0x00627E) == 1", "0xS00627e=1")]
        [TestCase("prev(bit6(0x00627E)) == 0", "d0xS00627e=0")]
        [TestCase("bit6(0x00627E) == 1 && prev(bit6(0x00627E)) == 0", "0xS00627e=1_d0xS00627e=0")]
        [TestCase("byte(0x000028) == 3", "0xH000028=3")]
        [TestCase("3 == byte(0x000028)", "0xH000028=3")] // prefer constants on right
        [TestCase("high4(0x00616A) == 8", "0xU00616a=8")]
        [TestCase("word(0x000042) == 5786", "0x 000042=5786")]
        [TestCase("byte(0x000028) == 12 && word(0x000042) == 25959 || byte(0x0062AF) != 0 || word(0x0062AD) >= 10000", "0xH000028=12_0x 000042=25959S0xH0062af!=0S0x 0062ad>=10000")]
        [TestCase("once(byte(0x000440) == 140)", "0xH000440=140.1.")]
        [TestCase("never(byte(0x000440) == 0)", "R:0xH000440=0")]
        [TestCase("unless(byte(0x000440) == 0)", "P:0xH000440=0")]
        [TestCase("repeated(4, byte(0x1234) == 56 || byte(0x2345) == 67)", "C:0xH001234=56_0xH002345=67.4.")]
        [TestCase("dword(0x1234) == 12345678 * 30 / 60", "0xX001234=6172839")]
        [TestCase("byte(0x1234) - prev(byte(0x1234)) + byte(0x2345) == 6", "B:d0xH001234=0_A:0xH001234=0_0xH002345=6")]
        public void TestSerializeRequirements(string input, string expected)
        {
            // verify serialization of the builder
            var achievement = CreateAchievement(input);
            Assert.That(achievement.SerializeRequirements(), Is.EqualTo(expected));

            // convert to actual achievement and verify serialization of that
            var cheev = achievement.ToAchievement();
            Assert.That(AchievementBuilder.SerializeRequirements(cheev), Is.EqualTo(expected));
        }

        [Test]
        public void TestAreRequirementsSameExact()
        {
            var achievement1 = CreateAchievement("byte(0x001234) == 1 && byte(0x005678) == 78");
            var achievement2 = CreateAchievement("byte(0x001234) == 1 && byte(0x005678) == 78");
            Assert.That(achievement1.AreRequirementsSame(achievement2), Is.True);
        }

        [Test]
        public void TestAreRequirementsSameSubset()
        {
            var achievement1 = CreateAchievement("byte(0x001234) == 1");
            var achievement2 = CreateAchievement("byte(0x001234) == 1 && byte(0x005678) == 78");
            Assert.That(achievement1.AreRequirementsSame(achievement2), Is.False);
        }

        [Test]
        public void TestAreRequirementsSameSuperset()
        {
            var achievement1 = CreateAchievement("byte(0x001234) == 1 && byte(0x005678) == 78");
            var achievement2 = CreateAchievement("byte(0x001234) == 1");
            Assert.That(achievement1.AreRequirementsSame(achievement2), Is.False);
        }

        [Test]
        public void TestAreRequirementsSameReordered()
        {
            var achievement1 = CreateAchievement("byte(0x001234) == 1 && byte(0x005678) == 78");
            var achievement2 = CreateAchievement("byte(0x005678) == 78 && byte(0x001234) == 1");
            Assert.That(achievement1.AreRequirementsSame(achievement2), Is.True);
        }


        [Test]
        public void TestAreRequirementsSameExactAlts()
        {
            var achievement1 = CreateAchievement("byte(0x001234) == 1 || byte(0x005678) == 78");
            var achievement2 = CreateAchievement("byte(0x001234) == 1 || byte(0x005678) == 78");
            Assert.That(achievement1.AreRequirementsSame(achievement2), Is.True);
        }

        [Test]
        public void TestAreRequirementsSameSubsetAlts()
        {
            var achievement1 = CreateAchievement("byte(0x001234) == 1");
            var achievement2 = CreateAchievement("byte(0x001234) == 1 || byte(0x005678) == 78");
            Assert.That(achievement1.AreRequirementsSame(achievement2), Is.False);
        }

        [Test]
        public void TestAreRequirementsSameSupersetAlts()
        {
            var achievement1 = CreateAchievement("byte(0x001234) == 1 || byte(0x005678) == 78");
            var achievement2 = CreateAchievement("byte(0x001234) == 1");
            Assert.That(achievement1.AreRequirementsSame(achievement2), Is.False);
        }

        [Test]
        // ==== NormalizeComparisons ====
        [TestCase("byte(0x001234) == 1 && byte(0x004567) >= 0", "byte(0x001234) == 1")] // greater than or equal to 0 is always true, ignore it
        [TestCase("byte(0x001234) <= 0", "byte(0x001234) == 0")] // less than 0 can never be true, only keep the equals
        [TestCase("byte(0x001234) > 0", "byte(0x001234) != 0")] // less than 0 can never be true, so if it's greater than 0, it's just not zero
        [TestCase("bit0(0x001234) <= 0", "bit0(0x001234) == 0")] // less than 0 can never be true, only keep the equals
        [TestCase("bit0(0x001234) != 0", "bit0(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit1(0x001234) != 0", "bit1(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit2(0x001234) != 0", "bit2(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit3(0x001234) != 0", "bit3(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit4(0x001234) != 0", "bit4(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit5(0x001234) != 0", "bit5(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit6(0x001234) != 0", "bit6(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit7(0x001234) != 0", "bit7(0x001234) == 1")] // bit not equal to zero must be 1
        [TestCase("bit0(0x001234) > 0", "bit0(0x001234) == 1")] // bit greater than zero must be 1
        [TestCase("bit0(0x001234) != 1", "bit0(0x001234) == 0")] // bit not equal to one must be 0
        [TestCase("bit0(0x001234) < 1", "bit0(0x001234) == 0")] // bit less than one must be 0
        [TestCase("byte(0x001234) < 1", "byte(0x001234) == 0")] // byte less than one must be 0
        [TestCase("byte(0x001234) == 1 && byte(0x004567) < 0", "0 == 1")] // less than 0 can never be true, replace with always_false
        [TestCase("byte(0x001234) == 1 && low4(0x004567) > 15", "0 == 1")] // nibble cannot be greater than 15, replace with always_false
        [TestCase("byte(0x001234) == 1 && high4(0x004567) > 15", "0 == 1")] // nibble cannot be greater than 15, replace with always_false
        [TestCase("byte(0x001234) == 1 && byte(0x004567) > 255", "0 == 1")] // byte cannot be greater than 255, replace with always_false
        [TestCase("byte(0x001234) == 1 && word(0x004567) > 65535", "0 == 1")] // word cannot be greater than 255, replace with always_false
        [TestCase("byte(0x001234) == 1 && dword(0x004567) > 4294967295", "0 == 1")] // dword cannot be greater than 4294967295, replace with always_false
        [TestCase("byte(0x001234) == 1 && low4(0x004567) >= 15", "byte(0x001234) == 1 && low4(0x004567) == 15")] // nibble cannot be greater than 15, change to equals
        [TestCase("byte(0x001234) == 1 && high4(0x004567) >= 15", "byte(0x001234) == 1 && high4(0x004567) == 15")] // nibble cannot be greater than 15, change to equals
        [TestCase("byte(0x001234) == 1 && byte(0x004567) >= 255", "byte(0x001234) == 1 && byte(0x004567) == 255")] // byte cannot be greater than 255, change to equals
        [TestCase("byte(0x001234) == 1 && word(0x004567) >= 65535", "byte(0x001234) == 1 && word(0x004567) == 65535")] // word cannot be greater than 255, change to equals
        [TestCase("byte(0x001234) == 1 && dword(0x004567) >= 4294967295", "byte(0x001234) == 1 && dword(0x004567) == 4294967295")] // dword cannot be greater than 4294967295, change to equals
        [TestCase("byte(0x001234) == 1 && bit0(0x004567) <= 1", "byte(0x001234) == 1")] // bit always less than or equal to 1, ignore it
        [TestCase("byte(0x001234) == 1 && low4(0x004567) <= 15", "byte(0x001234) == 1")] // nibble always less than or equal to 15, ignore it
        [TestCase("byte(0x001234) == 1 && high4(0x004567) <= 15", "byte(0x001234) == 1")] // nibble always less than or equal to 15, ignore it
        [TestCase("byte(0x001234) == 1 && byte(0x004567) <= 255", "byte(0x001234) == 1")] // byte always less than or equal to 255, ignore it
        [TestCase("byte(0x001234) == 1 && word(0x004567) <= 65535", "byte(0x001234) == 1")] // word always less than or equal to 255, ignore it
        [TestCase("byte(0x001234) == 1 && dword(0x004567) <= 4294967295", "byte(0x001234) == 1")] // dword always less than or equal to 4294967295, ignore it
        [TestCase("byte(0x001234) == 1 && low4(0x004567) < 15", "byte(0x001234) == 1 && low4(0x004567) != 15")] // nibble cannot be greater than 15, change to not equals
        [TestCase("byte(0x001234) == 1 && high4(0x004567) < 15", "byte(0x001234) == 1 && high4(0x004567) != 15")] // nibble cannot be greater than 15, change to not equals
        [TestCase("byte(0x001234) == 1 && byte(0x004567) < 255", "byte(0x001234) == 1 && byte(0x004567) != 255")] // byte cannot be greater than 255, change to not equals
        [TestCase("byte(0x001234) == 1 && word(0x004567) < 65535", "byte(0x001234) == 1 && word(0x004567) != 65535")] // word cannot be greater than 255, change to not equals
        [TestCase("byte(0x001234) == 1 && dword(0x004567) < 4294967295", "byte(0x001234) == 1 && dword(0x004567) != 4294967295")] // dword cannot be greater than 4294967295, change to not equals
        [TestCase("bit0(0x001234) + bit1(0x001234) == 2", "(bit0(0x001234) + bit1(0x001234)) == 2")] // addition can exceed max size of source
        [TestCase("byte(0x001234) > 256", "0 == 1")] // can never be true
        [TestCase("byte(0x001234) < 256", "1 == 1")] // always true
        [TestCase("0 < 256", "1 == 1")] // always true
        [TestCase("0 == 1", "0 == 1")] // always false
        [TestCase("1 == 1", "1 == 1")] // always true
        [TestCase("3 > 6", "0 == 1")] // always false
        // ==== NormalizeNonHitCountResetAndPauseIfs ====
        [TestCase("never(byte(0x001234) != 5)", "byte(0x001234) == 5")]
        [TestCase("never(byte(0x001234) == 5)", "byte(0x001234) != 5")]
        [TestCase("never(byte(0x001234) >= 5)", "byte(0x001234) < 5")]
        [TestCase("never(byte(0x001234) > 5)", "byte(0x001234) <= 5")]
        [TestCase("never(byte(0x001234) <= 5)", "byte(0x001234) > 5")]
        [TestCase("never(byte(0x001234) < 5)", "byte(0x001234) >= 5")]
        [TestCase("unless(byte(0x001234) != 5)", "byte(0x001234) == 5")]
        [TestCase("unless(byte(0x001234) == 5)", "byte(0x001234) != 5")]
        [TestCase("unless(byte(0x001234) >= 5)", "byte(0x001234) < 5")]
        [TestCase("unless(byte(0x001234) > 5)", "byte(0x001234) <= 5")]
        [TestCase("unless(byte(0x001234) <= 5)", "byte(0x001234) > 5")]
        [TestCase("unless(byte(0x001234) < 5)", "byte(0x001234) >= 5")]
        [TestCase("unless(byte(0x001234) == 5) && byte(0x002345) == 1", "byte(0x001234) != 5 && byte(0x002345) == 1")] // unless without HitCount should be inverted to a requirement
        [TestCase("byte(0x001234) == 5 && never(byte(0x001234) != 5)", "byte(0x001234) == 5")] // common pattern in older achievements to fix HitCount at 0, the ResetIf is functionally redundant
        [TestCase("(byte(0x002345) == 5 && never(byte(0x001234) == 6)) || (byte(0x002345) == 6 && never(byte(0x001235) == 3))", 
                  "(byte(0x002345) == 5 && byte(0x001234) != 6) || (byte(0x002345) == 6 && byte(0x001235) != 3)")] // same logic applies to alt groups
        [TestCase("once(byte(0x002345) == 5) && never(byte(0x001234) != 5)", "once(byte(0x002345) == 5) && never(byte(0x001234) != 5)")] // if there's a HitCount, leave the ResetIf alone
        [TestCase("never(byte(0x001234) != 5) && (byte(0x002345) == 6 || once(byte(0x002345) == 7))", "never(byte(0x001234) != 5) && (byte(0x002345) == 6 || once(byte(0x002345) == 7))")] // if there's a HitCount anywhere, leave the ResetIf alone
        // ==== PromoteCommonAltsToCore ====
        [TestCase("byte(0x001234) == 1 && ((byte(0x004567) == 1 && byte(0x004568) == 0) || (byte(0x004568) == 0 && byte(0x004569) == 1))", 
                  "byte(0x001234) == 1 && byte(0x004568) == 0 && (byte(0x004567) == 1 || byte(0x004569) == 1)")] // memory check in both alts is promoted to core
        [TestCase("byte(0x001234) == 1 && ((once(byte(0x004567) == 1) && never(byte(0x004568) == 0)) || (never(byte(0x004568) == 0) && once(byte(0x004569) == 1)))",
                  "byte(0x001234) == 1 && never(byte(0x004568) == 0) && (once(byte(0x004567) == 1) || once(byte(0x004569) == 1))")] // ResetIf in both alts is promoted to core
        [TestCase("byte(0x001234) == 1 && ((once(byte(0x004567) == 1) && unless(byte(0x004568) == 0)) || (unless(byte(0x004568) == 0) && once(byte(0x004569) == 1)))",
                  "byte(0x001234) == 1 && ((once(byte(0x004567) == 1) && unless(byte(0x004568) == 0)) || (unless(byte(0x004568) == 0) && once(byte(0x004569) == 1)))")] // PauseIf in both alts is not promoted to core unless HitCounts are also promoted
        [TestCase("byte(0x001234) == 1 && ((once(byte(0x004567) == 1) && unless(byte(0x004568) == 0)) || (unless(byte(0x004568) == 0) && once(byte(0x004567) == 1)))",
                  "byte(0x001234) == 1 && unless(byte(0x004568) == 0) && once(byte(0x004567) == 1)")] // PauseIf in both alts is promoted to core if all HitCounts are also promoted
        [TestCase("once(byte(0x001234) == 1) && ((never(byte(0x002345) + byte(0x002346) == 2)) || (never(byte(0x002345) + byte(0x002347) == 2)))",
                  "once(byte(0x001234) == 1) && ((never((byte(0x002345) + byte(0x002346)) == 2)) || (never((byte(0x002345) + byte(0x002347)) == 2)))")] // partial AddSource cannot be promoted
        [TestCase("once(byte(0x001234) == 1) && ((never(byte(0x002345) == 1) && unless(byte(0x003456) == 3)) || (never(byte(0x002345) == 1) && unless(byte(0x003456) == 1)))",
                  "once(byte(0x001234) == 1) && ((never(byte(0x002345) == 1) && unless(byte(0x003456) == 3)) || (never(byte(0x002345) == 1) && unless(byte(0x003456) == 1)))")] // resetif cannot be promoted if pauseif is present
        [TestCase("once(byte(0x001234) == 1) && ((once(byte(0x002345) == 1) && unless(byte(0x003456) == 3)) || (once(byte(0x002345) == 1) && unless(byte(0x003456) == 1)))",
                  "once(byte(0x001234) == 1) && ((once(byte(0x002345) == 1) && unless(byte(0x003456) == 3)) || (once(byte(0x002345) == 1) && unless(byte(0x003456) == 1)))")] // item with hitcount cannot be promoted if pauseif is present
        // ==== RemoveDuplicates ====
        [TestCase("byte(0x001234) == 1 && byte(0x001234) == 1", "byte(0x001234) == 1")]
        [TestCase("prev(byte(0x001234)) == 1 && prev(byte(0x001234)) == 1", "prev(byte(0x001234)) == 1")]
        [TestCase("once(byte(0x001234) == 1) && once(byte(0x001234) == 1)", "once(byte(0x001234) == 1)")]
        [TestCase("never(byte(0x001234) != prev(byte(0x001234))) && never(byte(0x001234) != prev(byte(0x001234)))", "byte(0x001234) == prev(byte(0x001234))")]
        // ==== RemoveRedundancies ====
        [TestCase("byte(0x001234) > 1 && byte(0x001234) > 2", "byte(0x001234) > 2")] // >1 && >2 is only >2
        [TestCase("byte(0x001234) > 1 && byte(0x001235) > 2", "byte(0x001234) > 1 && byte(0x001235) > 2")] // different addresses
        [TestCase("byte(0x001234) > 1 && byte(0x001234) >= 2", "byte(0x001234) >= 2")] // >1 && >=2 is only >=2
        [TestCase("byte(0x001234) >= 1 && byte(0x001234) > 2", "byte(0x001234) > 2")] // >=1 && >2 is only >2
        [TestCase("byte(0x001234) < 3 && byte(0x001234) < 2", "byte(0x001234) < 2")] // <3 && <2 is only >2
        [TestCase("byte(0x001234) < 3 && byte(0x001235) < 2", "byte(0x001234) < 3 && byte(0x001235) < 2")] // different addresses
        [TestCase("byte(0x001234) < 3 && byte(0x001234) <= 2", "byte(0x001234) <= 2")] // <3 && <=2 is only <=2
        [TestCase("byte(0x001234) <= 3 && byte(0x001234) < 2", "byte(0x001234) < 2")] // <=3 && <2 is only <2
        [TestCase("byte(0x001234) != 3 && byte(0x001234) == 2", "byte(0x001234) == 2")] // =2 is implicitly !=3
        [TestCase("byte(0x001234) == 3 && byte(0x001234) != 2", "byte(0x001234) == 3")] // =3 is implicitly !=2
        [TestCase("byte(0x001234) == 3 && byte(0x001234) == 3", "byte(0x001234) == 3")] // redundant
        [TestCase("byte(0x001234) > 3 && byte(0x001234) < 2", "0 == 1")] // cannot both be true
        [TestCase("byte(0x001234) > 2 && byte(0x001234) < 2", "0 == 1")] // cannot both be true
        [TestCase("byte(0x001234) < 2 && byte(0x001234) > 2", "0 == 1")] // cannot both be true
        [TestCase("byte(0x001234) < 2 && byte(0x001234) > 3", "0 == 1")] // cannot both be true
        [TestCase("byte(0x001234) >= 3 && byte(0x001234) <= 2", "0 == 1")] // cannot both be true
        [TestCase("byte(0x001234) <= 2 && byte(0x001234) >= 3", "0 == 1")] // cannot both be true
        [TestCase("byte(0x001234) == 3 && byte(0x001234) == 2", "0 == 1")] // cannot both be true
        [TestCase("byte(0x001234) - prev(byte(0x001234)) == 1 && byte(0x001234) == 2", "(byte(0x001234) - prev(byte(0x001234))) == 1 && byte(0x001234) == 2")] // conflict with part of a SubSource clause should not be treated as wholly conflicting
        [TestCase("byte(0x001234) <= 2 && byte(0x001234) >= 2", "byte(0x001234) == 2")] // only overlap is the one value
        [TestCase("byte(0x001234) >= 2 && byte(0x001234) <= 2", "byte(0x001234) == 2")] // only overlap is the one value
        [TestCase("once(byte(0x001234) == 2) && once(byte(0x001234) == 3)", "once(byte(0x001234) == 2) && once(byte(0x001234) == 3)")] // cannot both be true at the same time, but can each be true once
        [TestCase("never(byte(0x001234) == 2) && never(byte(0x001234) == 3)", "byte(0x001234) != 2 && byte(0x001234) != 3")] // cannot both be true at the same time, but can each be true once
        [TestCase("never(byte(0x001234) == 2) && byte(0x001234) != 2", "byte(0x001234) != 2")] // no hitcount, only keep non-resetif
        [TestCase("byte(0x001234) == 2 && never(byte(0x001234) != 2)", "byte(0x001234) == 2")] // no hitcount, only keep non-resetif
        [TestCase("never(byte(0x001234) == 2) && byte(0x001234) != 2 && once(byte(0x001235) == 3)", "never(byte(0x001234) == 2) && once(byte(0x001235) == 3)")] // hitcount, only keep resetif
        [TestCase("byte(0x001234) == 2 && never(byte(0x001234) != 2) && once(byte(0x001235) == 3)", "never(byte(0x001234) != 2) && once(byte(0x001235) == 3)")] // hitcount, only keep resetif
        [TestCase("never(byte(0x001234) < 2) && repeated(10, byte(0x001234) >= 2)", "never(byte(0x001234) < 2) && repeated(10, byte(0x001234) >= 2)")] // HitCount on same field as ResetIf should not be optimized away
        // ==== MergeDuplicateAlts ====
        [TestCase("byte(0x001234) > 1 || byte(0x001234) > 2", "byte(0x001234) > 1")] // >1 || >2 is only >1
        [TestCase("byte(0x001234) > 1 || byte(0x001235) > 2", "byte(0x001234) > 1 || byte(0x001235) > 2")] // different addresses
        [TestCase("byte(0x001234) > 1 || byte(0x001234) >= 2", "byte(0x001234) > 1")] // >1 || >=2 is only >1
        [TestCase("byte(0x001234) >= 1 || byte(0x001234) > 2", "byte(0x001234) >= 1")] // >=1 && >2 is only >=1
        [TestCase("byte(0x001234) < 3 || byte(0x001234) < 2", "byte(0x001234) < 3")] // <3 || <2 is only <3
        [TestCase("byte(0x001234) < 3 || byte(0x001235) < 2", "byte(0x001234) < 3 || byte(0x001235) < 2")] // different addresses
        [TestCase("byte(0x001234) < 3 || byte(0x001234) <= 2", "byte(0x001234) < 3")] // <3 || <=2 is only <3
        [TestCase("byte(0x001234) <= 3 || byte(0x001234) < 2", "byte(0x001234) <= 3")] // <=3 || <2 is only <=3
        [TestCase("byte(0x001234) != 3 || byte(0x001234) == 2", "byte(0x001234) != 3")] // =2 is implicitly !=3
        [TestCase("byte(0x001234) == 3 || byte(0x001234) != 2", "byte(0x001234) != 2")] // =3 is implicitly !=2
        [TestCase("byte(0x001234) == 3 || byte(0x001234) == 3", "byte(0x001234) == 3")] // redundant
        [TestCase("byte(0x001234) == 3 || byte(0x001234) == 2", "byte(0x001234) == 3 || byte(0x001234) == 2")] // either can be true separately
        [TestCase("byte(0x001234) > 3 || byte(0x001234) < 2", "byte(0x001234) > 3 || byte(0x001234) < 2")] // either can be true separately
        [TestCase("byte(0x001234) > 2 || byte(0x001234) < 2", "byte(0x001234) != 2")] // <2 or >2 is just != 2
        [TestCase("byte(0x001234) < 2 || byte(0x001234) > 2", "byte(0x001234) != 2")] // either can be true separately
        [TestCase("byte(0x001234) < 2 || byte(0x001234) > 3", "byte(0x001234) < 2 || byte(0x001234) > 3")] // either can be true separately
        [TestCase("byte(0x001234) >= 3 || byte(0x001234) <= 2", "byte(0x001234) >= 3 || byte(0x001234) <= 2")] // always true, can't really collapse
        [TestCase("byte(0x001234) <= 2 || byte(0x001234) >= 3", "byte(0x001234) <= 2 || byte(0x001234) >= 3")] // always true, can't really collapse
        [TestCase("byte(0x001234) <= 2 || byte(0x001234) >= 2", "byte(0x001234) <= 2 || byte(0x001234) >= 2")] // always true, can't really collapse
        [TestCase("byte(0x001234) >= 2 || byte(0x001234) <= 2", "byte(0x001234) >= 2 || byte(0x001234) <= 2")] // always true, can't really collapse
        [TestCase("always_false() || byte(0x001234) == 2 || byte(0x001234) == 3", "byte(0x001234) == 2 || byte(0x001234) == 3")] // always_false group can be removed
        [TestCase("always_false() || byte(0x001234) == 2", "0 == 1 || byte(0x001234) == 2")] // minimum of two alts
        [TestCase("always_true() || byte(0x001234) == 2 || byte(0x001234) == 3", "")] // always_true group causes other groups to be ignored if they don't have a pauseif or resetif
        [TestCase("always_true() || byte(0x001234) == 2 || (byte(0x001234) == 3 && unless(byte(0x002345) == 1)) || (once(byte(0x001234) == 4) && never(byte(0x002345) == 1))",
            "1 == 1 || (byte(0x001234) == 3 && unless(byte(0x002345) == 1)) || (once(byte(0x001234) == 4) && never(byte(0x002345) == 1))")] // always_true group causes group without pauseif or resetif to be removed
        // ==== RemoveAltsAlreadyInCore ====
        [TestCase("byte(0x001234) == 2 && ((byte(0x001234) == 2 && byte(0x004567) == 3) || (byte(0x001234) == 2 && byte(0x004567) == 4))",
                  "byte(0x001234) == 2 && (byte(0x004567) == 3 || byte(0x004567) == 4)")]
        // ==== MergeBits ====
        [TestCase("bit0(0x001234) == 1 && bit1(0x001234) == 1 && bit2(0x001234) == 0 && bit3(0x001234) == 1", "low4(0x001234) == 11")]
        [TestCase("bit4(0x001234) == 1 && bit5(0x001234) == 1 && bit6(0x001234) == 0 && bit7(0x001234) == 1", "high4(0x001234) == 11")]
        [TestCase("low4(0x001234) == 12 && high4(0x001234) == 8", "byte(0x001234) == 140")]
        // ==== Complex ====
        [TestCase("byte(0x001234) == 1 && ((low4(0x004567) == 1 && high4(0x004567) >= 12) || (low4(0x004567) == 9 && high4(0x004567) >= 12) || (low4(0x004567) == 1 && high4(0x004567) >= 13))",
                  "byte(0x001234) == 1 && high4(0x004567) >= 12 && (low4(0x004567) == 1 || low4(0x004567) == 9)")] // alts 1 + 3 can be merged together, then the high4 extracted
        public void TestOptimize(string input, string expected)
        {
            var achievement = CreateAchievement(input);

            if (achievement.CoreRequirements.Count == 0)
            {
                // core requirement required, so fake one
                var fakeRequirement = new Requirement();
                achievement.CoreRequirements.Add(fakeRequirement);
                achievement.Optimize();
                achievement.CoreRequirements.Remove(fakeRequirement);
            }
            else
            {
                achievement.Optimize();
            }

            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }
    }
}
