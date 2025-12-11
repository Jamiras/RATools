using NUnit.Framework;
using RATools.ViewModels;
using RATools.ViewModels.Navigation;
using System.Collections.Generic;

namespace RATools.Tests.ViewModels.Nagivation
{
    [TestFixture]
    class NavigationViewModelBaseTests
    {
        private class NavigationViewModelHarness : NavigationViewModelBase
        {
            public NavigationViewModelHarness()
            {
            }

            public void SetCompareState(GeneratedCompareState compareState)
            {
                CompareState = compareState;
            }
        }

        [Test]
        public void TestInitialState()
        {
            var harness = new NavigationViewModelHarness();
            Assert.That(harness.ImageName, Is.Null);
            Assert.That(harness.ImageResourcePath, Is.EqualTo("/RATools;component/Resources/.png"));
            Assert.That(harness.ImageTooltip, Is.Null);

            Assert.That(harness.Label, Is.EqualTo(""));

            Assert.That(harness.ModificationMessage, Is.Null);
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.Same));

            Assert.That(harness.IsExpanded, Is.True);
            Assert.That(harness.Children, Is.Null);
            Assert.That(harness.ContextMenu, Is.Null);
        }

        [Test]
        public void TestImageResourcePathInfluencedByImageName()
        {
            var harness = new NavigationViewModelHarness();
            Assert.That(harness.ImageName, Is.Null);
            Assert.That(harness.ImageResourcePath, Is.EqualTo("/RATools;component/Resources/.png"));
            Assert.That(harness.ImageTooltip, Is.Null);

            var changedProperties = new List<string>();
            harness.PropertyChanged += (o, e) => changedProperties.Add(e.PropertyName);

            harness.ImageName = "test";

            Assert.That(harness.ImageName, Is.EqualTo("test"));
            Assert.That(harness.ImageResourcePath, Is.EqualTo("/RATools;component/Resources/test.png"));
            Assert.That(harness.ImageTooltip, Is.Null);

            Assert.That(changedProperties, 
                Has.Member("ImageName").And.Member("ImageResourcePath").And.Count.EqualTo(2));
        }

        [Test]
        public void TestCompareStateAndModificationMessage()
        {
            var harness = new NavigationViewModelHarness();
            Assert.IsNull(harness.ModificationMessage);
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.Same));

            var changedProperties = new List<string>();
            harness.PropertyChanged += (o, e) => changedProperties.Add(e.PropertyName);

            harness.SetCompareState(GeneratedCompareState.GeneratedOnly);
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.GeneratedOnly));
            Assert.That(harness.ModificationMessage, Is.EqualTo("Generated assets match published"));

            Assert.That(changedProperties,
                Has.Member("CompareState").And.Member("ModificationMessage").And.Count.EqualTo(2));

            harness.SetCompareState(GeneratedCompareState.NotGenerated);
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.NotGenerated));
            Assert.That(harness.ModificationMessage, Is.EqualTo("Published asset is not generated"));

            harness.SetCompareState(GeneratedCompareState.PublishedDiffers);
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.PublishedDiffers));
            Assert.That(harness.ModificationMessage, Is.EqualTo("Generated assets differ from published"));

            harness.SetCompareState(GeneratedCompareState.LocalDiffers);
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.LocalDiffers));
            Assert.That(harness.ModificationMessage, Is.EqualTo("Generated assets not exported"));

            changedProperties.Clear();
            harness.SetCompareState(GeneratedCompareState.Same);
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.Same));
            Assert.That(harness.ModificationMessage, Is.Null);

            Assert.That(changedProperties,
                Has.Member("CompareState").And.Member("ModificationMessage").And.Count.EqualTo(2));
        }

        [Test]
        public void TestCompareStateFromChildren()
        {
            var harness = new NavigationViewModelHarness();
            var child1 = new NavigationViewModelHarness();
            var child2 = new NavigationViewModelHarness();
            child2.SetCompareState(GeneratedCompareState.GeneratedOnly);

            harness.AddChild(child1);
            Assert.That(harness.Children.Count, Is.EqualTo(1));
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.Same));

            harness.AddChild(child2);
            Assert.That(harness.Children.Count, Is.EqualTo(2));
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.GeneratedOnly));

            child1.SetCompareState(GeneratedCompareState.LocalDiffers);
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.LocalDiffers));

            child2.SetCompareState(GeneratedCompareState.PublishedDiffers);
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.LocalDiffers));

            child1.SetCompareState(GeneratedCompareState.Same);
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.PublishedDiffers));
        }
    }
}
