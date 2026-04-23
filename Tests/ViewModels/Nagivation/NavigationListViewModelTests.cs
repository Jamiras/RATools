using Jamiras.Components;
using Jamiras.Services;
using Moq;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using RATools.Services;
using RATools.ViewModels;
using RATools.ViewModels.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Tests.ViewModels.Nagivation
{
    [TestFixture]
    class NavigationListViewModelTests
    {
        private class NavigationListViewModelHarness
        {
            public NavigationListViewModelHarness()
            {
                _fileSystemService = new Mock<IFileSystemService>();

                _backgroundWorkerService = new Mock<IBackgroundWorkerService>();
                _backgroundWorkerService.Setup(b => b.InvokeOnUiThread(It.IsAny<Action>())).Callback((Action a) => a());

                _editors = new List<ViewerViewModelBase>();

                ServiceRepository.Reset();
                ServiceRepository.Instance.RegisterInstance(new Mock<ISettings>().Object);
            }

            public void Initialize()
            {
                if (Game == null)
                {
                    _publishedAssets = new PublishedAssets("1234.json", _fileSystemService.Object);
                    _localAssets = new LocalAssets("1234-User.txt", _fileSystemService.Object);

                    Game = new MockGameViewModel(_fileSystemService.Object, _publishedAssets, _localAssets);
                    Game.SetValue(GameViewModel.NavigationNodesProperty, NavigationNodes);
                }
            }

            public void InitializeSubsets()
            {
                Initialize();

                var sets = (List<AchievementSet>)_publishedAssets.Sets;
                sets.Add(new AchievementSet { Id = 1111, Title = Game.Title, Type = AchievementSetType.Core });
                sets.Add(new AchievementSet { Id = 2222, Title = "Bonus", Type = AchievementSetType.Bonus });
            }

            public void Merge(AchievementScriptInterpreter interpreter)
            {
                Initialize();

                var viewModel = new NavigationListViewModel(Game, _publishedAssets, _localAssets, _editors, _backgroundWorkerService.Object);
                NavigationNodes = viewModel.Merge(interpreter).ToList();
            }

            public Achievement CreateAchievement(string title, int points = 5, AchievementType type = AchievementType.None)
            {
                var achievement = new Achievement();
                achievement.Title = title;
                achievement.Points = points;
                achievement.Type = type;
                return achievement;
            }

            public Achievement AddLocalAchievement(string title, int points = 5, AchievementType type = AchievementType.None)
            {
                Initialize();

                var achievement = CreateAchievement(title, points, type);
                ((List<Achievement>)_localAssets.Achievements).Add(achievement);
                return achievement;
            }

            public Achievement AddPublishedAchievement(string title, int points = 5, AchievementType type = AchievementType.None)
            {
                Initialize();

                var achievement = CreateAchievement(title, points, type);
                ((List<Achievement>)_publishedAssets.Achievements).Add(achievement);
                return achievement;
            }

            private readonly Mock<IFileSystemService> _fileSystemService;
            private readonly Mock<IBackgroundWorkerService> _backgroundWorkerService;
            public MockGameViewModel Game { get; private set; }
            private PublishedAssets _publishedAssets;
            private LocalAssets _localAssets;
            private readonly List<ViewerViewModelBase> _editors;

            public List<NavigationViewModelBase> NavigationNodes { get; private set; }
        }

        class MockScriptViewModel : ScriptViewModel
        {
            public MockScriptViewModel()
                : base()
            {
            }
        }

        class MockGameViewModel : GameViewModel
        {
            public MockGameViewModel(IFileSystemService fileSystemService, PublishedAssets publishedAssets, LocalAssets localAssets)
                : base(1234, "Game Title", new Mock<ILogger>().Object, fileSystemService)
            {
                SetRACacheDirectory("C:\\RACache\\");

                _publishedAssets = publishedAssets;
                _localAssets = localAssets;
            }

            public void InitScript(string filename)
            {
                Script = new MockScriptViewModel();
                Script.Filename = filename;
            }
        }

        [Test]
        public void TestEmpty()
        {
            var harness = new NavigationListViewModelHarness();
            harness.Merge(null);

            Assert.AreEqual(4, harness.NavigationNodes.Count);
            Assert.AreEqual("Script", harness.NavigationNodes[0].Label);
            Assert.AreEqual("Rich Presence", harness.NavigationNodes[1].Label);
            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Label);
            Assert.AreEqual("Leaderboards", harness.NavigationNodes[3].Label);
            Assert.AreEqual(0, harness.NavigationNodes[0].Children?.Count ?? 0);
            Assert.AreEqual(0, harness.NavigationNodes[1].Children?.Count ?? 0);
            Assert.AreEqual(0, harness.NavigationNodes[2].Children?.Count ?? 0);
            Assert.AreEqual(0, harness.NavigationNodes[3].Children?.Count ?? 0);
        }

        [Test]
        public void TestMergeScript()
        {
            var harness = new NavigationListViewModelHarness();
            harness.Initialize();
            harness.Game.InitScript("test.rascript");
            harness.Merge(null);

            Assert.AreEqual("Script", harness.NavigationNodes[0].Label);
            Assert.AreEqual(1, harness.NavigationNodes[0].Children?.Count ?? 0);

            var scriptNode = harness.NavigationNodes[0].Children[0] as ScriptNavigationViewModel;
            Assert.IsNotNull(scriptNode);
            Assert.AreEqual("test.rascript", scriptNode.Label);
            Assert.AreSame(harness.Game.Script, scriptNode.Editor);
            Assert.AreEqual(GeneratedCompareState.Same, scriptNode.CompareState);
            Assert.IsNull(scriptNode.ModificationMessage);
            Assert.IsNull(scriptNode.ContextMenu);
        }

        [Test]
        public void TestMergeLocalAchievement()
        {
            var harness = new NavigationListViewModelHarness();
            var achievement = harness.AddLocalAchievement("Test Achievement");

            // if generated assets aren't provided, local assets shouldn't be loaded. this prevents them
            // from appearing as "Local, but not generated" while the script is initially being processed.
            harness.Merge(null);
            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Label);
            Assert.AreEqual(0, harness.NavigationNodes[2].Children?.Count ?? 0);

            // when generated assets are provided, local assets should be loaded.
            harness.Merge(new AchievementScriptInterpreter());
            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Label);
            Assert.AreEqual(1, harness.NavigationNodes[2].Children?.Count ?? 0);

            var achievementNode = harness.NavigationNodes[2].Children[0] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement", achievementNode.Label);
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(achievement, ((AchievementViewModel)achievementNode.Editor).Local.Asset);
            Assert.AreEqual(GeneratedCompareState.NotGenerated, achievementNode.CompareState);
            Assert.AreEqual("Local asset is not generated", achievementNode.ModificationMessage);

            Assert.IsNotNull(achievementNode.ContextMenu);
            Assert.AreEqual(1, achievementNode.ContextMenu.Count());
            var menuItem = achievementNode.ContextMenu.First();
            Assert.AreEqual("Update Local", menuItem.Label);
            Assert.IsFalse(menuItem.Command.CanExecute(null));
        }

        [Test]
        public void TestMergePublishedAchievement()
        {
            var harness = new NavigationListViewModelHarness();
            var achievement = harness.AddPublishedAchievement("Test Achievement");
            harness.Merge(null);

            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Label);
            Assert.AreEqual(1, harness.NavigationNodes[2].Children?.Count ?? 0);

            var achievementNode = harness.NavigationNodes[2].Children[0] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement", achievementNode.Label);
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(achievement, ((AchievementViewModel)achievementNode.Editor).Published.Asset);
            Assert.AreEqual(GeneratedCompareState.None, achievementNode.CompareState);
            Assert.IsNull(achievementNode.ModificationMessage);

            Assert.IsNotNull(achievementNode.ContextMenu);
            Assert.AreEqual(1, achievementNode.ContextMenu.Count());
            var menuItem = achievementNode.ContextMenu.First();
            Assert.AreEqual("Update Local", menuItem.Label);
            Assert.IsFalse(menuItem.Command.CanExecute(null));
        }

        [Test]
        public void TestMergeLocalAndPublishedAchievementIdentical()
        {
            var harness = new NavigationListViewModelHarness();
            var publishedAchievement = harness.AddPublishedAchievement("Test Achievement");
            publishedAchievement.Id = 12345;
            var localAchievement = harness.AddLocalAchievement("Test Achievement");
            localAchievement.Id = 12345;
            harness.Merge(new AchievementScriptInterpreter());

            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Label);
            Assert.AreEqual(1, harness.NavigationNodes[2].Children?.Count ?? 0);

            var achievementNode = harness.NavigationNodes[2].Children[0] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement", achievementNode.Label);
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(publishedAchievement, ((AchievementViewModel)achievementNode.Editor).Published.Asset);
            Assert.AreSame(localAchievement, ((AchievementViewModel)achievementNode.Editor).Local.Asset);
            Assert.AreEqual(GeneratedCompareState.None, achievementNode.CompareState);
            Assert.IsNull(achievementNode.ModificationMessage);

            Assert.IsNotNull(achievementNode.ContextMenu);
            Assert.AreEqual(1, achievementNode.ContextMenu.Count());
            var menuItem = achievementNode.ContextMenu.First();
            Assert.AreEqual("Update Local", menuItem.Label);
            Assert.IsFalse(menuItem.Command.CanExecute(null));
        }

        [Test]
        public void TestMergeLocalAndPublishedAchievementSameId()
        {
            var harness = new NavigationListViewModelHarness();
            var publishedAchievement = harness.AddPublishedAchievement("Test Achievement 1");
            publishedAchievement.Id = 12345;
            var localAchievement = harness.AddLocalAchievement("Test Achievement 2");
            localAchievement.Id = 12345;
            harness.Merge(new AchievementScriptInterpreter());

            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Label);
            Assert.AreEqual(1, harness.NavigationNodes[2].Children?.Count ?? 0);

            var achievementNode = harness.NavigationNodes[2].Children[0] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement 1", achievementNode.Label); // should use published label over local
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(publishedAchievement, ((AchievementViewModel)achievementNode.Editor).Published.Asset);
            Assert.AreSame(localAchievement, ((AchievementViewModel)achievementNode.Editor).Local.Asset);
            Assert.AreEqual(GeneratedCompareState.None, achievementNode.CompareState);
            Assert.IsNull(achievementNode.ModificationMessage);

            Assert.IsNotNull(achievementNode.ContextMenu);
            Assert.AreEqual(1, achievementNode.ContextMenu.Count());
            var menuItem = achievementNode.ContextMenu.First();
            Assert.AreEqual("Update Local", menuItem.Label);
            Assert.IsFalse(menuItem.Command.CanExecute(null));
        }

        [Test]
        public void TestMergeLocalAndPublishedAchievementDifferentId()
        {
            var harness = new NavigationListViewModelHarness();
            var publishedAchievement = harness.AddPublishedAchievement("Test Achievement 1");
            publishedAchievement.Id = 12345;
            var localAchievement = harness.AddLocalAchievement("Test Achievement 2");
            localAchievement.Id = 12346;
            harness.Merge(new AchievementScriptInterpreter());

            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Label);
            Assert.AreEqual(2, harness.NavigationNodes[2].Children?.Count ?? 0);

            // local should be first
            var achievementNode = harness.NavigationNodes[2].Children[0] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement 2", achievementNode.Label);
            Assert.IsNotNull(achievementNode.Editor);
            Assert.IsNull(((AchievementViewModel)achievementNode.Editor).Published.Asset);
            Assert.AreSame(localAchievement, ((AchievementViewModel)achievementNode.Editor).Local.Asset);
            Assert.AreEqual(GeneratedCompareState.NotGenerated, achievementNode.CompareState);
            Assert.AreEqual("Local asset is not generated", achievementNode.ModificationMessage);

            achievementNode = harness.NavigationNodes[2].Children[1] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement 1", achievementNode.Label);
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(publishedAchievement, ((AchievementViewModel)achievementNode.Editor).Published.Asset);
            Assert.IsNull(((AchievementViewModel)achievementNode.Editor).Local.Asset);
            Assert.AreEqual(GeneratedCompareState.None, achievementNode.CompareState);
            Assert.IsNull(achievementNode.ModificationMessage);
        }

        [Test]
        public void TestMergeGeneratedAchievement()
        {
            var harness = new NavigationListViewModelHarness();
            var achievement = harness.CreateAchievement("Test Achievement");
            var interpreter = new AchievementScriptInterpreter();
            interpreter.AddAchievement(achievement);
            harness.Merge(interpreter);

            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Label);
            Assert.AreEqual(1, harness.NavigationNodes[2].Children?.Count ?? 0);

            var achievementNode = harness.NavigationNodes[2].Children[0] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement", achievementNode.Label);
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(achievement, ((AchievementViewModel)achievementNode.Editor).Generated.Asset);
            Assert.AreEqual(GeneratedCompareState.GeneratedOnly, achievementNode.CompareState);
            Assert.AreEqual("Generated only", achievementNode.ModificationMessage);

            Assert.IsNotNull(achievementNode.ContextMenu);
            Assert.AreEqual(1, achievementNode.ContextMenu.Count());
            var menuItem = achievementNode.ContextMenu.First();
            Assert.AreEqual("Update Local", menuItem.Label);
            Assert.IsTrue(menuItem.Command.CanExecute(null));
        }

        [Test]
        public void TestMergeLocalAndGeneratedAchievementIdentical()
        {
            var harness = new NavigationListViewModelHarness();
            var generatedAchievement = harness.CreateAchievement("Test Achievement");
            var localAchievement = harness.AddLocalAchievement("Test Achievement");
            localAchievement.Id = 12345;
            var interpreter = new AchievementScriptInterpreter();
            interpreter.AddAchievement(generatedAchievement);
            harness.Merge(interpreter);

            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Label);
            Assert.AreEqual(1, harness.NavigationNodes[2].Children?.Count ?? 0);

            var achievementNode = harness.NavigationNodes[2].Children[0] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement", achievementNode.Label);
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(generatedAchievement, ((AchievementViewModel)achievementNode.Editor).Generated.Asset);
            Assert.AreSame(localAchievement, ((AchievementViewModel)achievementNode.Editor).Local.Asset);
            Assert.AreEqual(GeneratedCompareState.Same, achievementNode.CompareState);
            Assert.IsNull(achievementNode.ModificationMessage);

            Assert.IsNotNull(achievementNode.ContextMenu);
            Assert.AreEqual(1, achievementNode.ContextMenu.Count());
            var menuItem = achievementNode.ContextMenu.First();
            Assert.AreEqual("Update Local", menuItem.Label);
            Assert.IsFalse(menuItem.Command.CanExecute(null));
        }

        [Test]
        public void TestMergeLocalAndGeneratedAchievementSameId()
        {
            var harness = new NavigationListViewModelHarness();
            var generatedAchievement = harness.CreateAchievement("Test Achievement 1");
            generatedAchievement.Id = 12345;
            var localAchievement = harness.AddLocalAchievement("Test Achievement 2");
            localAchievement.Id = 12345;
            var interpreter = new AchievementScriptInterpreter();
            interpreter.AddAchievement(generatedAchievement);
            harness.Merge(interpreter);

            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Label);
            Assert.AreEqual(1, harness.NavigationNodes[2].Children?.Count ?? 0);

            var achievementNode = harness.NavigationNodes[2].Children[0] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement 1", achievementNode.Label); // generated title should be used over local title
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(generatedAchievement, ((AchievementViewModel)achievementNode.Editor).Generated.Asset);
            Assert.AreSame(localAchievement, ((AchievementViewModel)achievementNode.Editor).Local.Asset);
            Assert.AreEqual(GeneratedCompareState.LocalDiffers, achievementNode.CompareState);
            Assert.AreEqual("Generated asset differs from unpublished", achievementNode.ModificationMessage);

            Assert.IsNotNull(achievementNode.ContextMenu);
            Assert.AreEqual(1, achievementNode.ContextMenu.Count());
            var menuItem = achievementNode.ContextMenu.First();
            Assert.AreEqual("Update Local", menuItem.Label);
            Assert.IsTrue(menuItem.Command.CanExecute(null));
        }

        [Test]
        public void TestMergeLocalAndGeneratedAchievementDifferentId()
        {
            var harness = new NavigationListViewModelHarness();
            var generatedAchievement = harness.CreateAchievement("Test Achievement 1");
            generatedAchievement.Id = 12346;
            var localAchievement = harness.AddLocalAchievement("Test Achievement 2");
            localAchievement.Id = 12345;
            var interpreter = new AchievementScriptInterpreter();
            interpreter.AddAchievement(generatedAchievement);
            harness.Merge(interpreter);

            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Label);
            Assert.AreEqual(2, harness.NavigationNodes[2].Children?.Count ?? 0);

            // generated achievement should be first
            var achievementNode = harness.NavigationNodes[2].Children[0] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement 1", achievementNode.Label);
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(generatedAchievement, ((AchievementViewModel)achievementNode.Editor).Generated.Asset);
            Assert.IsNull(((AchievementViewModel)achievementNode.Editor).Local.Asset);
            Assert.AreEqual(GeneratedCompareState.GeneratedOnly, achievementNode.CompareState);
            Assert.AreEqual("Generated only", achievementNode.ModificationMessage);

            Assert.IsNotNull(achievementNode.ContextMenu);
            Assert.AreEqual(1, achievementNode.ContextMenu.Count());
            var menuItem = achievementNode.ContextMenu.First();
            Assert.AreEqual("Update Local", menuItem.Label);
            Assert.IsTrue(menuItem.Command.CanExecute(null));

            achievementNode = harness.NavigationNodes[2].Children[1] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement 2", achievementNode.Label);
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(localAchievement, ((AchievementViewModel)achievementNode.Editor).Local.Asset);
            Assert.IsNull(((AchievementViewModel)achievementNode.Editor).Generated.Asset);
            Assert.AreEqual(GeneratedCompareState.NotGenerated, achievementNode.CompareState);
            Assert.AreEqual("Local asset is not generated", achievementNode.ModificationMessage);

            Assert.IsNotNull(achievementNode.ContextMenu);
            Assert.AreEqual(1, achievementNode.ContextMenu.Count());
            menuItem = achievementNode.ContextMenu.First();
            Assert.AreEqual("Update Local", menuItem.Label);
            Assert.IsFalse(menuItem.Command.CanExecute(null));
        }

        [Test]
        public void TestMergePublishedAndGeneratedAchievementIdentical()
        {
            var harness = new NavigationListViewModelHarness();
            var generatedAchievement = harness.CreateAchievement("Test Achievement");
            var publishedAchievement = harness.AddPublishedAchievement("Test Achievement");
            publishedAchievement.Id = 12345;
            var interpreter = new AchievementScriptInterpreter();
            interpreter.AddAchievement(generatedAchievement);
            harness.Merge(interpreter);

            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Label);
            Assert.AreEqual(1, harness.NavigationNodes[2].Children?.Count ?? 0);

            var achievementNode = harness.NavigationNodes[2].Children[0] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement", achievementNode.Label);
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(generatedAchievement, ((AchievementViewModel)achievementNode.Editor).Generated.Asset);
            Assert.AreSame(publishedAchievement, ((AchievementViewModel)achievementNode.Editor).Published.Asset);
            Assert.AreEqual(GeneratedCompareState.Same, achievementNode.CompareState);
            Assert.IsNull(achievementNode.ModificationMessage);

            Assert.IsNotNull(achievementNode.ContextMenu);
            Assert.AreEqual(1, achievementNode.ContextMenu.Count());
            var menuItem = achievementNode.ContextMenu.First();
            Assert.AreEqual("Update Local", menuItem.Label);
            Assert.IsFalse(menuItem.Command.CanExecute(null));
        }

        [Test]
        public void TestMergePublishedAndGeneratedAchievementSameId()
        {
            var harness = new NavigationListViewModelHarness();
            var generatedAchievement = harness.CreateAchievement("Test Achievement 1");
            generatedAchievement.Id = 12345;
            var publishedAchievement = harness.AddPublishedAchievement("Test Achievement 2");
            publishedAchievement.Id = 12345;
            var interpreter = new AchievementScriptInterpreter();
            interpreter.AddAchievement(generatedAchievement);
            harness.Merge(interpreter);

            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Label);
            Assert.AreEqual(1, harness.NavigationNodes[2].Children?.Count ?? 0);

            var achievementNode = harness.NavigationNodes[2].Children[0] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement 1", achievementNode.Label); // generated title should be used over local title
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(generatedAchievement, ((AchievementViewModel)achievementNode.Editor).Generated.Asset);
            Assert.AreSame(publishedAchievement, ((AchievementViewModel)achievementNode.Editor).Published.Asset);
            Assert.AreEqual(GeneratedCompareState.LocalDiffers, achievementNode.CompareState);
            Assert.AreEqual("Generated asset differs from unpublished", achievementNode.ModificationMessage);

            Assert.IsNotNull(achievementNode.ContextMenu);
            Assert.AreEqual(1, achievementNode.ContextMenu.Count());
            var menuItem = achievementNode.ContextMenu.First();
            Assert.AreEqual("Update Local", menuItem.Label);
            Assert.IsTrue(menuItem.Command.CanExecute(null));
        }

        [Test]
        public void TestMergePublishedAndGeneratedAchievementDifferentId()
        {
            var harness = new NavigationListViewModelHarness();
            var generatedAchievement = harness.CreateAchievement("Test Achievement 1");
            generatedAchievement.Id = 12346;
            var publishedAchievement = harness.AddPublishedAchievement("Test Achievement 2");
            publishedAchievement.Id = 12345;
            var interpreter = new AchievementScriptInterpreter();
            interpreter.AddAchievement(generatedAchievement);
            harness.Merge(interpreter);

            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Label);
            Assert.AreEqual(2, harness.NavigationNodes[2].Children?.Count ?? 0);

            // generated achievement should be first
            var achievementNode = harness.NavigationNodes[2].Children[0] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement 1", achievementNode.Label);
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(generatedAchievement, ((AchievementViewModel)achievementNode.Editor).Generated.Asset);
            Assert.IsNull(((AchievementViewModel)achievementNode.Editor).Published.Asset);
            Assert.AreEqual(GeneratedCompareState.GeneratedOnly, achievementNode.CompareState);
            Assert.AreEqual("Generated only", achievementNode.ModificationMessage);

            Assert.IsNotNull(achievementNode.ContextMenu);
            Assert.AreEqual(1, achievementNode.ContextMenu.Count());
            var menuItem = achievementNode.ContextMenu.First();
            Assert.AreEqual("Update Local", menuItem.Label);
            Assert.IsTrue(menuItem.Command.CanExecute(null));

            achievementNode = harness.NavigationNodes[2].Children[1] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement 2", achievementNode.Label);
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(publishedAchievement, ((AchievementViewModel)achievementNode.Editor).Published.Asset);
            Assert.IsNull(((AchievementViewModel)achievementNode.Editor).Generated.Asset);
            Assert.AreEqual(GeneratedCompareState.None, achievementNode.CompareState);
            Assert.IsNull(achievementNode.ModificationMessage);

            Assert.IsNotNull(achievementNode.ContextMenu);
            Assert.AreEqual(1, achievementNode.ContextMenu.Count());
            menuItem = achievementNode.ContextMenu.First();
            Assert.AreEqual("Update Local", menuItem.Label);
            Assert.IsFalse(menuItem.Command.CanExecute(null));
        }


        [Test]
        public void TestMergeSubsetGeneratedAchievement()
        {
            var harness = new NavigationListViewModelHarness();
            var achievement = harness.CreateAchievement("Test Achievement");
            achievement.OwnerSetId = 2222;
            harness.InitializeSubsets();

            var interpreter = new AchievementScriptInterpreter();
            interpreter.AddAchievement(achievement);
            harness.Merge(interpreter);

            Assert.AreEqual("Game Title", harness.NavigationNodes[2].Label);
            Assert.AreEqual(2, harness.NavigationNodes[2].Children?.Count ?? 0);
            Assert.AreEqual("Achievements", harness.NavigationNodes[2].Children[0].Label);
            Assert.AreEqual(0, harness.NavigationNodes[2].Children[0].Children?.Count ?? 0);
            Assert.AreEqual("Leaderboards", harness.NavigationNodes[2].Children[1].Label);
            Assert.AreEqual(0, harness.NavigationNodes[2].Children[1].Children?.Count ?? 0);

            Assert.AreEqual("Bonus", harness.NavigationNodes[3].Label);
            Assert.AreEqual(2, harness.NavigationNodes[3].Children?.Count ?? 0);

            Assert.AreEqual("Achievements", harness.NavigationNodes[3].Children[0].Label);
            Assert.AreEqual(1, harness.NavigationNodes[3].Children[0].Children?.Count ?? 0);

            var achievementNode = harness.NavigationNodes[3].Children[0].Children[0] as AchievementNavigationViewModel;
            Assert.IsNotNull(achievementNode);
            Assert.AreEqual("Test Achievement", achievementNode.Label);
            Assert.IsNotNull(achievementNode.Editor);
            Assert.AreSame(achievement, ((AchievementViewModel)achievementNode.Editor).Generated.Asset);
            Assert.AreEqual(GeneratedCompareState.GeneratedOnly, achievementNode.CompareState);
            Assert.AreEqual("Generated only", achievementNode.ModificationMessage);

            Assert.IsNotNull(achievementNode.ContextMenu);
            Assert.AreEqual(1, achievementNode.ContextMenu.Count());
            var menuItem = achievementNode.ContextMenu.First();
            Assert.AreEqual("Update Local", menuItem.Label);
            Assert.IsTrue(menuItem.Command.CanExecute(null));
        }
    }
}
