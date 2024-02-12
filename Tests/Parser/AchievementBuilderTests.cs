using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser.Expressions;
using System.Linq;

namespace RATools.Parser.Tests
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
            achievement.Trigger = new Trigger(
                new Requirement[]
                {
                    new Requirement { Left = address1, Operator = RequirementOperator.Equal, Right = value1 },
                    new Requirement { Left = address2, Operator = RequirementOperator.Equal, Right = value2 },
                },
                new Requirement[][]
                {
                    new Requirement[]
                    {
                        new Requirement { Left = address3, Operator = RequirementOperator.Equal, Right = value3 },
                    },
                    new Requirement[]
                    {
                        new Requirement { Left = address3, Operator = RequirementOperator.Equal, Right = value4 },
                    }
                }
            );

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
        [TestCase("A:0xN20770f&6_0xO20770f=0", "(bit1(0x20770F) & 0x06 + bit2(0x20770F)) == 0")]
        [TestCase("B:0xN20770f=0_0xO20770f=0", "(bit2(0x20770F) - bit1(0x20770F)) == 0")]
        [TestCase("C:0xN20770f=0_0xO20770f=0.4.", "tally(4, bit1(0x20770F) == 0, bit2(0x20770F) == 0)")]
        [TestCase("O:0xN20770f=0_0xO20770f=0.4.", "repeated(4, bit1(0x20770F) == 0 || bit2(0x20770F) == 0)")]
        [TestCase("N:0xN20770f=0_0xO20770f=0.1.", "once(bit1(0x20770F) == 0 && bit2(0x20770F) == 0)")]
        [TestCase("M:0xH001234>=100_Q:0x 002345=7",
                  "measured(byte(0x001234) >= 100, when=word(0x002345) == 7)")]
        [TestCase("Q:0x 002345=7", "measured_if(word(0x002345) == 7)")] // this is an error - measured_if without measured cannot be converted to a when clause
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
    }
}
