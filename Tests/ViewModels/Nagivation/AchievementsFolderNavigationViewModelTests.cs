using Jamiras.Components;
using Jamiras.Services;
using Moq;
using NUnit.Framework;
using RATools.Data;
using RATools.Services;
using RATools.ViewModels;
using RATools.ViewModels.Navigation;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Tests.ViewModels.Nagivation
{
    [TestFixture]
    class AchievementsFolderNavigationViewModelTests
    {
        [OneTimeSetUp]
        public void FixtureSetup()
        {
            ServiceRepository.Reset();

            var mockSettings = new Mock<ISettings>();
            mockSettings.Setup(s => s.HexValues).Returns(true);
            ServiceRepository.Instance.RegisterInstance<ISettings>(mockSettings.Object);
        }

        private class MockGameViewModel : GameViewModel
        {
            public MockGameViewModel()
                : base(99, "Game", new Mock<ILogger>().Object, new Mock<IFileSystemService>().Object, new Mock<ISettings>().Object)
            {
                SerializationContext = new SerializationContext();
            }
        }

        [Test]
        public void TestInitialState()
        {
            var game = new MockGameViewModel();
            var set = new AchievementSet { Id = 2, Title = "Set2", BadgeName = "01234" };
            var harness = new AchievementsFolderNavigationViewModel(game, set);

            Assert.That(harness.ImageName, Is.EqualTo("folder"));
            Assert.That(harness.ImageResourcePath, Is.EqualTo("/RATools;component/Resources/folder.png"));
            Assert.That(harness.ImageTooltip, Is.EqualTo("Folder"));

            Assert.That(harness.Label, Is.EqualTo("Achievements"));

            Assert.That(harness.ModificationMessage, Is.Null);
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.Same));

            Assert.That(harness.IsExpanded, Is.True);
            Assert.That(harness.Children, Is.Not.Null);
            Assert.That(harness.Children.Count(), Is.EqualTo(0));

            Assert.That(harness.ContextMenu, Is.Null);
        }

        [Test]
        public void TestEditorInitializedOnSelect()
        {
            var game = new MockGameViewModel();
            var set = new AchievementSet { Id = 2, Title = "Set2", BadgeName = "01234" };
            var harness = new AchievementsFolderNavigationViewModel(game, set);

            var achievement1ViewModel = new AchievementViewModel(game);
            achievement1ViewModel.Published.Asset = new Achievement
            {
                Points = 5,
                Category = 3,
            };
            achievement1ViewModel.Refresh();
            harness.AddChild(new AchievementNavigationViewModel(achievement1ViewModel));

            Assert.That(harness.Editor, Is.Not.Null.And.InstanceOf<AchievementsListViewModel>());
            var editor = (AchievementsListViewModel)harness.Editor;
            Assert.That(editor.Achievements.Count(), Is.EqualTo(0));

            harness.IsSelected = true;
            Assert.That(editor.Achievements.Count(), Is.EqualTo(1));
            Assert.That(editor.Achievements.First(), Is.SameAs(achievement1ViewModel));
        }
    }
}
