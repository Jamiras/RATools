using Jamiras.Components;
using Jamiras.Services;
using Moq;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Services;
using RATools.ViewModels;

namespace RATools.Tests.ViewModels
{
    [TestFixture]
    class AchievementViewModelTests
    {
        private string _clipboardString;

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            ServiceRepository.Reset();

            var mockSettings = new Mock<ISettings>();
            mockSettings.Setup(s => s.HexValues).Returns(true);
            ServiceRepository.Instance.RegisterInstance<ISettings>(mockSettings.Object);

            var mockClipboardService = new Mock<IClipboardService>();
            mockClipboardService.Setup(c => c.SetData(It.IsAny<string>())).Callback((string s) => _clipboardString = s);
            ServiceRepository.Instance.RegisterInstance<IClipboardService>(mockClipboardService.Object);
        }

        [OneTimeTearDown]
        public void FixtureTeardown()
        {
            ServiceRepository.Reset();
        }

        private class MockGameViewModel : GameViewModel
        {
            public MockGameViewModel()
                : base(99, "Game", new Mock<ILogger>().Object, new Mock<IFileSystemService>().Object)
            {
                SerializationContext = new SerializationContext();
            }
        }

        [Test]
        public void TestInitialization()
        {
            var vmAchievement = new AchievementViewModel(new MockGameViewModel());
            Assert.That(vmAchievement.ViewerType, Is.EqualTo("Achievement"));
        }


        [Test]
        public void TestCopyDefinitionToClipboard()
        {
            var input = "R:0x1234=6_0xh2345<6S0x5555!=d0x5555S0x4444=9";
            var builder = new AchievementBuilder();
            builder.ParseRequirements(Tokenizer.CreateTokenizer(input));
            var achievement = new Achievement();
            achievement.Trigger = builder.ToTrigger();
            var vmAchievement = new AchievementViewModel(new MockGameViewModel());
            vmAchievement.Generated.Asset = achievement;

            _clipboardString = null;
            vmAchievement.CopyDefinitionToClipboardCommand.Execute();
            Assert.That(_clipboardString, Is.EqualTo("R:0x 001234=6_0xH002345<6S0x 005555!=d0x 005555S0x 004444=9"));
        }
    }
}
