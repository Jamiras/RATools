using Jamiras.Components;
using Jamiras.Services;
using Moq;
using NUnit.Framework;
using RATools.Data;
using RATools.Services;
using RATools.ViewModels;
using RATools.ViewModels.Navigation;
using System;
using System.Linq;

namespace RATools.Tests.ViewModels
{
    [TestFixture]
    class AchievementListViewModelTests
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
        public void TestInitialization()
        {
            var game = new MockGameViewModel();
            var set = new AchievementSet { Id = 2, Title = "Set2", BadgeName = "01234" };

            var listViewModel = new AchievementsListViewModel(game, set);
            Assert.That(listViewModel.Title, Is.EqualTo("Set2"));
            Assert.That(listViewModel.ViewerType, Is.EqualTo("AchievementList"));
            Assert.That(listViewModel.ViewerId, Is.EqualTo(2));
            Assert.That(listViewModel.Achievements, Is.Not.Null);
            Assert.That(listViewModel.Achievements.Count(), Is.EqualTo(0));
            Assert.That(listViewModel.Points, Is.EqualTo(0));
            Assert.That(listViewModel.PointsSummary, Is.EqualTo(""));
        }

        [Test]
        public void TestRefreshPromotedOnly()
        {
            var game = new MockGameViewModel();
            var set = new AchievementSet { Id = 2, Title = "Set2", BadgeName = "01234" };
            var listViewModel = new AchievementsListViewModel(game, set);

            var achievement1ViewModel = new AchievementViewModel(game);
            achievement1ViewModel.Published.Asset = new Achievement
            {
                Points = 5,
                Category = 3,
            };
            achievement1ViewModel.Refresh();

            var achievement2ViewModel = new AchievementViewModel(game);
            achievement2ViewModel.Published.Asset = new Achievement
            {
                Points = 10,
                Category = 3,
            };
            achievement2ViewModel.Refresh();

            listViewModel.Refresh(set, new[]
            {
                new AchievementNavigationViewModel(achievement1ViewModel),
                new AchievementNavigationViewModel(achievement2ViewModel),
            });

            Assert.That(listViewModel.Achievements.Count(), Is.EqualTo(2));
            Assert.That(listViewModel.Achievements.First(), Is.SameAs(achievement1ViewModel));
            Assert.That(listViewModel.Achievements.ElementAt(1), Is.SameAs(achievement2ViewModel));

            Assert.That(listViewModel.Points, Is.EqualTo(15));
            Assert.That(listViewModel.Description, Is.EqualTo("2 achievements"));
            Assert.That(listViewModel.PointsSummary, Is.EqualTo("15 points"));
        }

        [Test]
        public void TestRefreshLocalOnly()
        {
            var game = new MockGameViewModel();
            var set = new AchievementSet { Id = 2, Title = "Set2", BadgeName = "01234" };
            var listViewModel = new AchievementsListViewModel(game, set);

            var achievement1ViewModel = new AchievementViewModel(game);
            achievement1ViewModel.Local.Asset = new Achievement
            {
                Points = 5,
            };
            achievement1ViewModel.Refresh();

            var achievement2ViewModel = new AchievementViewModel(game);
            achievement2ViewModel.Local.Asset = new Achievement
            {
                Points = 10,
            };
            achievement2ViewModel.Refresh();

            listViewModel.Refresh(set, new[]
            {
                new AchievementNavigationViewModel(achievement1ViewModel),
                new AchievementNavigationViewModel(achievement2ViewModel),
            });

            Assert.That(listViewModel.Achievements.Count(), Is.EqualTo(2));
            Assert.That(listViewModel.Achievements.First(), Is.SameAs(achievement1ViewModel));
            Assert.That(listViewModel.Achievements.ElementAt(1), Is.SameAs(achievement2ViewModel));

            Assert.That(listViewModel.Points, Is.EqualTo(15));
            Assert.That(listViewModel.Description, Is.EqualTo("2 achievements"));
            Assert.That(listViewModel.PointsSummary, Is.EqualTo("15 points"));
        }

        [Test]
        public void TestRefreshPublishedAndLocal()
        {
            var game = new MockGameViewModel();
            var set = new AchievementSet { Id = 2, Title = "Set2", BadgeName = "01234" };
            var listViewModel = new AchievementsListViewModel(game, set);

            var achievement1ViewModel = new AchievementViewModel(game);
            achievement1ViewModel.Published.Asset = new Achievement
            {
                Points = 5,
                Category = 3,
            };
            achievement1ViewModel.Refresh();

            var achievement2ViewModel = new AchievementViewModel(game);
            achievement2ViewModel.Local.Asset = new Achievement
            {
                Points = 10,
            };
            achievement2ViewModel.Refresh();

            listViewModel.Refresh(set, new[]
            {
                new AchievementNavigationViewModel(achievement1ViewModel),
                new AchievementNavigationViewModel(achievement2ViewModel),
            });

            Assert.That(listViewModel.Achievements.Count(), Is.EqualTo(2));
            Assert.That(listViewModel.Achievements.First(), Is.SameAs(achievement1ViewModel));
            Assert.That(listViewModel.Achievements.ElementAt(1), Is.SameAs(achievement2ViewModel));

            Assert.That(listViewModel.Points, Is.EqualTo(15));
            Assert.That(listViewModel.Description, Is.EqualTo("2 achievements (1 promoted, 1 local)"));
            Assert.That(listViewModel.PointsSummary, Is.EqualTo("15 points (5 promoted, 10 local)"));
        }
    }
}
