using Jamiras.Components;
using Jamiras.Services;
using Moq;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RATools.Tests.ViewModels
{
    [TestFixture]
    class AchievementSetViewModelTests
    {
        private class AchievementSetViewModelHarness : AchievementSetViewModel
        {
            public AchievementSetViewModelHarness(int gameId, string title)
                : this(gameId, title, new Mock<IFileSystemService>().Object)
            {
            }

            public AchievementSetViewModelHarness(int gameId, string title, IFileSystemService fileSystemService)
                : base(new AchievementSet { OwnerGameId = gameId, OwnerSetId = 5, Title = title },
                      new Mock<ILogger>().Object, fileSystemService)
            {
            }

            public Achievement AddLocalAchievement(int id, string name)
            {
                if (LocalAssets == null)
                    AssociateRACacheDirectory(".");

                var achievement = new Achievement
                {
                    Id = id,
                    Title = name,
                    Description = name
                };

                ((List<Achievement>)LocalAssets.Achievements).Add(achievement);
                return achievement;
            }

            public Achievement AddPublishedAchievement(int id, string name)
            {
                if (LocalAssets == null)
                    AssociateRACacheDirectory(".");

                var achievement = new Achievement
                {
                    Id = id,
                    Title = name,
                    Category = 3
                };

                ((List<Achievement>)PublishedAssets.Achievements).Add(achievement);
                return achievement;
            }
        }

        [Test]
        public void TestInitialization()
        {
            var vmSet = new AchievementSetViewModelHarness(1234, "Title");
            Assert.That(vmSet.Id, Is.EqualTo(5));
            Assert.That(vmSet.AchievementSet.OwnerGameId, Is.EqualTo(1234));
            Assert.That(vmSet.Title, Is.EqualTo("Title"));
            Assert.That(vmSet.BadgeName, Is.EqualTo(""));
            Assert.That(vmSet.PublishedAssets, Is.Null);
            Assert.That(vmSet.LocalAssets, Is.Null);
        }

        [Test]
        public void TestReadPublished()
        {
            var mockFileSystemService = new Mock<IFileSystemService>();
            string mockPublished = "{\"Title\":\"GameTitle\",\"Achievements\":[" +
                "{\"ID\":123,\"MemAddr\":\"1=3\",\"Title\":\"Ach123\",\"Description\":\"Desc123\"," +
                 "\"Points\":6,\"Modified\":1625805850,\"Created\":1625508213,\"BadgeName\":\"4321\",\"Flags\":3}," +
                "{\"ID\":234,\"MemAddr\":\"2=3\",\"Title\":\"Ach234\",\"Description\":\"Desc234\"," +
                 "\"Points\":3,\"Modified\":1625805853,\"Created\":1625508215,\"BadgeName\":\"5555\",\"Flags\":5}" +
              "]}";
            mockFileSystemService.Setup(f => f.FileExists("C:\\Emulator\\RACache\\Data\\1234.json")).Returns(true);
            mockFileSystemService.Setup(f => f.OpenFile("C:\\Emulator\\RACache\\Data\\1234.json", OpenFileMode.Read))
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes(mockPublished)));
            var vmSet = new AchievementSetViewModelHarness(1234, "Title", mockFileSystemService.Object);
            vmSet.AssociateRACacheDirectory("C:\\Emulator\\RACache\\Data");
            Assert.That(vmSet.Title, Is.EqualTo("GameTitle"));

            Assert.That(vmSet.PublishedAssets, Is.Not.Null);
            Assert.That(vmSet.PublishedAssets.Achievements.Count(), Is.EqualTo(2));

            Assert.That(vmSet.LocalAssets, Is.Not.Null);
            Assert.That(vmSet.LocalAssets.Achievements.Count(), Is.EqualTo(0));
        }

        [Test]
        public void TestReadPublishedMultiset()
        {
            var mockFileSystemService = new Mock<IFileSystemService>();
            string mockPublished = "{\"Title\":\"GameTitle\",\"Sets\":[" +
                "{\"GameId\":2222,\"AchievementSetId\":2223,\"Title\":\"CoreSet\",\"Type\":\"core\",\"ImageIconUrl\":\"http://local/Images/012345.png\"," +
                 "\"Achievements\":[" +
                  "{\"ID\":123,\"MemAddr\":\"1=3\",\"Title\":\"Ach123\",\"Description\":\"Desc123\"," +
                   "\"Points\":6,\"Modified\":1625805850,\"Created\":1625508213,\"BadgeName\":\"4321\",\"Flags\":3}," +
                  "{\"ID\":234,\"MemAddr\":\"2=3\",\"Title\":\"Ach234\",\"Description\":\"Desc234\"," +
                   "\"Points\":3,\"Modified\":1625805853,\"Created\":1625508215,\"BadgeName\":\"5555\",\"Flags\":5}" +
                "]}," +
                "{\"GameId\":3333,\"AchievementSetId\":3334,\"Title\":\"Subset\",\"Type\":\"bonus\",\"ImageIconUrl\":\"http://local/Images/123456.png\"," +
                 "\"Achievements\":[" +
                  "{\"ID\":345,\"MemAddr\":\"1=3\",\"Title\":\"Ach123\",\"Description\":\"Desc123\"," +
                   "\"Points\":6,\"Modified\":1625805850,\"Created\":1625508213,\"BadgeName\":\"4321\",\"Flags\":3}" +
                "]}" +
                "]}";
            mockFileSystemService.Setup(f => f.FileExists("C:\\Emulator\\RACache\\Data\\2222.json")).Returns(true);
            mockFileSystemService.Setup(f => f.OpenFile("C:\\Emulator\\RACache\\Data\\2222.json", OpenFileMode.Read))
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes(mockPublished)));
            var vmSet = new AchievementSetViewModelHarness(2222, "Title", mockFileSystemService.Object);
            var sets = new List<AchievementSetViewModel>();
            sets.Add(vmSet);
            vmSet.AssociateRACacheDirectory("C:\\Emulator\\RACache\\Data", sets);

            Assert.That(vmSet.PublishedAssets, Is.Not.Null);
            Assert.That(vmSet.PublishedAssets.Achievements.Count(), Is.EqualTo(3));

            Assert.That(vmSet.LocalAssets, Is.Not.Null);
            Assert.That(vmSet.LocalAssets.Achievements.Count(), Is.EqualTo(0));

            Assert.That(sets.Count, Is.EqualTo(2));

            Assert.That(sets[0], Is.SameAs(vmSet));
            Assert.That(vmSet.Id, Is.EqualTo(2223));
            Assert.That(vmSet.Title, Is.EqualTo("CoreSet"));
            Assert.That(vmSet.BadgeName, Is.EqualTo("012345"));
            Assert.That(vmSet.AchievementSet.Type, Is.EqualTo(AchievementSetType.Core));

            Assert.That(sets[1].Id, Is.EqualTo(3334));
            Assert.That(sets[1].Title, Is.EqualTo("Subset"));
            Assert.That(sets[1].BadgeName, Is.EqualTo("123456"));
            Assert.That(sets[1].AchievementSet.Type, Is.EqualTo(AchievementSetType.Bonus));
        }
    }
}
