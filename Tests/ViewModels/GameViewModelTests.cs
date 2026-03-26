using Jamiras.Components;
using Jamiras.Services;
using Moq;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Services;
using RATools.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RATools.Tests.ViewModels
{
    [TestFixture]
    class GameViewModelTests
    {
        private class GameViewModelHarness : GameViewModel
        {
            public GameViewModelHarness(int gameId, string title)
                : this(gameId, title, new Mock<IFileSystemService>().Object)
            {
            }

            public GameViewModelHarness(int gameId, string title, IFileSystemService fileSystemService)
                : base(gameId, title, new Mock<ILogger>().Object, fileSystemService)
            {
                Script = new Mock<ScriptViewModel>().Object;
                SelectedEditor = Script;
            }

            public Achievement AddLocalAchievement(int id, string name)
            {
                if (_localAssets == null)
                    _localAssets = new LocalAssets(GameId + "-User.txt", _fileSystemService);

                var achievement = new Achievement
                {
                    Id = id,
                    Title = name,
                    Description = name
                };

                ((List<Achievement>)_localAssets.Achievements).Add(achievement);
                return achievement;
            }
            public Achievement AddPublishedAchievement(int id, string name)
            {
                if (_publishedAssets == null)
                    _publishedAssets = new PublishedAssets(GameId + ".json", _fileSystemService);

                var achievement = new Achievement
                {
                    Id = id,
                    Title = name,
                    Category = 3
                };

                ((List<Achievement>)_publishedAssets.Achievements).Add(achievement);
                return achievement;
            }

            public new void PopulateEditorList(AchievementScriptInterpreter interpreter)
            {
                ServiceRepository.Reset();
                var backgroundWorkerService = new Mock<IBackgroundWorkerService>();
                backgroundWorkerService.Setup(b => b.InvokeOnUiThread(It.IsAny<Action>())).Callback((Action a) => a());
                ServiceRepository.Instance.RegisterInstance(backgroundWorkerService.Object);
                ServiceRepository.Instance.RegisterInstance(new Mock<ISettings>().Object);
                base.PopulateEditorList(interpreter);
                ServiceRepository.Reset();
            }
        }

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            ServiceRepository.Reset();

            var mockSettings = new Mock<ISettings>();
            mockSettings.Setup(s => s.HexValues).Returns(true);
            ServiceRepository.Instance.RegisterInstance<ISettings>(mockSettings.Object);

            ServiceRepository.Instance.RegisterInstance<IClipboardService>(new Mock<IClipboardService>().Object);
        }

        [OneTimeTearDown]
        public void FixtureTeardown()
        {
            ServiceRepository.Reset();
        }

        [Test]
        public void TestInitialization()
        {
            var vmGame = new GameViewModelHarness(1234, "Title");
            Assert.That(vmGame.GameId, Is.EqualTo(1234));
            Assert.That(vmGame.Title, Is.EqualTo("Title"));
            Assert.That(vmGame.Script, Is.Not.Null);
            Assert.That(vmGame.Editors.Count(), Is.EqualTo(0)); // editors list isn't populated until PopulateEditorList is called
            Assert.That(vmGame.SelectedEditor, Is.SameAs(vmGame.Script));
            Assert.That(vmGame.Notes, Is.Not.Null.And.Empty);
            Assert.That(vmGame.CompileProgress, Is.EqualTo(0));
            Assert.That(vmGame.CompileProgressLine, Is.EqualTo(0));
            Assert.That(vmGame.GeneratedAchievementCount, Is.EqualTo(0));
            Assert.That(vmGame.CoreAchievementCount, Is.EqualTo(0));
            Assert.That(vmGame.CoreAchievementPoints, Is.EqualTo(0));
            Assert.That(vmGame.UnofficialAchievementCount, Is.EqualTo(0));
            Assert.That(vmGame.UnofficialAchievementPoints, Is.EqualTo(0));
            Assert.That(vmGame.LocalAchievementCount, Is.EqualTo(0));
            Assert.That(vmGame.LocalAchievementPoints, Is.EqualTo(0));
        }

        [Test]
        public void TestLoadNotes()
        {
            var mockFileSystemService = new Mock<IFileSystemService>();
            string mockNotes =
                "[{\"User\":\"Joe\",\"Address\":\"0x000123\",\"Note\":\"Test1\"}," +
                "{\"User\":\"Joe\",\"Address\":\"0x000124\",\"Note\":\"Test2\"}," +
                "{\"User\":\"Joe\",\"Address\":\"0x000125\",\"Note\":\"Test3\"}," +
                "{\"User\":\"Joe\",\"Address\":\"0x0001AF\",\"Note\":\"Test4\"}]";
            mockFileSystemService.Setup(f => f.OpenFile("C:\\Emulator\\RACache\\Data\\1234-Notes.json", OpenFileMode.Read))
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes(mockNotes)));
            var vmGame = new GameViewModelHarness(1234, "Title", mockFileSystemService.Object);
            vmGame.AssociateRACacheDirectory("C:\\Emulator\\RACache\\Data");

            Assert.That(vmGame.RACacheDirectory, Is.EqualTo("C:\\Emulator\\RACache\\Data"));
            Assert.That(vmGame.Notes, Is.Not.Null);
            Assert.That(vmGame.Notes.Count, Is.EqualTo(4));
            Assert.That(vmGame.Notes[0x123].Note, Is.EqualTo("Test1"));
            Assert.That(vmGame.Notes[0x124].Note, Is.EqualTo("Test2"));
            Assert.That(vmGame.Notes[0x125].Note, Is.EqualTo("Test3"));
            Assert.That(vmGame.Notes[0x1AF].Note, Is.EqualTo("Test4"));
        }

        [Test]
        public void TestLoadNotesEmpty()
        {
            var mockFileSystemService = new Mock<IFileSystemService>();
            string mockNotes = "[]";
            mockFileSystemService.Setup(f => f.OpenFile("C:\\Emulator\\RACache\\Data\\1234-Notes.json", OpenFileMode.Read))
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes(mockNotes)));
            var vmGame = new GameViewModelHarness(1234, "Title", mockFileSystemService.Object);
            vmGame.AssociateRACacheDirectory("C:\\Emulator\\RACache\\Data");

            Assert.That(vmGame.RACacheDirectory, Is.EqualTo("C:\\Emulator\\RACache\\Data"));
            Assert.That(vmGame.Notes, Is.Not.Null);
            Assert.That(vmGame.Notes.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestLoadNotesNonExistant()
        {
            var mockFileSystemService = new Mock<IFileSystemService>();
            var vmGame = new GameViewModelHarness(1234, "Title", mockFileSystemService.Object);
            vmGame.AssociateRACacheDirectory("C:\\Emulator\\RACache\\Data");

            Assert.That(vmGame.RACacheDirectory, Is.EqualTo("C:\\Emulator\\RACache\\Data"));
            Assert.That(vmGame.Notes, Is.Not.Null);
            Assert.That(vmGame.Notes.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestLoadNotesDeleted()
        {
            var mockFileSystemService = new Mock<IFileSystemService>();
            string mockNotes =
                "[{\"User\":\"Joe\",\"Address\":\"0x000123\",\"Note\":\"Test1\"}," +
                "{\"User\":\"Joe\",\"Address\":\"0x000124\",\"Note\":\"\"}," + // deleted note is returned as empty string
                "{\"User\":\"Joe\",\"Address\":\"0x000125\",\"Note\":\"''\"}," + // ancient bug set empty notes to two single quotes
                "{\"User\":\"Joe\",\"Address\":\"0x0001AF\",\"Note\":\"Test4\"}]";
            mockFileSystemService.Setup(f => f.OpenFile("C:\\Emulator\\RACache\\Data\\1234-Notes.json", OpenFileMode.Read))
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes(mockNotes)));
            var vmGame = new GameViewModelHarness(1234, "Title", mockFileSystemService.Object);
            vmGame.AssociateRACacheDirectory("C:\\Emulator\\RACache\\Data");

            Assert.That(vmGame.RACacheDirectory, Is.EqualTo("C:\\Emulator\\RACache\\Data"));
            Assert.That(vmGame.Notes, Is.Not.Null);
            Assert.That(vmGame.Notes.Count, Is.EqualTo(2));
            Assert.That(vmGame.Notes[0x123].Note, Is.EqualTo("Test1"));
            Assert.That(vmGame.Notes[0x1AF].Note, Is.EqualTo("Test4"));
        }

        [Test]
        public void TestPopulateEditorListNull()
        {
            var vmGame = new GameViewModelHarness(1234, "Title");
            vmGame.PopulateEditorList(null);
            Assert.That(vmGame.GameId, Is.EqualTo(1234));
            Assert.That(vmGame.Title, Is.EqualTo("Title"));
            Assert.That(vmGame.Script, Is.Not.Null);
            Assert.That(vmGame.Editors.Count(), Is.EqualTo(1));
            Assert.That(vmGame.SelectedEditor, Is.SameAs(vmGame.Script));
            Assert.That(vmGame.Notes, Is.Not.Null.And.Empty);
            Assert.That(vmGame.GeneratedAchievementCount, Is.EqualTo(0));
            Assert.That(vmGame.CoreAchievementCount, Is.EqualTo(0));
            Assert.That(vmGame.CoreAchievementPoints, Is.EqualTo(0));
            Assert.That(vmGame.UnofficialAchievementCount, Is.EqualTo(0));
            Assert.That(vmGame.UnofficialAchievementPoints, Is.EqualTo(0));
            Assert.That(vmGame.LocalAchievementCount, Is.EqualTo(0));
            Assert.That(vmGame.LocalAchievementPoints, Is.EqualTo(0));
        }

        private Achievement AddGeneratedAchievement(AchievementScriptInterpreter interpreter, int id, string name)
        {
            var achievement = new Achievement
            {
                Id = id,
                Title = name,
                Description = name
            };

            interpreter.AddAchievement(achievement);
            return achievement;
        }

        private Leaderboard AddGeneratedLeaderboard(AchievementScriptInterpreter interpreter, int id, string name)
        {
            var leaderboard = new Leaderboard
            {
                //Id = id,
                Title = name,
                Start = Trigger.Deserialize("1=1"),
                Submit = Trigger.Deserialize("1=1"),
                Cancel = Trigger.Deserialize("1=1"),
                Value = Value.Deserialize("1")
            };

            interpreter.AddLeaderboard(leaderboard);
            return leaderboard;
        }

        private void AddGeneratedRichPresence(AchievementScriptInterpreter interpreter)
        {
            interpreter.RichPresence = "Display:\nTest";
        }

        [Test]
        public void TestPopulateEditorListMergeLocal()
        {
            var vmGame = new GameViewModelHarness(1234, "Title");

            var interpreter = new AchievementScriptInterpreter();
            AddGeneratedAchievement(interpreter, 65, "Test1").Points = 1;
            AddGeneratedAchievement(interpreter, 111000004, "Test2").Points = 2;
            AddGeneratedAchievement(interpreter, 0, "Test3").Points = 4;
            vmGame.AddLocalAchievement(111000004, "Test2b").Points = 8;
            vmGame.AddLocalAchievement(111000005, "A Test4").Points = 16;
            vmGame.AddLocalAchievement(0, "Test5").Points = 32;

            vmGame.PopulateEditorList(interpreter);

            Assert.That(vmGame.Editors.Count(), Is.EqualTo(6));
            var achievementList = vmGame.NavigationNodes.ElementAt(2);
            Assert.That(achievementList.Children.ElementAt(0).Label, Is.EqualTo("Test1"));
            Assert.That(achievementList.Children.ElementAt(1).Label, Is.EqualTo("Test2")); // title should reflect generated value
            Assert.That(achievementList.Children.ElementAt(2).Label, Is.EqualTo("Test3"));
            Assert.That(achievementList.Children.ElementAt(3).Label, Is.EqualTo("A Test4")); // non-generated items should appear last
            Assert.That(achievementList.Children.ElementAt(4).Label, Is.EqualTo("Test5"));

            // items without an ID will be assigned the next available local ID
            Assert.That(vmGame.Editors.OfType<AchievementViewModel>().First(a => a.Title == "Test1").Id, Is.EqualTo(65));
            Assert.That(vmGame.Editors.OfType<AchievementViewModel>().First(a => a.Title == "Test2").Id, Is.GreaterThan(AssetBase.FirstLocalId)); // generated and local (provided)
            Assert.That(vmGame.Editors.OfType<AchievementViewModel>().First(a => a.Title == "Test3").Id, Is.GreaterThan(AssetBase.FirstLocalId)); // generated (not provided)
            Assert.That(vmGame.Editors.OfType<AchievementViewModel>().First(a => a.Title == "A Test4").Id, Is.GreaterThan(AssetBase.FirstLocalId)); // from local (provided)
            Assert.That(vmGame.Editors.OfType<AchievementViewModel>().First(a => a.Title == "Test5").Id, Is.GreaterThan(AssetBase.FirstLocalId)); // local (not provided)
        }

        [Test]
        public void TestPopulateEditorListMergePublished()
        {
            var vmGame = new GameViewModelHarness(1234, "Title");

            var interpreter = new AchievementScriptInterpreter();
            AddGeneratedAchievement(interpreter, 65, "Test1").Points = 1;
            AddGeneratedAchievement(interpreter, 111000004, "Test2").Points = 2;
            AddGeneratedAchievement(interpreter, 0, "Test3").Points = 4;
            vmGame.AddPublishedAchievement(65, "Test1b").Points = 8;
            vmGame.AddPublishedAchievement(68, "Test2").Points = 16;
            var ach = vmGame.AddPublishedAchievement(72, "Test3");
            ach.Points = 32;
            ach.Category = 5;

            vmGame.PopulateEditorList(interpreter);

            Assert.That(vmGame.Editors.Count(), Is.EqualTo(4));
            var achievementList = vmGame.NavigationNodes.ElementAt(2);
            Assert.That(achievementList.Children.ElementAt(0).Label, Is.EqualTo("Test1"));
            Assert.That(achievementList.Children.ElementAt(1).Label, Is.EqualTo("Test2")); // title should reflect generated value
            Assert.That(achievementList.Children.ElementAt(2).Label, Is.EqualTo("Test3"));

            // items without an ID will be assigned the next available local ID
            Assert.That(vmGame.Editors.OfType<AchievementViewModel>().First(a => a.Title == "Test1").Id, Is.EqualTo(65));
            Assert.That(vmGame.Editors.OfType<AchievementViewModel>().First(a => a.Title == "Test2").Id, Is.GreaterThan(AssetBase.FirstLocalId)); // generated and local (provided)
            Assert.That(vmGame.Editors.OfType<AchievementViewModel>().First(a => a.Title == "Test3").Id, Is.EqualTo(72));
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
            mockFileSystemService.Setup(f => f.OpenFile("C:\\Emulator\\RACache\\Data\\1234.json", OpenFileMode.Read))
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes(mockPublished)));
            var vmGame = new GameViewModelHarness(1234, "Title", mockFileSystemService.Object);
            vmGame.AssociateRACacheDirectory("C:\\Emulator\\RACache\\Data");
            Assert.That(vmGame.Title, Is.EqualTo("GameTitle"));

            vmGame.PopulateEditorList(null);
            Assert.That(vmGame.Editors.Count(), Is.EqualTo(3));
            Assert.That(vmGame.Editors.ElementAt(0).Title, Is.EqualTo("Ach123"));
            Assert.That(vmGame.Editors.ElementAt(1).Title, Is.EqualTo("Ach234"));

            var ach123 = ((AchievementViewModel)vmGame.Editors.ElementAt(0)).Published.Asset as Achievement;
            Assert.That(ach123.Id, Is.EqualTo(123));
            Assert.That(ach123.Title, Is.EqualTo("Ach123"));
            Assert.That(ach123.Description, Is.EqualTo("Desc123"));
            Assert.That(ach123.Category, Is.EqualTo(3));
            Assert.That(ach123.Points, Is.EqualTo(6));
            Assert.That(ach123.BadgeName, Is.EqualTo("4321"));
            Assert.That(ach123.CoreRequirements.Count(), Is.EqualTo(1));
            Assert.That(ach123.CoreRequirements.First().Left.Value, Is.EqualTo(1));
            Assert.That(ach123.CoreRequirements.First().Right.Value, Is.EqualTo(3));
            Assert.That(ach123.Published, Is.EqualTo(new DateTime(2021, 07, 05, 18, 03, 33, DateTimeKind.Utc)));
            Assert.That(ach123.LastModified, Is.EqualTo(new DateTime(2021, 07, 09, 04, 44, 10, DateTimeKind.Utc)));

            var ach234 = ((AchievementViewModel)vmGame.Editors.ElementAt(1)).Published.Asset as Achievement;
            Assert.That(ach234.Id, Is.EqualTo(234));
            Assert.That(ach234.Title, Is.EqualTo("Ach234"));
            Assert.That(ach234.Description, Is.EqualTo("Desc234"));
            Assert.That(ach234.Category, Is.EqualTo(5));
            Assert.That(ach234.Points, Is.EqualTo(3));
            Assert.That(ach234.BadgeName, Is.EqualTo("5555"));
            Assert.That(ach234.CoreRequirements.Count(), Is.EqualTo(1));
            Assert.That(ach234.CoreRequirements.First().Left.Value, Is.EqualTo(2));
            Assert.That(ach234.CoreRequirements.First().Right.Value, Is.EqualTo(3));
            Assert.That(ach234.Published, Is.EqualTo(new DateTime(2021, 07, 05, 18, 03, 35, DateTimeKind.Utc)));
            Assert.That(ach234.LastModified, Is.EqualTo(new DateTime(2021, 07, 09, 04, 44, 13, DateTimeKind.Utc)));

            Assert.That(vmGame.GeneratedAchievementCount, Is.EqualTo(0));
            Assert.That(vmGame.CoreAchievementCount, Is.EqualTo(1));
            Assert.That(vmGame.CoreAchievementPoints, Is.EqualTo(6));
            Assert.That(vmGame.UnofficialAchievementCount, Is.EqualTo(1));
            Assert.That(vmGame.UnofficialAchievementPoints, Is.EqualTo(3));
            Assert.That(vmGame.LocalAchievementCount, Is.EqualTo(0));
            Assert.That(vmGame.LocalAchievementPoints, Is.EqualTo(0));
        }
    }
}
