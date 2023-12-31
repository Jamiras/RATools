using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.Services;
using Moq;
using NUnit.Framework;
using RATools.Data;
using RATools.Services;
using RATools.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Tests.ViewModels
{
    [TestFixture]
    class AssetViewModelBaseTests
    {
        private class AssetViewModelBaseHarness : AssetViewModelBase
        {
            public AssetViewModelBaseHarness()
                : base(new MockGameViewModel())
            {
            }

            public AssetViewModelBaseHarness(GameViewModel owner)
                : base(owner)
            {
            }

            public override string ViewerType
            {
                get { return "Test"; }
            }

            protected override void UpdateLocal(AssetBase asset, AssetBase localAsset, StringBuilder warning, bool validateAll)
            {
                throw new NotImplementedException();
            }

            internal override IEnumerable<TriggerViewModel> BuildTriggerList(AssetSourceViewModel assetViewModel)
            {
                return new TriggerViewModel[]
                {
                    new TriggerViewModel("Trigger" + Id, (Achievement)null, NumberFormat.Decimal, new Dictionary<int, string>())
                };
            }

            private class MockGameViewModel : GameViewModel
            {
                public MockGameViewModel()
                    : base(99, "Game", new Mock<ILogger>().Object, new Mock<IFileSystemService>().Object)
                { 
                }
            }
        }

        private class TestAsset : AssetBase
        {
            public TestAsset(int id, string title, string description)
            {
                Id = id;
                Title = title;
                Description = description;
            }
        }

        private class TestAssetUnofficial : AssetBase
        {
            public TestAssetUnofficial(int id, string title, string description)
            {
                Id = id;
                Title = title;
                Description = description;
            }

            public override bool IsUnofficial
            {
                get { return true; }
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
            var vmAsset = new AssetViewModelBaseHarness();
            Assert.That(vmAsset.BadgeName, Is.Null);
            Assert.That(vmAsset.CanUpdate, Is.False);
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.None));
            Assert.That(vmAsset.DeleteLocalCommand, Is.InstanceOf<DisabledCommand>());
            Assert.That(vmAsset.Description, Is.EqualTo(""));
            Assert.That(vmAsset.Generated, Is.Not.Null);
            Assert.That(vmAsset.Id, Is.EqualTo(0));
            Assert.That(vmAsset.IsDescriptionModified, Is.False);
            Assert.That(vmAsset.IsGenerated, Is.False);
            Assert.That(vmAsset.IsPointsModified, Is.False);
            Assert.That(vmAsset.IsTitleModified, Is.False);
            Assert.That(vmAsset.Local, Is.Not.Null);
            Assert.That(vmAsset.ModificationMessage, Is.Null);
            Assert.That(vmAsset.Other, Is.Null);
            Assert.That(vmAsset.Points, Is.EqualTo(0));
            Assert.That(vmAsset.Published, Is.Not.Null);
            Assert.That(vmAsset.SourceLine, Is.EqualTo(0));
            Assert.That(vmAsset.Title, Is.EqualTo(""));
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(0));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Generated"));
            Assert.That(vmAsset.UpdateLocalCommand, Is.InstanceOf<DisabledCommand>());
            Assert.That(vmAsset.ViewerImage, Is.EqualTo("/RATools;component/Resources/test.png"));
            Assert.That(vmAsset.ViewerType, Is.EqualTo("Test"));
        }

        [Test]
        public void TestRefreshCoreNotGenerated()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Published.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1234));
            Assert.That(vmAsset.Title, Is.EqualTo("Title"));
            Assert.That(vmAsset.Description, Is.EqualTo("Description"));
            Assert.That(vmAsset.IsTitleModified, Is.False);
            Assert.That(vmAsset.IsDescriptionModified, Is.False);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("Badge"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.None));
            Assert.That(vmAsset.ModificationMessage, Is.Null);
            Assert.That(vmAsset.IsGenerated, Is.False);
            Assert.That(vmAsset.CanUpdate, Is.False);
            Assert.That(vmAsset.Other, Is.Null);
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.Not.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1234"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Core (Not Generated)"));
        }

        [Test]
        public void TestRefreshUnofficialNotGenerated()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Published.Asset = new TestAssetUnofficial(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1234));
            Assert.That(vmAsset.Title, Is.EqualTo("Title"));
            Assert.That(vmAsset.Description, Is.EqualTo("Description"));
            Assert.That(vmAsset.IsTitleModified, Is.False);
            Assert.That(vmAsset.IsDescriptionModified, Is.False);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("Badge"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.None));
            Assert.That(vmAsset.ModificationMessage, Is.Null);
            Assert.That(vmAsset.IsGenerated, Is.False);
            Assert.That(vmAsset.CanUpdate, Is.False);
            Assert.That(vmAsset.Other, Is.Null);
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.Not.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1234"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Unofficial (Not Generated)"));
        }

        [Test]
        public void TestRefreshLocalNotGenerated()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Local.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1234));
            Assert.That(vmAsset.Title, Is.EqualTo("Title"));
            Assert.That(vmAsset.Description, Is.EqualTo("Description"));
            Assert.That(vmAsset.IsTitleModified, Is.False);
            Assert.That(vmAsset.IsDescriptionModified, Is.False);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("Badge"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.None));
            Assert.That(vmAsset.ModificationMessage, Is.Null);
            Assert.That(vmAsset.IsGenerated, Is.False);
            Assert.That(vmAsset.CanUpdate, Is.False);
            Assert.That(vmAsset.Other, Is.Null);
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.Not.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1234"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Local (Not Generated)"));
        }

        [Test]
        public void TestRefreshGeneratedSameAsCore()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Published.Asset = new TestAsset(1235, "TitleG", "DescriptionG")
            {
                BadgeName = "BadgeG"
            };
            vmAsset.Local.Asset = new TestAsset(1234, "TitleL", "DescriptionL")
            {
                BadgeName = "BadgeL"
            };
            vmAsset.Generated.Asset = new TestAsset(1235, "TitleG", "DescriptionG")
            {
                BadgeName = "BadgeG"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1235));
            Assert.That(vmAsset.Title, Is.EqualTo("TitleG"));
            Assert.That(vmAsset.Description, Is.EqualTo("DescriptionG"));
            Assert.That(vmAsset.IsTitleModified, Is.True);
            Assert.That(vmAsset.IsDescriptionModified, Is.True);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("BadgeG"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.LocalDiffers));
            Assert.That(vmAsset.ModificationMessage, Is.EqualTo("Local differs from generated"));
            Assert.That(vmAsset.IsGenerated, Is.True);
            Assert.That(vmAsset.CanUpdate, Is.True);
            Assert.That(vmAsset.Other, Is.SameAs(vmAsset.Local));
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1235"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Generated (Same as Core)"));
        }

        [Test]
        public void TestRefreshGeneratedSameAsUnofficial()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Published.Asset = new TestAssetUnofficial(1235, "TitleG", "DescriptionG")
            {
                BadgeName = "BadgeG"
            };
            vmAsset.Local.Asset = new TestAsset(1234, "TitleL", "DescriptionL")
            {
                BadgeName = "BadgeL"
            };
            vmAsset.Generated.Asset = new TestAsset(1235, "TitleG", "DescriptionG")
            {
                BadgeName = "BadgeG"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1235));
            Assert.That(vmAsset.Title, Is.EqualTo("TitleG"));
            Assert.That(vmAsset.Description, Is.EqualTo("DescriptionG"));
            Assert.That(vmAsset.IsTitleModified, Is.True);
            Assert.That(vmAsset.IsDescriptionModified, Is.True);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("BadgeG"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.LocalDiffers));
            Assert.That(vmAsset.ModificationMessage, Is.EqualTo("Local differs from generated"));
            Assert.That(vmAsset.IsGenerated, Is.True);
            Assert.That(vmAsset.CanUpdate, Is.True);
            Assert.That(vmAsset.Other, Is.SameAs(vmAsset.Local));
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1235"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Generated (Same as Unofficial)"));
        }

        [Test]
        public void TestRefreshGeneratedDiffersFromLocal()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Local.Asset = new TestAsset(1234, "TitleL", "DescriptionL")
            {
                BadgeName = "BadgeL"
            };
            vmAsset.Generated.Asset = new TestAsset(1235, "TitleG", "DescriptionG")
            {
                BadgeName = "BadgeG"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1235));
            Assert.That(vmAsset.Title, Is.EqualTo("TitleG"));
            Assert.That(vmAsset.Description, Is.EqualTo("DescriptionG"));
            Assert.That(vmAsset.IsTitleModified, Is.True);
            Assert.That(vmAsset.IsDescriptionModified, Is.True);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("BadgeG"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.LocalDiffers));
            Assert.That(vmAsset.ModificationMessage, Is.EqualTo("Local differs from generated"));
            Assert.That(vmAsset.IsGenerated, Is.True);
            Assert.That(vmAsset.CanUpdate, Is.True);
            Assert.That(vmAsset.Other, Is.SameAs(vmAsset.Local));
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1235"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Generated"));
        }

        [Test]
        public void TestRefreshGeneratedSameAsLocalButNotCore()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Published.Asset = new TestAsset(1235, "TitleP", "DescriptionP")
            {
                BadgeName = "BadgeP"
            };
            vmAsset.Local.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };
            vmAsset.Generated.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1234));
            Assert.That(vmAsset.Title, Is.EqualTo("Title"));
            Assert.That(vmAsset.Description, Is.EqualTo("Description"));
            Assert.That(vmAsset.IsTitleModified, Is.True);
            Assert.That(vmAsset.IsDescriptionModified, Is.True);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("Badge"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.PublishedDiffers));
            Assert.That(vmAsset.ModificationMessage, Is.EqualTo("Core differs from generated"));
            Assert.That(vmAsset.IsGenerated, Is.True);
            Assert.That(vmAsset.CanUpdate, Is.False);
            Assert.That(vmAsset.Other, Is.SameAs(vmAsset.Published));
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1234"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Generated (Same as Local)"));
        }

        [Test]
        public void TestRefreshGeneratedSameAsLocalButNotUnofficial()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Published.Asset = new TestAssetUnofficial(1235, "TitleP", "DescriptionP")
            {
                BadgeName = "BadgeP"
            };
            vmAsset.Local.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };
            vmAsset.Generated.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1234));
            Assert.That(vmAsset.Title, Is.EqualTo("Title"));
            Assert.That(vmAsset.Description, Is.EqualTo("Description"));
            Assert.That(vmAsset.IsTitleModified, Is.True);
            Assert.That(vmAsset.IsDescriptionModified, Is.True);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("Badge"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.PublishedDiffers));
            Assert.That(vmAsset.ModificationMessage, Is.EqualTo("Unofficial differs from generated"));
            Assert.That(vmAsset.IsGenerated, Is.True);
            Assert.That(vmAsset.CanUpdate, Is.False);
            Assert.That(vmAsset.Other, Is.SameAs(vmAsset.Published));
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1234"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Generated (Same as Local)"));
        }

        [Test]
        public void TestRefreshGeneratedNotInLocalDiffersFromCore()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Published.Asset = new TestAssetUnofficial(1235, "TitleP", "DescriptionP")
            {
                BadgeName = "BadgeP"
            };
            vmAsset.Generated.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1234));
            Assert.That(vmAsset.Title, Is.EqualTo("Title"));
            Assert.That(vmAsset.Description, Is.EqualTo("Description"));
            Assert.That(vmAsset.IsTitleModified, Is.True);
            Assert.That(vmAsset.IsDescriptionModified, Is.True);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("Badge"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.PublishedDiffers));
            Assert.That(vmAsset.ModificationMessage, Is.EqualTo("Unofficial differs from generated"));
            Assert.That(vmAsset.IsGenerated, Is.True);
            Assert.That(vmAsset.CanUpdate, Is.True);
            Assert.That(vmAsset.Other, Is.SameAs(vmAsset.Published));
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1234"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Generated (Not in Local)"));
        }

        [Test]
        public void TestRefreshGeneratedNotInLocal()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Generated.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1234));
            Assert.That(vmAsset.Title, Is.EqualTo("Title"));
            Assert.That(vmAsset.Description, Is.EqualTo("Description"));
            Assert.That(vmAsset.IsTitleModified, Is.False);
            Assert.That(vmAsset.IsDescriptionModified, Is.False);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("Badge"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.PublishedMatchesNotLocal));
            Assert.That(vmAsset.ModificationMessage, Is.EqualTo("Local Test does not exist"));
            Assert.That(vmAsset.IsGenerated, Is.True);
            Assert.That(vmAsset.CanUpdate, Is.True);
            Assert.That(vmAsset.Other, Is.Null);
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.Not.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1234"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Generated (Not in Local)"));
        }

        [Test]
        public void TestRefreshGeneratedSameAsCoreNotInLocal()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Published.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };
            vmAsset.Generated.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1234));
            Assert.That(vmAsset.Title, Is.EqualTo("Title"));
            Assert.That(vmAsset.Description, Is.EqualTo("Description"));
            Assert.That(vmAsset.IsTitleModified, Is.False);
            Assert.That(vmAsset.IsDescriptionModified, Is.False);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("Badge"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.PublishedMatchesNotLocal));
            Assert.That(vmAsset.ModificationMessage, Is.EqualTo("Local Test does not exist"));
            Assert.That(vmAsset.IsGenerated, Is.True);
            Assert.That(vmAsset.CanUpdate, Is.True);
            Assert.That(vmAsset.Other, Is.Null);
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.Not.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1234"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Generated (Same as Core, not in Local)"));
        }

        [Test]
        public void TestRefreshGeneratedSameAsUnofficialNotInLocal()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Published.Asset = new TestAssetUnofficial(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };
            vmAsset.Generated.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1234));
            Assert.That(vmAsset.Title, Is.EqualTo("Title"));
            Assert.That(vmAsset.Description, Is.EqualTo("Description"));
            Assert.That(vmAsset.IsTitleModified, Is.False);
            Assert.That(vmAsset.IsDescriptionModified, Is.False);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("Badge"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.PublishedMatchesNotLocal));
            Assert.That(vmAsset.ModificationMessage, Is.EqualTo("Local Test does not exist"));
            Assert.That(vmAsset.IsGenerated, Is.True);
            Assert.That(vmAsset.CanUpdate, Is.True);
            Assert.That(vmAsset.Other, Is.Null);
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.Not.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1234"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Generated (Same as Unofficial, not in Local)"));
        }

        [Test]
        public void TestRefreshGeneratedSameAsLocal()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Local.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };
            vmAsset.Generated.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1234));
            Assert.That(vmAsset.Title, Is.EqualTo("Title"));
            Assert.That(vmAsset.Description, Is.EqualTo("Description"));
            Assert.That(vmAsset.IsTitleModified, Is.False);
            Assert.That(vmAsset.IsDescriptionModified, Is.False);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("Badge"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.Same));
            Assert.That(vmAsset.ModificationMessage, Is.Null);
            Assert.That(vmAsset.IsGenerated, Is.True);
            Assert.That(vmAsset.CanUpdate, Is.False);
            Assert.That(vmAsset.Other, Is.Null);
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.Not.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1234"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Generated (Same as Local)"));
        }

        [Test]
        public void TestRefreshGeneratedSameAsCoreAndLocal()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Published.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };
            vmAsset.Local.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };
            vmAsset.Generated.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1234));
            Assert.That(vmAsset.Title, Is.EqualTo("Title"));
            Assert.That(vmAsset.Description, Is.EqualTo("Description"));
            Assert.That(vmAsset.IsTitleModified, Is.False);
            Assert.That(vmAsset.IsDescriptionModified, Is.False);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("Badge"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.Same));
            Assert.That(vmAsset.ModificationMessage, Is.Null);
            Assert.That(vmAsset.IsGenerated, Is.True);
            Assert.That(vmAsset.CanUpdate, Is.False);
            Assert.That(vmAsset.Other, Is.Null);
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.Not.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1234"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Generated (Same as Core and Local)"));
        }

        [Test]
        public void TestRefreshGeneratedSameAsCoreAndLocalExceptBadge()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Published.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };
            vmAsset.Local.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "00000"
            };
            vmAsset.Generated.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "0"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1234));
            Assert.That(vmAsset.Title, Is.EqualTo("Title"));
            Assert.That(vmAsset.Description, Is.EqualTo("Description"));
            Assert.That(vmAsset.IsTitleModified, Is.False);
            Assert.That(vmAsset.IsDescriptionModified, Is.False);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("Badge"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.Same));
            Assert.That(vmAsset.ModificationMessage, Is.Null);
            Assert.That(vmAsset.IsGenerated, Is.True);
            Assert.That(vmAsset.CanUpdate, Is.False);
            Assert.That(vmAsset.Other, Is.Null);
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.Not.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1234"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Generated (Same as Core and Local)"));
        }

        [Test]
        public void TestRefreshGeneratedSameAsUnofficialAndLocal()
        {
            var vmAsset = new AssetViewModelBaseHarness();
            vmAsset.Published.Asset = new TestAssetUnofficial(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };
            vmAsset.Local.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };
            vmAsset.Generated.Asset = new TestAsset(1234, "Title", "Description")
            {
                BadgeName = "Badge"
            };

            vmAsset.Refresh();

            Assert.That(vmAsset.Id, Is.EqualTo(1234));
            Assert.That(vmAsset.Title, Is.EqualTo("Title"));
            Assert.That(vmAsset.Description, Is.EqualTo("Description"));
            Assert.That(vmAsset.IsTitleModified, Is.False);
            Assert.That(vmAsset.IsDescriptionModified, Is.False);
            Assert.That(vmAsset.BadgeName, Is.EqualTo("Badge"));
            Assert.That(vmAsset.CompareState, Is.EqualTo(GeneratedCompareState.Same));
            Assert.That(vmAsset.ModificationMessage, Is.Null);
            Assert.That(vmAsset.IsGenerated, Is.True);
            Assert.That(vmAsset.CanUpdate, Is.False);
            Assert.That(vmAsset.Other, Is.Null);
            Assert.That(vmAsset.Triggers.Count(), Is.EqualTo(1));
            Assert.That(vmAsset.Triggers.ElementAt(0), Is.Not.InstanceOf<TriggerComparisonViewModel>());
            Assert.That(vmAsset.Triggers.ElementAt(0).Label, Is.EqualTo("Trigger1234"));
            Assert.That(vmAsset.TriggerSource, Is.EqualTo("Generated (Same as Unofficial and Local)"));
        }
    }
}
