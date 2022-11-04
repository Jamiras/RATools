using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Parser.Expressions;
using RATools.Parser.Internal;
using System.Linq;

namespace RATools.Tests.Parser
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

        private static AchievementBuilder CreateAchievement(string input, string expectedError = null)
        {
            // NOTE: these are integration tests as they rely on ExpressionBase.Parse and 
            // AchievementScriptInterpreter.ScriptInterpreterAchievementBuilder, but using string 
            // inputs /output makes reading the tests and validating the behavior easier for humans.
            var tokenizer = new PositionalTokenizer(Tokenizer.CreateTokenizer(input));
            var expression = ExpressionBase.Parse(tokenizer);
            if (expression is ErrorExpression)
                Assert.Fail(((ErrorExpression)expression).Message);

            var achievement = new ScriptInterpreterAchievementBuilder();
            var error = achievement.PopulateFromExpression(expression);
            if (expectedError != null)
                Assert.That(error, Is.EqualTo(expectedError));
            else
                Assert.That(error, Is.Null.Or.Empty);

            return achievement;
        }

        [Test]
        [TestCase("0xS00627e=1", "bit6(0x00627E) == 1")]
        [TestCase("d0xS00627e=0", "prev(bit6(0x00627E)) == 0")]
        [TestCase("p0xK00627e=0", "prior(bitcount(0x00627E)) == 0")]
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
        [TestCase("A:0xN20770f*6_0xO20770f=0", "(bit1(0x20770F) * 6 + bit2(0x20770F)) == 0")]
        [TestCase("A:0xN20770f/6_0xO20770f=0", "(bit1(0x20770F) / 6 + bit2(0x20770F)) == 0")]
        [TestCase("A:0xN20770f&6_0xO20770f=0", "(bit1(0x20770F) & 6 + bit2(0x20770F)) == 0")]
        [TestCase("B:0xN20770f=0_0xO20770f=0", "(bit2(0x20770F) - bit1(0x20770F)) == 0")]
        [TestCase("C:0xN20770f=0_0xO20770f=0.4.", "tally(4, bit1(0x20770F) == 0, bit2(0x20770F) == 0)")]
        [TestCase("O:0xN20770f=0_0xO20770f=0.4.", "repeated(4, bit1(0x20770F) == 0 || bit2(0x20770F) == 0)")]
        [TestCase("N:0xN20770f=0_0xO20770f=0.1.", "once(bit1(0x20770F) == 0 && bit2(0x20770F) == 0)")]
        [TestCase("C:0xH464a70>d0xH464a70.1._M:0=1.8._I:0xW5b9624_Q:0xM000612=0",
                  "measured(tally(8, once(byte(0x464A70) > prev(byte(0x464A70)))), when=bit0(tbyte(0x5B9624) + 0x000612) == 0)")]
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
        [TestCase("0xS00627e", "bit6(0x00627E)")]
        [TestCase("d0xS00627e", "prev(bit6(0x00627E))")]
        [TestCase("p0xK00627e", "prior(bitcount(0x00627E))")]
        [TestCase("0xS00627e_d0xS00627e", "(bit6(0x00627E) + prev(bit6(0x00627E)))")]
        [TestCase("0xN20770f*6_0xO20770f", "(bit1(0x20770F) * 6 + bit2(0x20770F))")]
        [TestCase("0xN20770f*6_0xO20770f*-1", "(bit1(0x20770F) * 6 - bit2(0x20770F))")]
        [TestCase("0xO20770f*-1_0xN20770f*6", "(bit1(0x20770F) * 6 - bit2(0x20770F))")]
        [TestCase("0xN20770f*6_0xO20770f*-5", "(bit1(0x20770F) * 6 - bit2(0x20770F) * 5)")]
        [TestCase("0xO20770f*-5_0xN20770f*6", "(bit1(0x20770F) * 6 - bit2(0x20770F) * 5)")]
        public void TestParseValue(string input, string expected)
        {
            var builder = new AchievementBuilder();
            builder.ParseValue(Tokenizer.CreateTokenizer(input));
            Assert.That(builder.RequirementsDebugString, Is.EqualTo(expected));
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
        // ==== NormalizeLimits ===
        [TestCase("byte(0x001234) == 1 && byte(0x004567) >= 0", "byte(0x001234) == 1")] // greater than or equal to 0 is always true, ignore it
        [TestCase("byte(0x001234) >= 0 && byte(0x001234) <= 15", "byte(0x001234) <= 15")] // greater than or equal to 0 is always true, ignore it
        [TestCase("byte(0x001234) == 1 && prev(byte(0x004567)) >= 0", "byte(0x001234) == 1")] // greater than or equal to 0 is always true, ignore it
        [TestCase("byte(0x001234) == 1 || prev(byte(0x004567)) < 0", "byte(0x001234) == 1")] // less than 0 is always false, ignore it
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
        [TestCase("8 > byte(0x001234)", "byte(0x001234) < 8")] // prefer value on the right
        [TestCase("byte(0x001234) == 1 && byte(0x004567) < 0", "always_false()")] // less than 0 can never be true, replace with always_false
        [TestCase("byte(0x001234) == 1 && low4(0x004567) > 15", "always_false()")] // nibble cannot be greater than 15, replace with always_false
        [TestCase("byte(0x001234) == 1 && high4(0x004567) > 15", "always_false()")] // nibble cannot be greater than 15, replace with always_false
        [TestCase("byte(0x001234) == 1 && byte(0x004567) > 255", "always_false()")] // byte cannot be greater than 255, replace with always_false
        [TestCase("byte(0x001234) == 1 && word(0x004567) > 65535", "always_false()")] // word cannot be greater than 255, replace with always_false
        [TestCase("byte(0x001234) == 1 && dword(0x004567) > 4294967295", "always_false()")] // dword cannot be greater than 4294967295, replace with always_false
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
        [TestCase("byte(0x001234) == 1000", "always_false()")] // can never be true
        [TestCase("byte(0x001234) > 256", "always_false()")] // can never be true
        [TestCase("byte(0x001234) < 256", "always_true()")] // always true
        [TestCase("bitcount(0x1234) == 9", "always_false()")] // bitcount can never return more than 8
        [TestCase("bitcount(0x1234) + bitcount(0x1235) == 9", "(bitcount(0x001234) + bitcount(0x001235)) == 9")] // multiple bitcounts can be more than 8
        [TestCase("bitcount(0x1234) >= 8", "byte(0x001234) == 255")] // bitcount == 8 is all bits set
        [TestCase("measured(bitcount(0x1234) >= 8)", "measured(bitcount(0x001234) == 8)")] // don't convert bitcount to byte checks when wrapped in measured
        [TestCase("bitcount(0x1234) == 0", "byte(0x001234) == 0")] // bitcount == 0 is no bits set
        [TestCase("once(bit1(0x001234) == 255) && byte(0x002345) == 4", "always_false()")] // bit can never be 255, entire group is false
        // ==== NormalizeBCD ===
        [TestCase("bcd(byte(0x1234)) == 20", "byte(0x001234) == 32")]
        [TestCase("bcd(byte(0x1234)) == 100", "always_false()")] // BCD of a byte cannot exceed 99
        [TestCase("bcd(byte(0x1234)) < 100", "always_true()")] // BCD of a byte cannot exceed 99
        [TestCase("bcd(byte(0x1234)) >= 99", "byte(0x001234) >= 153")] // BCD of a byte can exceed 99, but it's not a valid BCD entry
        [TestCase("bcd(dword(0x1234)) == 12345678", "dword(0x001234) == 305419896")]
        [TestCase("bcd(dword(0x1234)) == 100000000", "always_false()")] // BCD of a dword cannot exceed 99999999
        [TestCase("bcd(byte(0x1234)) == bcd(byte(0x2345))", "byte(0x001234) == byte(0x002345)")] // BCD can be removed from both sides of the comparison
        [TestCase("bcd(byte(0x1234)) == byte(0x2345)", "bcd(byte(0x001234)) == byte(0x002345)")] // BCD cannot be removed when comparing to another memory address
        [TestCase("bcd(low4(0x1234)) == low4(0x2345)", "low4(0x001234) == low4(0x002345)")] // BCD can be removed for memory accessors of 4 bits or less
        [TestCase("low4(0x1234) == bcd(low4(0x2345))", "low4(0x001234) == low4(0x002345)")] // BCD can be removed for memory accessors of 4 bits or less
        [TestCase("bcd(low4(0x1234)) == 6", "low4(0x001234) == 6")] // BCD can be removed for memory accessors of 4 bits or less
        [TestCase("bcd(low4(0x1234)) == 10", "always_false()")] // BCD of a nummber cannot exceed 9
        // ==== NormalizeComparisons ===
        [TestCase("byte(0x001234) == prev(byte(0x001234))", "byte(0x001234) == prev(byte(0x001234))")] // non-deterministic
        [TestCase("byte(0x001234) == word(0x001234)", "byte(0x001234) == word(0x001234)")] // non-deterministic
        [TestCase("byte(0x001234) == byte(0x001234)", "always_true()")] // always true
        [TestCase("byte(0x001234) != byte(0x001234)", "always_false()")] // never true
        [TestCase("byte(0x001234) <= byte(0x001234)", "always_true()")] // always true
        [TestCase("byte(0x001234) < byte(0x001234)", "always_false()")] // never true
        [TestCase("byte(0x001234) >= byte(0x001234)", "always_true()")] // always true
        [TestCase("byte(0x001234) > byte(0x001234)", "always_false()")] // never true
        [TestCase("byte(0x001234) / byte(0x001234)", "byte(0x001234) / byte(0x001234)")] // indeterminant - 0 or 1
        [TestCase("byte(0x001234) * byte(0x001234)", "byte(0x001234) * byte(0x001234)")] // cannot be simplified
        [TestCase("byte(0x001234) & byte(0x001234)", "byte(0x001234) & byte(0x001234)")] // could be simplified to just byte(0x001234)
        [TestCase("once(byte(0x001234) == byte(0x001234))", "always_true()")] // always true
        [TestCase("repeated(3, byte(0x001234) == byte(0x001234))", "repeated(3, always_true())")] // always true, but ignored for two frames
        [TestCase("never(repeated(3, 1 == 1))", "never(repeated(3, always_true()))")] // always true, but ignored for two frames
        [TestCase("byte(0x001234) == 1 && repeated(3, never(1 == 1))", "byte(0x001234) == 1 && never(repeated(3, always_true()))")] // always true, but ignored for two frames
        [TestCase("repeated(3, byte(0x001234) != byte(0x001234))", "always_false()")] // always false will never be true, regardless of how many frames it's false
        [TestCase("0 < 256", "always_true()")] // always true
        [TestCase("0 == 1", "always_false()")] // always false
        [TestCase("1 == 1", "always_true()")] // always true
        [TestCase("3 > 6", "always_false()")] // always false
        [TestCase("4.56 == 4.56", "always_true()")] // always true
        [TestCase("4.56 > 4.57", "always_false()")] // always false
        [TestCase("4.56 > 4", "always_true()")] // always false
        [TestCase("0 == 1 && byte(0x001234) == 1", "always_false()")] // always false and anything is always false
        [TestCase("0 == 1 && (byte(0x001234) == 1 || byte(0x001234) == 2)", "always_false()")] // always false and anything is always false
        [TestCase("1 == 1 && byte(0x001234) == 1", "byte(0x001234) == 1")] // always true and anything is the anything clause
        [TestCase("1 == 1 && (1 == 2 || 1 == 3)", "always_false()")] // if all alts are false, entire trigger is false
        [TestCase("once(byte(0x004567) == 2) && (byte(0x002345) == 3 || (always_false() && never(byte(0x001234) == 1) && byte(0x001235) == 2))",
                  "once(byte(0x004567) == 2) && (byte(0x002345) == 3 || (never(byte(0x001234) == 1) && always_false()))")] // always_false paired with ResetIf does not eradicate the ResetIf
        [TestCase("once(byte(0x001234) == 1) && never(0 == 1)", "once(byte(0x001234) == 1)")] // a ResetIf for a condition that can never be true is redundant
        [TestCase("once(byte(0x001234) == 1) && unless(0 == 1)", "once(byte(0x001234) == 1)")] // a PauseIf for a condition that can never be true is redundant
        [TestCase("once(byte(0x001234) == 1) && never(1 == 1)", "never(always_true())")] // a ResetIf for a condition that is always true will never let the trigger fire
        [TestCase("once(byte(0x001234) == 1) && unless(1 == 1)", "always_false()")] // a PauseIf for a condition that is always true will prevent the trigger from firing
        [TestCase("once(byte(0x001234) == 1) && never(once(bit2(0x1234) == 255))", "once(byte(0x001234) == 1)")] // condition becomes always_false(), and never(always_false()) can be eliminated
        [TestCase("byte(0x001234) == 1 && never(byte(0x2345) > 0x2345)", "byte(0x001234) == 1")] // condition becomes always_false(), and never(always_false()) can be eliminated
        [TestCase("byte(0x001234) == 1 && unless(once(byte(0x2345) == 0x2345))", "byte(0x001234) == 1")] // condition becomes always_false(), and unless(always_false()) can be eliminated
        public void TestOptimizeNormalizeComparisons(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2))",
                  "never(byte(0x002345) == 2) && repeated(2, byte(0x001234) == 1)")] // ResetNextIf can be turned into a ResetIf
        [TestCase("never(byte(0x2222) == 2) && unless(repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2)))",
                  "never(byte(0x002222) == 2) && unless(repeated(2, byte(0x001234) == 1)) && (never(byte(0x002345) == 2))")] // ResetNextIf can be turned into a ResetIf, but has to be moved into an alt group
        [TestCase("byte(0x2222) == 2 || unless(repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2)))",
                  "byte(0x002222) == 2 || unless(repeated(2, byte(0x001234) == 1)) || (never(byte(0x002345) == 2) && always_false())")] // PauseLock cannot be eliminated, ResetNextIf turned into a ResetIf and moved to a separate alt
        [TestCase("byte(0x2222) == 2 || never(byte(0x3333) == 1) && unless(repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2)))",
                  "byte(0x002222) == 2 || (never(byte(0x003333) == 1) && unless(repeated(2, byte(0x001234) == 1))) || (never(byte(0x002345) == 2) && always_false())")] // ResetNextIf can be turned into a ResetIf, but has to be moved into an always false alt group
        [TestCase("once(byte(0x2222) == 2) && repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2))",
                  "once(byte(0x002222) == 2) && repeated(2, byte(0x001234) == 1 && never(byte(0x002345) == 2))")] // ResetNextIf cannot be turned into a ResetIf
        [TestCase("repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2)) && repeated(3, byte(0x1234) == 3 && never(byte(0x2345) == 2))",
                  "never(byte(0x002345) == 2) && repeated(2, byte(0x001234) == 1) && repeated(3, byte(0x001234) == 3)")] // similar ResetNextIfs can be turned into a ResetIf
        [TestCase("repeated(2, byte(0x1234) == 1 && never(byte(0x2345) == 2)) && repeated(3, byte(0x1234) == 3 && never(byte(0x2345) == 3))",
                  "repeated(2, byte(0x001234) == 1 && never(byte(0x002345) == 2)) && repeated(3, byte(0x001234) == 3 && never(byte(0x002345) == 3))")] // dissimilar ResetNextIfs cannot be turned into a ResetIf
        [TestCase("disable_when(byte(0x1234) == 1, until=byte(0x2345) == 2)",
                  "unless(once(byte(0x001234) == 1)) && (never(byte(0x002345) == 2))")] // Pause lock that's not guarding anything should not be inverted
        [TestCase("disable_when(byte(0x1234) == 1, until=byte(0x2345) == 2) && never(byte(0x3456) == 3)",
                  "unless(once(byte(0x001234) == 1)) && never(byte(0x003456) == 3) && (never(byte(0x002345) == 2))")] // ResetNextIf can be turned into a ResetIf, but has to be moved into an alt group
        [TestCase("disable_when(tally(2, byte(0x1234) == 1, byte(0x1234) == 2), until=byte(0x2345) == 2)",
                  "unless(tally(2, byte(0x001234) == 1, byte(0x001234) == 2)) && (never(byte(0x002345) == 2))")] // tally will generate similar ResetNextIfs which can be turned into a ResetIf, but has to be moved into an alt group
        [TestCase("never(byte(0x001234) != 5)", "byte(0x001234) == 5")]
        [TestCase("never(byte(0x001234) == 5)", "byte(0x001234) != 5")]
        [TestCase("never(byte(0x001234) >= 5)", "byte(0x001234) < 5")]
        [TestCase("never(byte(0x001234) > 5)", "byte(0x001234) <= 5")]
        [TestCase("never(byte(0x001234) <= 5)", "byte(0x001234) > 5")]
        [TestCase("never(byte(0x001234) < 5)", "byte(0x001234) >= 5")]
        [TestCase("never(byte(0x001234) != 1 && byte(0x002345) == 2)", "byte(0x001234) == 1 || byte(0x002345) != 2")] // AndNext becomes OrNext, both operators inverted
        [TestCase("unless(byte(0x001234) != 5)", "byte(0x001234) == 5")]
        [TestCase("unless(byte(0x001234) == 5)", "byte(0x001234) != 5")]
        [TestCase("unless(byte(0x001234) >= 5)", "byte(0x001234) < 5")]
        [TestCase("unless(byte(0x001234) > 5)", "byte(0x001234) <= 5")]
        [TestCase("unless(byte(0x001234) <= 5)", "byte(0x001234) > 5")]
        [TestCase("unless(byte(0x001234) < 5)", "byte(0x001234) >= 5")]
        [TestCase("unless(byte(0x001234) != 1 && byte(0x002345) == 2)", "byte(0x001234) == 1 || byte(0x002345) != 2")] // AndNext becomes OrNext, both operators inverted
        [TestCase("unless(byte(0x001234) == 5) && byte(0x002345) == 1", "byte(0x001234) != 5 && byte(0x002345) == 1")] // unless without HitCount should be inverted to a requirement
        [TestCase("unless(byte(0x001234) != 1) && unless(once(byte(0x002345) == 1))", "unless(byte(0x001234) != 1) && unless(once(byte(0x002345) == 1))")] // PauseLock is affected by Pause, so other Pause won't be inverted
        [TestCase("byte(0x001234) == 5 && never(byte(0x001234) != 5)", "byte(0x001234) == 5")] // common pattern in older achievements to fix HitCount at 0, the ResetIf is functionally redundant
        [TestCase("(byte(0x002345) == 5 && never(byte(0x001234) == 6)) || (byte(0x002345) == 6 && never(byte(0x001235) == 3))", 
                  "(byte(0x002345) == 5 && byte(0x001234) != 6) || (byte(0x002345) == 6 && byte(0x001235) != 3)")] // same logic applies to alt groups
        [TestCase("once(byte(0x002345) == 5) && never(byte(0x001234) != 5)", "once(byte(0x002345) == 5) && never(byte(0x001234) != 5)")] // if there's a HitCount, leave the ResetIf alone
        [TestCase("never(byte(0x001234) != 5) && (byte(0x002345) == 6 || once(byte(0x002345) == 7))", "never(byte(0x001234) != 5) && (byte(0x002345) == 6 || once(byte(0x002345) == 7))")] // if there's a HitCount anywhere, leave the ResetIf alone
        [TestCase("(measured(byte(0x1234) < 100) && unless(byte(0x1235) == 1)) || (measured(byte(0x1236) < 100) && unless(byte(0x1235) == 2))",
                  "(measured(byte(0x001234) < 100) && unless(byte(0x001235) == 1)) || (measured(byte(0x001236) < 100) && unless(byte(0x001235) == 2))")] // measured should prevent unless from being inverted
        public void TestOptimizeNormalizeResetIfsAndPauseIfs(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("byte(0x001234) == 1 && ((byte(0x004567) == 1 && byte(0x004568) == 0) || (byte(0x004568) == 0 && byte(0x004569) == 1))", 
                  "byte(0x001234) == 1 && byte(0x004568) == 0 && (byte(0x004567) == 1 || byte(0x004569) == 1)")] // memory check in both alts is promoted to core
        [TestCase("byte(0x001234) == 1 && ((once(byte(0x004567) == 1) && never(byte(0x004568) == 0)) || (never(byte(0x004568) == 0) && once(byte(0x004569) == 1)))",
                  "byte(0x001234) == 1 && never(byte(0x004568) == 0) && (once(byte(0x004567) == 1) || once(byte(0x004569) == 1))")] // ResetIf in both alts is promoted to core
        [TestCase("byte(0x001234) == 1 && ((once(byte(0x004567) == 1) && unless(byte(0x004568) == 0)) || (unless(byte(0x004568) == 0) && once(byte(0x004569) == 1)))",
                  "byte(0x001234) == 1 && ((once(byte(0x004567) == 1) && unless(byte(0x004568) == 0)) || (unless(byte(0x004568) == 0) && once(byte(0x004569) == 1)))")] // PauseIf is not promoted if any part of group differs from other alts
        [TestCase("byte(0x001234) == 1 && ((once(byte(0x004567) == 1) && unless(byte(0x004568) == 0)) || (unless(byte(0x004568) == 0) && once(byte(0x004567) == 1)))",
                  "byte(0x001234) == 1 && once(byte(0x004567) == 1) && unless(byte(0x004568) == 0)")] // PauseIf is only promoted if entire group is duplicated in all alts
        [TestCase("byte(0x001234) == 1 && ((byte(0x004567) == 1 && byte(0x004568) == 0) || (byte(0x004568) == 0))",
                  "byte(0x001234) == 1 && byte(0x004568) == 0")] // entire second alt is subset of first alt, eliminate first alt. remaining alt promoted to core
        [TestCase("once(byte(0x001234) == 1) && ((never(byte(0x002345) + byte(0x002346) == 2)) || (never(byte(0x002345) + byte(0x002347) == 2)))",
                  "once(byte(0x001234) == 1) && ((never((byte(0x002345) + byte(0x002346)) == 2)) || (never((byte(0x002345) + byte(0x002347)) == 2)))")] // partial AddSource cannot be promoted
        [TestCase("once(byte(0x001234) == 1) && ((never(byte(0x002345) == 1) && unless(byte(0x003456) == 3)) || (never(byte(0x002345) == 1) && unless(byte(0x003456) == 1)))",
                  "once(byte(0x001234) == 1) && ((never(byte(0x002345) == 1) && unless(byte(0x003456) == 3)) || (never(byte(0x002345) == 1) && unless(byte(0x003456) == 1)))")] // resetif cannot be promoted if pauseif is present
        [TestCase("once(byte(0x001234) == 1) && ((once(byte(0x002345) == 1) && unless(byte(0x003456) == 3)) || (once(byte(0x002345) == 1) && unless(byte(0x003456) == 1)))",
                  "once(byte(0x001234) == 1) && ((once(byte(0x002345) == 1) && unless(byte(0x003456) == 3)) || (once(byte(0x002345) == 1) && unless(byte(0x003456) == 1)))")] // item with hitcount cannot be promoted if pauseif is present
        [TestCase("1 == 1 && ((byte(0x001234) == 1 && byte(0x002345) == 1) || (byte(0x001234) == 1 && byte(0x002345) == 2))",
                  "byte(0x001234) == 1 && (byte(0x002345) == 1 || byte(0x002345) == 2)")] // core "1=1" should be removed by promotion of another condition
        [TestCase("measured(repeated(6, byte(0x1234) == 10), when=byte(0x2345) == 7) || measured(repeated(6, byte(0x2345) == 4), when=byte(0x2345) == 7)",
                  "(measured(repeated(6, byte(0x001234) == 10), when=byte(0x002345) == 7)) || (measured(repeated(6, byte(0x002345) == 4), when=byte(0x002345) == 7))")] // measured_if must stay with measured
        [TestCase("measured(repeated(6, byte(0x1234) == 10), when=byte(0x2345) == 7) || measured(repeated(6, byte(0x1234) == 10), when=byte(0x2345) == 7)",
                  "measured(repeated(6, byte(0x001234) == 10), when=byte(0x002345) == 7)")] // measured_if must stay with measured
        [TestCase("once(byte(0x001234) == 1) && (always_false() || never(byte(0x002345) == 1))", // always_false group is discarded, never is promoted
                  "once(byte(0x001234) == 1) && never(byte(0x002345) == 1)")]
        [TestCase("once(byte(0x001234) == 1) && (never(byte(0x002345) == 1) || never(byte(0x002345) == 1))", // duplicate alt is merge, never can be promoted to core
                  "once(byte(0x001234) == 1) && never(byte(0x002345) == 1)")]
        [TestCase("byte(0x001234) == 1 && (always_false() || once(byte(0x002345) == 2) && unless(byte(0x002345) == 1))", // always_false group is discarded, unless can be promoted because core won't be affected
                  "byte(0x001234) == 1 && once(byte(0x002345) == 2) && unless(byte(0x002345) == 1)")]
        [TestCase("once(byte(0x001234) == 1) && (always_false() || once(byte(0x002345) == 2) && unless(byte(0x002345) == 1))", // always_false group is discarded, unless is not promoted because of hit target
                  "once(byte(0x001234) == 1) && ((once(byte(0x002345) == 2) && unless(byte(0x002345) == 1)))")]
        [TestCase("once(byte(0x001234) == 1) && unless(once(byte(0x001234) == 1)) && (always_false() || never(byte(0x002345) == 1))", // never should not be promoted to core containing unless
                  "once(byte(0x001234) == 1) && unless(once(byte(0x001234) == 1)) && (never(byte(0x002345) == 1))")]
        public void TestOptimizePromoteCommonAltsToCore(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("byte(0x001234) == 1 && byte(0x001234) == 1", "byte(0x001234) == 1")]
        [TestCase("byte(0x001234) < 8 && 8 >= byte(0x001234)", "byte(0x001234) < 8")] // prefer value on the right
        [TestCase("prev(byte(0x001234)) == 1 && prev(byte(0x001234)) == 1", "prev(byte(0x001234)) == 1")]
        [TestCase("once(byte(0x001234) == 1) && once(byte(0x001234) == 1)", "once(byte(0x001234) == 1)")]
        [TestCase("never(byte(0x001234) != prev(byte(0x001234))) && never(byte(0x001234) != prev(byte(0x001234)))", "byte(0x001234) == prev(byte(0x001234))")]
        [TestCase("byte(0x001234) == 1 && byte(0x002345) + byte(0x001234) == 1", "byte(0x001234) == 1 && (byte(0x002345) + byte(0x001234)) == 1")] // duplicate in AddSource clause should be ignored
        [TestCase("byte(0x002345) + byte(0x001234) == 1 && byte(0x002345) + byte(0x001234) == 1", "(byte(0x002345) + byte(0x001234)) == 1")] // complete AddSource duplicate should be elimiated
        [TestCase("byte(0x001234) == 1 && once(byte(0x002345) == 1 && byte(0x001234) == 1)", "byte(0x001234) == 1 && once(byte(0x002345) == 1 && byte(0x001234) == 1)")] // duplicate in AndNext clause should be ignored
        [TestCase("once(byte(0x002345) == 1 && byte(0x001234) == 1) && once(byte(0x002345) == 1 && byte(0x001234) == 1)", "once(byte(0x002345) == 1 && byte(0x001234) == 1)")] // complete AndNext duplicate should be elimiated
        [TestCase("byte(0x001234) == 2 && ((byte(0x001234) == 2 && byte(0x004567) == 3) || (byte(0x001234) == 2 && byte(0x004567) == 4))",
                  "byte(0x001234) == 2 && (byte(0x004567) == 3 || byte(0x004567) == 4)")] // alts in core are redundant
        [TestCase("unless(byte(0x001234) == 1) && never(byte(0x002345) == 1) && ((unless(byte(0x001234) == 1) && once(byte(0x002345) == 2)) || (unless(byte(0x001234) == 1) && never(byte(0x002345) == 3)))", // PauseIf guarding once or never should not be promoted even if duplicated
                  "unless(byte(0x001234) == 1) && never(byte(0x002345) == 1) && ((unless(byte(0x001234) == 1) && once(byte(0x002345) == 2)) || (unless(byte(0x001234) == 1) && never(byte(0x002345) == 3)))")]
        public void TestOptimizeRemoveDuplicates(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }
        
        [TestCase("byte(0x001234) > 1 && byte(0x001234) > 2", "byte(0x001234) > 2")] // >1 && >2 is only >2
        [TestCase("byte(0x001234) > 3 && byte(0x001234) < 2", "always_false()")] // cannot both be true
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
        [TestCase("once(byte(0x1234) == 6) && repeated(5, byte(0x1234) == 6)", "repeated(5, byte(0x001234) == 6)")] // same condition with different hitcounts only honors higher hitcount
        [TestCase("repeated(5, byte(0x1234) == 6) && once(byte(0x1234) == 6)", "repeated(5, byte(0x001234) == 6)")] // same condition with different hitcounts only honors higher hitcount
        [TestCase("byte(0x1234) == 6 && repeated(5, byte(0x1234) == 6)", "byte(0x001234) == 6 && repeated(5, byte(0x001234) == 6)")] // without hitcount, cannot be merged
        [TestCase("once(byte(0x1234) == byte(0x1234)) && repeated(5, byte(0x2345) == byte(0x2345))", "repeated(5, always_true())")] // different conditions evaluate to always true, only capture higher hitcount
        [TestCase("once(byte(0x1234) == 1) && never(byte(0x2345) != 12) && never(byte(0x2345) == 0)", "once(byte(0x001234) == 1) && never(byte(0x002345) != 12)")] // never should keep the less restrictive condition
        public void TestOptimizeRemoveRedundancies(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

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
        [TestCase("byte(0x001234) >= 3 || byte(0x001234) <= 2", "byte(0x001234) >= 3 || byte(0x001234) <= 2")] // always true, can't collapse without overlap
        [TestCase("byte(0x001234) <= 2 || byte(0x001234) >= 3", "byte(0x001234) <= 2 || byte(0x001234) >= 3")] // always true, can't collapse without overlap
        [TestCase("byte(0x001234) <= 2 || byte(0x001234) >= 2", "always_true()")]
        [TestCase("byte(0x001234) >= 2 || byte(0x001234) <= 2", "always_true()")]
        [TestCase("always_false() || byte(0x001234) == 2 || byte(0x001234) == 3", "byte(0x001234) == 2 || byte(0x001234) == 3")] // always_false group can be removed
        [TestCase("always_false() || byte(0x001234) == 2", "byte(0x001234) == 2")] // always_false group can be removed
        [TestCase("always_true() || byte(0x001234) == 2 || byte(0x001234) == 3", "always_true()")] // always_true group causes other groups to be ignored if they don't have a pauseif or resetif
        [TestCase("always_true() || byte(0x001234) == 2 || (byte(0x001234) == 3 && unless(byte(0x002345) == 1)) || (once(byte(0x001234) == 4) && never(byte(0x002345) == 1))",
            "always_true() || (once(byte(0x001234) == 4) && never(byte(0x002345) == 1))")] // always_true alt causes groups without pauseif or resetif to be removed
        [TestCase("tally(2, once(byte(0x1111) == 1 && byte(0x2222) == 0), once(byte(0x1111) == 2 && byte(0x2222) == 0))",
            "tally(2, once(byte(0x001111) == 1 && byte(0x002222) == 0), once(byte(0x001111) == 2 && byte(0x002222) == 0))")]
        public void TestOptimizeMergeDuplicateAlts(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("bit0(0x001234) == 1 && bit1(0x001234) == 1 && bit2(0x001234) == 0 && bit3(0x001234) == 1", "low4(0x001234) == 11")]
        [TestCase("bit4(0x001234) == 1 && bit5(0x001234) == 1 && bit6(0x001234) == 0 && bit7(0x001234) == 1", "high4(0x001234) == 11")]
        [TestCase("low4(0x001234) == 12 && high4(0x001234) == 8", "byte(0x001234) == 140")]
        [TestCase("bit0(0x001234) + bit1(0x001234) + bit2(0x001234) + bit3(0x001234) + bit4(0x001234) + bit5(0x001234) + bit6(0x001234) + bit7(0x001234) == 6",
                  "bitcount(0x001234) == 6")]
        [TestCase("bit0(0x001234) + bit1(0x001234) + bit2(0x001234) + bit3(0x001234) + bit4(0x001234) + bit5(0x001234) + bit6(0x001234) + bit7(0x001234) + " +
                  "bit7(0x001234) + bit7(0x001234) + bit7(0x001234) + bit7(0x001234) + bit7(0x001234) + bit7(0x001234) + bit7(0x001234) + bit7(0x001234) == 6",
                  "(bitcount(0x001234) + bit7(0x001234) + bit7(0x001234) + bit7(0x001234) + bit7(0x001234) + bit7(0x001234) + bit7(0x001234) + bit7(0x001234) + bit7(0x001234)) == 6")]
        [TestCase("bit0(0x001234) + bit1(0x001234) + bit2(0x001234) + bit3(0x001234) + bit4(0x001234) + bit5(0x001234) + bit6(0x001234) + bit7(0x001234) + " +
                  "bit0(0x001234) + bit1(0x001234) + bit2(0x001234) + bit3(0x001234) + bit4(0x001234) + bit5(0x001234) + bit6(0x001234) + bit7(0x001234) == 6",
                  "(bitcount(0x001234) + bitcount(0x001234)) == 6")]
        [TestCase("bit0(0x001234) + bit1(0x001234) + bit2(0x001234) + bit3(0x001234) + bit4(0x001234) + bit5(0x001234) + bit6(0x001234) + bit7(0x001234) + bit0(0x001235) == 6",
                  "(bitcount(0x001234) + bit0(0x001235)) == 6")]
        [TestCase("bit7(0x001233) + bit0(0x001234) + bit1(0x001234) + bit2(0x001234) + bit3(0x001234) + bit4(0x001234) + bit5(0x001234) + bit6(0x001234) + bit7(0x001234) == 6",
                  "(bit7(0x001233) + bitcount(0x001234)) == 6")]
        [TestCase("bit0(0x001234) + bit1(0x001234) + bit2(0x001234) + bit3(0x001234) + bit4(0x001234) + bit0(0x001235) + bit5(0x001234) + bit6(0x001234) + bit7(0x001234) == 6",
                  "(bitcount(0x001234) + bit0(0x001235)) == 6")]
        [TestCase("bit0(0x001234) + bit1(0x001235) + bit2(0x001234) + bit3(0x001234) + bit4(0x001234) + bit5(0x001234) + bit6(0x001234) + bit7(0x001234) == 6",
                  "(bit0(0x001234) + bit1(0x001235) + bit2(0x001234) + bit3(0x001234) + bit4(0x001234) + bit5(0x001234) + bit6(0x001234) + bit7(0x001234)) == 6")]
        [TestCase("bit0(0x001234) + prev(bit1(0x001234)) + bit2(0x001234) + bit3(0x001234) + bit4(0x001234) + bit5(0x001234) + bit6(0x001234) + bit7(0x001234) == 6",
                  "(bit0(0x001234) + prev(bit1(0x001234)) + bit2(0x001234) + bit3(0x001234) + bit4(0x001234) + bit5(0x001234) + bit6(0x001234) + bit7(0x001234)) == 6")]
        [TestCase("prev(bit0(0x001234)) + prev(bit1(0x001234)) + prev(bit2(0x001234)) + prev(bit3(0x001234)) + " +
                  "prev(bit4(0x001234)) + prev(bit5(0x001234)) + prev(bit6(0x001234)) + prev(bit7(0x001234)) == 6",
                  "prev(bitcount(0x001234)) == 6")]
        public void TestOptimizeMergeBits(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("word(word(0x001234) + 138) + 1 >= word(0x2345)",             // AddAddress compared to a non-AddAddress will generate an extra condition
                  "((word(word(0x001234) + 0x00008A)) + 1) >= word(0x002345)")] // to prevent the AddAddress from affecting the non-AddAddress. merge the +1 into that
        [TestCase("never(once(prev(byte(1)) - 1 == byte(1)) && repeated(10, always_true())",
                  "never(repeated(10, (once((prev(byte(0x000001)) - 1) == byte(0x000001))) && always_true()))")] // don't merge the -1 in the prev clause with the 1 in the always_true clause
        public void TestOptimizeMergeAddSourceConstants(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("repeated(2, byte(0x1234) == 120 || byte(0x1234) == 126)",
                  "repeated(2, byte(0x001234) == 120 || byte(0x001234) == 126)")]
        [TestCase("repeated(2, byte(0x1234) == 120 || byte(0x1234) == 126)",
                  "repeated(2, byte(0x001234) == 120 || byte(0x001234) == 126)")]
        [TestCase("measured(repeated(2, byte(0x1234) == 120 || byte(0x1234) == 126))",
                  "measured(repeated(2, byte(0x001234) == 120 || byte(0x001234) == 126))")]
        [TestCase("once(byte(0x2345) == 1) && never(!(byte(0x1234) <= 8 && byte(0x1234) >= 6))",
                  "once(byte(0x002345) == 1) && never(byte(0x001234) > 8) && never(byte(0x001234) < 6)")]
        [TestCase("never(!(byte(0x1234) <= 8 && byte(0x1234) >= 6) && byte(0x2345) >= 10)", 
                  "never((byte(0x001234) > 8 || byte(0x001234) < 6) && byte(0x002345) >= 10)")]
        [TestCase("repeated(10, byte(0x2345) == 1 && byte(0x3456) == 2 && never(byte(0x1234) < 5 || byte(0x1234) > 8))", // never will be converted to a ResetNextIf (before repeated), which will be further converted to two ResetIfs
                  "never(byte(0x001234) < 5) && never(byte(0x001234) > 8) && repeated(10, byte(0x002345) == 1 && byte(0x003456) == 2)")]
        [TestCase("repeated(10, byte(0x2345) == 1 && byte(0x3456) == 2 && never(byte(0x1234) < 5 || byte(0x1234) > 8)) && once(byte(0x5678) == 2)",
                  "repeated(10, byte(0x002345) == 1 && byte(0x003456) == 2 && never(byte(0x001234) < 5 || byte(0x001234) > 8)) && once(byte(0x005678) == 2)")]
        public void TestOptimizeDenormalizeOrNexts(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [TestCase("byte(0x001234) == 1 && ((low4(0x004567) == 1 && high4(0x004567) >= 12) || (low4(0x004567) == 9 && high4(0x004567) >= 12) || (low4(0x004567) == 1 && high4(0x004567) >= 13))",
                  "byte(0x001234) == 1 && high4(0x004567) >= 12 && (low4(0x004567) == 1 || low4(0x004567) == 9)")] // alts 1 + 3 can be merged together, then the high4 extracted
        [TestCase("0 == 1 && never(byte(0x001234) == 1)", "always_false()")] // ResetIf without available HitCount inverted, then can be eliminated by always false
        [TestCase("once(always_false() || word(0x1234) >= 284 && word(0x1234) <= 301)",
                  "once(word(0x001234) >= 284 && word(0x001234) <= 301)")] // OrNext will move always_false to end, which will have the HitCount, HitCount should be kept when always_false is eliminated
        [TestCase("tally(2, once(always_false() || word(0x1234) >= 284 && word(0x1234) <= 301))",
                  "tally(2, once(word(0x001234) >= 284 && word(0x001234) <= 301))")] // always_false() inside once() is optimized out
        [TestCase("tally(2, once(byte(0x1234) == 1) || once(byte(0x1234) == 2) || once(byte(0x1234) == 3))",
                  "tally(2, (once(byte(0x001234) == 1) || once(byte(0x001234) == 2) || once(byte(0x001234) == 3)))")]
        [TestCase("measured(byte(0x1234) == 120, when = (byte(0x2345) == 6 || byte(0x2346) == 7))", // OrNext in MeasuredIf should not be split into alts
                  "measured(byte(0x001234) == 120, when=(byte(0x002345) == 6 || byte(0x002346) == 7))")]
        public void TestOptimizeComplex(string input, string expected)
        {
            var achievement = CreateAchievement(input);
            achievement.Optimize();
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo(expected));
        }

        [Test]
        // ==== CrossMultiplyOrConditions ====
        [TestCase("(A || B) && (C || D)", "(A && C) || (A && D) || (B && C) || (B && D)")]
        [TestCase("(A || B) && (A || D)", "(A && A) || (A && D) || (B && A) || (B && D)")]
        [TestCase("(A || B) && (A || C) && (B || C)",
                  "(A && A && B) || (A && A && C) || (A && C && B) || (A && C && C) || " +
                  "(B && A && B) || (B && A && C) || (B && C && B) || (B && C && C)")]
        [TestCase("((A && B) || (C && D)) && ((A && C) || (B && D))",
                  "(A && B && A && C) || (A && B && B && D) || (C && D && A && C) || (C && D && B && D)")]
        [TestCase("(A || B || C) && (D || E || F)",
                  "(A && D) || (A && E) || (A && F) || (B && D) || (B && E) || (B && F) || (C && D) || (C && E) || (C && F)")]
        [TestCase("(A && (B || C)) && (D || E)",
                  "A && ((B && D) || (B && E) || (C && D) || (C && E))")]
        [TestCase("A && (B || (C && D)) && (E || F)",
                  "A && ((B && E) || (B && F) || (C && D && E) || (C && D && F))")]
        public void TestOrExpansion(string input, string expected)
        {
            input = input.Replace("A", "byte(0x00000A) == 1");
            input = input.Replace("B", "byte(0x00000B) == 1");
            input = input.Replace("C", "byte(0x00000C) == 1");
            input = input.Replace("D", "byte(0x00000D) == 1");
            input = input.Replace("E", "byte(0x00000E) == 1");
            input = input.Replace("F", "byte(0x00000F) == 1");

            var achievement = CreateAchievement(input);

            // NOTE: not optimized - that's tested separately in TestOptimize
            var result = achievement.RequirementsDebugString;
            result = result.Replace("byte(0x00000A) == 1", "A");
            result = result.Replace("byte(0x00000B) == 1", "B");
            result = result.Replace("byte(0x00000C) == 1", "C");
            result = result.Replace("byte(0x00000D) == 1", "D");
            result = result.Replace("byte(0x00000E) == 1", "E");
            result = result.Replace("byte(0x00000F) == 1", "F");

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void TestOrExpansionLarge()
        {
            var largeExpression = "(A || B || C) && (D || E || F) && (A || C || E)";

            // cross-multiplication would result in 27 clauses. if more than 20 would be generated,
            // the code switches to using OrNext and cross-multiplying simpler clauses.
            // in this case, OrNext can be used in all clauses, so the output should match the input.
            TestOrExpansion(largeExpression, largeExpression);
        }

        [Test]
        public void TestNotAlwaysFalse()
        {
            var achievement = CreateAchievement("!always_false()");
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo("always_true()"));
        }

        [Test]
        public void TestNotAlwaysTrue()
        {
            var achievement = CreateAchievement("!always_true()");
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo("always_false()"));
        }

        [Test]
        public void TestNotAlwaysFalseChain()
        {
            var achievement = CreateAchievement("!(always_false() || byte(0x1234) == 2 || byte(0x1234) == 5)");
            Assert.That(achievement.RequirementsDebugString, Is.EqualTo("always_true() && byte(0x001234) != 2 && byte(0x001234) != 5"));
        }

        [Test]
        public void TestMemoryReferenceWithoutComparison()
        {
            CreateAchievement("byte(0x1234)", "Incomplete trigger condition");
            CreateAchievement("byte(0x1234) && byte(0x2345) == 1", "Incomplete trigger condition");
            CreateAchievement("byte(0x1234) == 1 && byte(0x2345)", "Incomplete trigger condition");
            CreateAchievement("byte(0x1234) || byte(0x2345) == 1", "Incomplete trigger condition");
            CreateAchievement("byte(0x1234) == 1 || byte(0x2345)", "Incomplete trigger condition");
        }

        [Test]
        public void TestNestedComparison()
        {
            CreateAchievement("(byte(0x1234) == 2) == 1", "Cannot chain comparisons");
        }

        [Test]
        public void TestTriggerWhenDistribution()
        {
            var achievement = CreateAchievement("once(byte(0x1234) == 1) && " +
                "trigger_when(byte(0x2345) == 2 && byte(0x2346) == 3 && (byte(0x2347) == 0 || byte(0x2347) == 1)) && " +
                "never(byte(0x3456) == 2)");
            achievement.Optimize();
            Assert.That(achievement.SerializeRequirements(), 
                Is.EqualTo("0xH001234=1.1._T:0xH002345=2_T:0xH002346=3_R:0xH003456=2ST:0xH002347=0ST:0xH002347=1"));
        }

        [Test]
        public void TestOrFirst()
        {
            var achievement = CreateAchievement("(byte(0x1234) == 1 || byte(0x1234) == 2) && " +
                "never(byte(0x2345) == 3) && once(byte(0x3456) == 4)");
            achievement.Optimize();
            Assert.That(achievement.SerializeRequirements(),
                Is.EqualTo("R:0xH002345=3_0xH003456=4.1.S0xH001234=1S0xH001234=2"));
        }

        [Test]
        public void TestGuardedResetIfWithAlts()
        {
            var achievement = CreateAchievement("once(byte(0x1234) == 1) && " +
                "(always_true() || (always_false() && never(byte(0x2345) == 2) && unless(byte(0x3456) == 3))) && " +
                "(byte(0x4567) == 1 || byte(0x004567) == 2)");
            achievement.Optimize();
            Assert.That(achievement.SerializeRequirements(),
                Is.EqualTo("0xH001234=1.1.S0xH004567=1S0xH004567=2SR:0xH002345=2_P:0xH003456=3_0=1"));
        }
    }
}
