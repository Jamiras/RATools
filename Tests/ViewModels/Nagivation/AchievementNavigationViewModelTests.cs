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
    class AchievementNavigationViewModelTests
    {
        [Test]
        public void TestInitialState()
        {
            var game = new GameViewModel(1, "Game", new Mock<ILogger>().Object, new Mock<IFileSystemService>().Object);
            var achievement = new AchievementViewModel(game);
            var harness = new AchievementNavigationViewModel(achievement);
            Assert.That(harness.ImageName, Is.EqualTo("achievement"));
            Assert.That(harness.ImageResourcePath, Is.EqualTo("/RATools;component/Resources/achievement.png"));
            Assert.That(harness.ImageTooltip, Is.EqualTo("Achievement"));

            Assert.That(harness.Label, Is.EqualTo(""));

            Assert.That(harness.ModificationMessage, Is.Null);
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.Same));

            Assert.That(harness.IsExpanded, Is.True);
            Assert.That(harness.Children, Is.Null);

            Assert.That(harness.ContextMenu, Is.Not.Null);
            Assert.That(harness.ContextMenu.Count(), Is.EqualTo(1));
            var menuItem = harness.ContextMenu.First();
            Assert.That(menuItem.Label, Is.EqualTo("Update Local"));
            Assert.That(menuItem.Command.CanExecute(null), Is.False);
        }

        [Test]
        public void TestPointsFromEditor()
        {
            ServiceRepository.Reset();
            ServiceRepository.Instance.RegisterInstance(new Mock<ISettings>().Object);

            var game = new GameViewModel(1, "Game", new Mock<ILogger>().Object, new Mock<IFileSystemService>().Object);
            var achievement = new Achievement { Points = 5 };
            var achievementViewModel = new AchievementViewModel(game);
            achievementViewModel.Generated.Asset = achievement;
            achievementViewModel.Refresh();
            Assert.That(achievementViewModel.Points, Is.EqualTo(5));

            var harness = new AchievementNavigationViewModel(achievementViewModel);
            var changedProperties = new List<string>();
            harness.PropertyChanged += (o, e) => changedProperties.Add(e.PropertyName);

            Assert.That(harness.Points, Is.EqualTo(5));

            achievement.Points = 10;
            achievementViewModel.Generated.Asset = achievement; // force sync
            Assert.That(harness.Points, Is.EqualTo(10));
            Assert.That(changedProperties, Has.Member("Points").And.Count.EqualTo(1));
        }

        [Test]
        public void TestAchievementTypeFromEditor()
        {
            ServiceRepository.Reset();
            ServiceRepository.Instance.RegisterInstance(new Mock<ISettings>().Object);

            var game = new GameViewModel(1, "Game", new Mock<ILogger>().Object, new Mock<IFileSystemService>().Object);
            var achievement = new Achievement { Type = AchievementType.Progression };
            var achievementViewModel = new AchievementViewModel(game);
            achievementViewModel.Generated.Asset = achievement;
            achievementViewModel.Refresh();
            Assert.That(achievementViewModel.AchievementType, Is.EqualTo(AchievementType.Progression));

            var harness = new AchievementNavigationViewModel(achievementViewModel);
            var changedProperties = new List<string>();
            harness.PropertyChanged += (o, e) => changedProperties.Add(e.PropertyName);

            Assert.That(harness.AchievementType, Is.EqualTo(AchievementType.Progression));
            Assert.That(harness.TypeImage, Is.EqualTo("/RATools;component/Resources/progression.png"));

            achievement.Type = AchievementType.WinCondition;
            achievementViewModel.Refresh(); // force sync
            Assert.That(harness.AchievementType, Is.EqualTo(AchievementType.WinCondition));
            Assert.That(harness.TypeImage, Is.EqualTo("/RATools;component/Resources/win-condition.png"));
            Assert.That(changedProperties, Has.Member("AchievementType").And.Member("TypeImage").And.Count.EqualTo(2));
        }
    }
}
