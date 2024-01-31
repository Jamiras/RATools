using Jamiras.Core.Tests;
using Jamiras.Services;
using Moq;
using NUnit.Framework;
using RATools.Data;
using RATools.Data.Tests;
using System.IO;
using System.Linq;
using System.Text;

namespace RATools.Parser.Tests
{
    [TestFixture]
    class LocalAssetsTests
    {
        private LocalAssets Initialize(string fileContents, MemoryStream memoryStream)
        {
            var fileName = "XXX-User.txt";
            var mockFileSystem = new Mock<IFileSystemService>();
            if (fileContents == null)
            {
                mockFileSystem.Setup(f => f.FileExists(fileName)).Returns(false);
            }
            else
            {
                mockFileSystem.Setup(f => f.FileExists(fileName)).Returns(true);
                mockFileSystem.Setup(f => f.OpenFile(fileName, OpenFileMode.Read)).Returns(new MemoryStream(Encoding.UTF8.GetBytes(fileContents)));
            }

            mockFileSystem.Setup(f => f.CreateFile(fileName)).Returns(memoryStream);

            return new LocalAssets(fileName, mockFileSystem.Object);
        }

        [Test]
        public void TestInitialize()
        {
            var achievements = Initialize("0.099\nTitle\n0:0xH0000c5=0_0xH0000b9=1:I Need Your Help:Talk to the ghost to begin your quest: : : :Jamiras:5:0:0:0:0:53352\n", null);
            Assert.That(achievements.Title, Is.EqualTo("Title"));
            Assert.That(achievements.Achievements.Count(), Is.EqualTo(1));

            var achievement = achievements.Achievements.First();
            Assert.That(achievement.Id, Is.EqualTo(0));
            Assert.That(achievement.Title, Is.EqualTo("I Need Your Help"));
            Assert.That(achievement.Description, Is.EqualTo("Talk to the ghost to begin your quest"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(achievement.BadgeName, Is.EqualTo("53352"));

            var builder = new AchievementBuilder(achievement);
            Assert.That(builder.RequirementsDebugString, Is.EqualTo("byte(0x0000C5) == 0 && byte(0x0000B9) == 1"));
        }

        [Test]
        public void TestReplace()
        {
            var achievements = Initialize("0.099\nTitle\n0:0xH001234=0:A:B: : : :U:5:0:0:D:R:B\n", null);
            Assert.That(achievements.Achievements.Count(), Is.EqualTo(1));

            var achievement = achievements.Achievements.First();

            var builder = new AchievementBuilder();
            builder.Title = "A2";
            builder.Description = "D2";
            builder.Points = 10;
            var achievement2 = builder.ToAchievement();

            var previous = achievements.Replace(achievement, achievement2);
            Assert.That(previous, Is.SameAs(achievement));

            Assert.That(achievements.Achievements.Count(), Is.EqualTo(1));
            Assert.That(achievements.Achievements.First(), Is.SameAs(achievement2));
        }

        [Test]
        public void TestReplaceAppend()
        {
            var achievements = Initialize("0.099\nTitle\n0:0xH001234=0:A:B: : : :U:5:0:0:D:R:B\n", null);
            Assert.That(achievements.Achievements.Count(), Is.EqualTo(1));

            var achievement = achievements.Achievements.First();

            var builder = new AchievementBuilder();
            builder.Title = "A2";
            builder.Description = "D2";
            builder.Points = 10;
            var achievement2 = builder.ToAchievement();

            var previous = achievements.Replace(null, achievement2);
            Assert.That(previous, Is.Null);

            Assert.That(achievements.Achievements.Count(), Is.EqualTo(2));
            Assert.That(achievements.Achievements.First(), Is.SameAs(achievement));
            Assert.That(achievements.Achievements.Last(), Is.SameAs(achievement2));
        }

        [Test]
        public void TestReplaceRemove()
        {
            var achievements = Initialize("0.099\nTitle\n0:0xH001234=0:A:B: : : :U:5:0:0:D:R:B\n", null);
            Assert.That(achievements.Achievements.Count(), Is.EqualTo(1));

            var achievement = achievements.Achievements.First();

            var previous = achievements.Replace(achievement, null);
            Assert.That(previous, Is.SameAs(achievement));

            Assert.That(achievements.Achievements.Count(), Is.EqualTo(0));
        }

        [Test]
        public void TestCommit()
        {
            var memoryStream = new MemoryStream();
            var achievements = Initialize("0.099\nTitle\n", memoryStream);
            Assert.That(achievements.Achievements.Count(), Is.EqualTo(0));

            var builder = new AchievementBuilder();
            builder.Title = "T";
            builder.Description = "D";
            builder.Points = 1;
            builder.CoreRequirements.Add(new Requirement {
                Left = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x1234 },
                Operator = RequirementOperator.Equal,
                Right = new Field { Type = FieldType.Value, Value = 1 }
            });
            var achievement = builder.ToAchievement();

            achievements.Replace(null, achievement);

            achievements.Commit("Test", null, null);

            var output = Encoding.UTF8.GetString(memoryStream.ToArray());
            Assert.That(output, Is.EqualTo("0.099\r\nTitle\r\n0:\"0xH001234=1\":\"T\":\"D\": : : :Test:1:0:0:0:0:\r\n"));
        }

        [Test]
        public void TestCommitNoFile()
        {
            var memoryStream = new MemoryStream();
            var achievements = Initialize(null, memoryStream);
            achievements.Title = "FromScript";
            Assert.That(achievements.Achievements.Count(), Is.EqualTo(0));

            var builder = new AchievementBuilder();
            builder.Title = "T";
            builder.Description = "D";
            builder.Points = 1;
            builder.CoreRequirements.Add(new Requirement
            {
                Left = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x1234 },
                Operator = RequirementOperator.Equal,
                Right = new Field { Type = FieldType.Value, Value = 1 }
            });
            var achievement = builder.ToAchievement();

            achievements.Replace(null, achievement);

            achievements.Commit("Test", null, null);

            var output = Encoding.UTF8.GetString(memoryStream.ToArray());
            Assert.That(output, Is.EqualTo("0.030\r\nFromScript\r\n0:\"0xH001234=1\":\"T\":\"D\": : : :Test:1:0:0:0:0:\r\n"));
        }

        [Test]
        public void TestCommitNew()
        {
            var memoryStream = new MemoryStream();
            var achievements = Initialize(null, memoryStream);
            achievements.Title = "Title";
            Assert.That(achievements.Achievements.Count(), Is.EqualTo(0));

            var builder = new AchievementBuilder();
            builder.Title = "T";
            builder.Description = "D";
            builder.Points = 1;
            builder.CoreRequirements.Add(new Requirement
            {
                Left = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x1234 },
                Operator = RequirementOperator.Equal,
                Right = new Field { Type = FieldType.Value, Value = 1 }
            });
            var achievement = builder.ToAchievement();

            achievements.Replace(null, achievement);

            achievements.Commit("Test", null, null);

            var output = Encoding.UTF8.GetString(memoryStream.ToArray());
            Assert.That(output, Is.EqualTo("0.030\r\nTitle\r\n0:\"0xH001234=1\":\"T\":\"D\": : : :Test:1:0:0:0:0:\r\n"));
        }

        [Test]
        public void TestCommitVersionDetected()
        {
            var memoryStream = new MemoryStream();
            var achievements = Initialize(null, memoryStream);
            achievements.Title = "Title";
            Assert.That(achievements.Achievements.Count(), Is.EqualTo(0));

            var builder = new AchievementBuilder();
            builder.Title = "T";
            builder.Description = "D";
            builder.Points = 1;
            builder.CoreRequirements.Add(new Requirement
            {
                Type = RequirementType.Trigger,
                Left = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x1234 },
                Operator = RequirementOperator.Equal,
                Right = new Field { Type = FieldType.Value, Value = 1 }
            });;
            var achievement = builder.ToAchievement();

            achievements.Replace(null, achievement);

            achievements.Commit("Test", null, null);

            var output = Encoding.UTF8.GetString(memoryStream.ToArray());
            Assert.That(output, Is.EqualTo("0.79\r\nTitle\r\n0:\"T:0xH001234=1\":\"T\":\"D\": : : :Test:1:0:0:0:0:\r\n"));
        }

        [Test]
        public void TestCommitVersionDetectedCulture()
        {
            using (var cultureOverride = new CultureOverride("fr-FR"))
            {
                var memoryStream = new MemoryStream();
                var achievements = Initialize(null, memoryStream);
                achievements.Title = "Title";
                Assert.That(achievements.Achievements.Count(), Is.EqualTo(0));

                var builder = new AchievementBuilder();
                builder.Title = "T";
                builder.Description = "D";
                builder.Points = 1;
                builder.CoreRequirements.Add(new Requirement
                {
                    Type = RequirementType.Trigger,
                    Left = new Field { Size = FieldSize.Byte, Type = FieldType.MemoryAddress, Value = 0x1234 },
                    Operator = RequirementOperator.Equal,
                    Right = new Field { Type = FieldType.Value, Value = 1 }
                }); ;
                var achievement = builder.ToAchievement();

                achievements.Replace(null, achievement);

                achievements.Commit("Test", null, null);

                var output = Encoding.UTF8.GetString(memoryStream.ToArray());
                Assert.That(output, Is.EqualTo("0.79\r\nTitle\r\n0:\"T:0xH001234=1\":\"T\":\"D\": : : :Test:1:0:0:0:0:\r\n"));
            }
        }
    }
}
