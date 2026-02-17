using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.Services;
using Moq;
using NUnit.Framework;
using RATools.ViewModels;
using RATools.ViewModels.Navigation;
using System.Collections.Generic;
using System.Linq;

namespace RATools.Tests.ViewModels.Nagivation
{
    [TestFixture]
    class EditorNavigationViewModelBaseTests
    {
        private class EditorNavigationViewModelHarness : EditorNavigationViewModelBase
        {
            public EditorNavigationViewModelHarness()
            {
            }

            public void SetCompareState(GeneratedCompareState compareState)
            {
                CompareState = compareState;
            }
        }

        private class DummyViewerViewModel : ViewerViewModelBase
        {
            public DummyViewerViewModel()
                : base(new GameViewModel(1, "GameTitle", new Mock<ILogger>().Object, new Mock<IFileSystemService>().Object))
            {
                UpdateLocalCommand = new DelegateCommand(() => { });
            }

            public override string ViewerType { get { return "Dummy"; } }

            public void SetTitle(string title)
            {
                Title = title; 
            }

            public void SetCompareState(GeneratedCompareState state)
            {
                CompareState = state;
                // CompareState updates ModificationMesssage
            }
        }

        [Test]
        public void TestInitialState()
        {
            var harness = new EditorNavigationViewModelHarness();
            Assert.That(harness.Editor, Is.Null);

            Assert.That(harness.ContextMenu, Is.Not.Null);
            Assert.That(harness.ContextMenu.Count(), Is.EqualTo(1));
            var menuItem = harness.ContextMenu.First();
            Assert.That(menuItem.Label, Is.EqualTo("Update Local"));
            Assert.That(menuItem.Command.CanExecute(null), Is.False);
        }

        [Test]
        public void TestTitleAndCompareStateFromEditor()
        {
            var harness = new EditorNavigationViewModelHarness();
            Assert.That(harness.Editor, Is.Null);

            var changedProperties = new List<string>();
            harness.PropertyChanged += (o, e) => changedProperties.Add(e.PropertyName);

            var editor = new DummyViewerViewModel();
            editor.SetTitle("Editor Title");
            editor.SetCompareState(GeneratedCompareState.NotGenerated);

            harness.Editor = editor;
            Assert.That(harness.Editor, Is.SameAs(editor));
            Assert.That(harness.Label, Is.EqualTo(editor.Title));
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.NotGenerated));

            Assert.That(changedProperties, 
                Has.Member("Editor").
                And.Member("Label").
                And.Member("CompareState").
                And.Member("ModificationMessage").
                And.Count.EqualTo(4));

            changedProperties.Clear();
            editor.SetTitle("New Title");
            Assert.That(harness.Label, Is.EqualTo("New Title"));
            Assert.That(changedProperties, Has.Member("Label").And.Count.EqualTo(1));

            changedProperties.Clear();
            editor.SetCompareState(GeneratedCompareState.Same);
            Assert.That(harness.CompareState, Is.EqualTo(GeneratedCompareState.Same));
            Assert.That(changedProperties, Has.Member("CompareState").And.Member("ModificationMessage").And.Count.EqualTo(2));
        }

        [Test]
        [TestCase(GeneratedCompareState.None, false)]
        [TestCase(GeneratedCompareState.Same, false)]
        [TestCase(GeneratedCompareState.NotGenerated, false)]
        [TestCase(GeneratedCompareState.GeneratedOnly, true)]
        [TestCase(GeneratedCompareState.PublishedDiffers, true)]
        [TestCase(GeneratedCompareState.LocalDiffers, true)]
        public void TestCanUpdateLocal(GeneratedCompareState state, bool expected)
        {
            var harness = new EditorNavigationViewModelHarness();
            var editor = new DummyViewerViewModel();
            editor.SetCompareState(state);

            harness.Editor = editor;
            Assert.That(harness.CompareState, Is.EqualTo(state));

            Assert.That(harness.ContextMenu, Is.Not.Null);
            Assert.That(harness.ContextMenu.Count(), Is.EqualTo(1));
            Assert.That(harness.ContextMenu.First().Command.CanExecute(null), Is.EqualTo(expected));
        }
    }
}
