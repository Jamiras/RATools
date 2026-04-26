using Jamiras.DataModels;
using Jamiras.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace RATools.ViewModels.Navigation
{
    [DebuggerDisplay("{Label}")]
    public abstract class NavigationViewModelBase : ViewModelBase
    {
        public string ImageResourcePath
        {
            get { return string.Format("/RATools;component/Resources/{0}.png", ImageName); }
        }

        public string ImageName
        {
            get { return _imageName; }
            set
            {
                if (_imageName != value)
                {
                    _imageName = value;
                    OnPropertyChanged(() => ImageName);
                    OnPropertyChanged(() => ImageResourcePath);
                }
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string _imageName;

        public static readonly ModelProperty ImageTooltipProperty = ModelProperty.Register(typeof(NavigationViewModelBase), "ImageTooltip", typeof(string), null);
        public string ImageTooltip
        {
            get { return (string)GetValue(ImageTooltipProperty); }
            set { SetValue(ImageTooltipProperty, value); }
        }


        public static readonly ModelProperty LabelProperty = ModelProperty.Register(typeof(NavigationViewModelBase), "Label", typeof(string), string.Empty);
        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        public static readonly ModelProperty ModificationMessageProperty = ModelProperty.Register(typeof(ViewerViewModelBase), "ModificationMessage", typeof(string), null);
        public string ModificationMessage
        {
            get { return (string)GetValue(ModificationMessageProperty); }
            protected set { SetValue(ModificationMessageProperty, value); }
        }

        public static readonly ModelProperty CompareStateProperty = ModelProperty.Register(typeof(ViewerViewModelBase), "CompareState", typeof(GeneratedCompareState), GeneratedCompareState.Same, HandleCompareStateChanged);
        public GeneratedCompareState CompareState
        {
            get { return (GeneratedCompareState)GetValue(CompareStateProperty); }
            protected set { SetValue(CompareStateProperty, value); }
        }

        private static void HandleCompareStateChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            var viewModel = (NavigationViewModelBase)sender;
            viewModel.ModificationMessage = viewModel.GetModificationMessage((GeneratedCompareState)e.NewValue);
            viewModel._parent?.UpdateCompareState();
        }

        protected virtual string GetModificationMessage(GeneratedCompareState state)
        {
            switch (state)
            {
                case GeneratedCompareState.GeneratedOnly: // ○
                    return "Generated only";
                case GeneratedCompareState.LocalDiffers: // ●
                    return "Generated asset differs from unpublished";
                case GeneratedCompareState.PublishedDiffers: // ◐
                    return "Generated asset differs from published";
                case GeneratedCompareState.NotGenerated: // ◖
                    return "Local asset is not generated";
                case GeneratedCompareState.Invalid: // △
                    return "Generation failed";
                default:
                    return null;
            }
        }

        internal void UpdateCompareState()
        {
            var compareState = GeneratedCompareState.Same;

            foreach (var child in Children)
            {
                if (child.CompareState > compareState)
                    compareState = child.CompareState;
            }

            CompareState = compareState;
        }

        public static readonly ModelProperty IsSelectedProperty = ModelProperty.Register(typeof(NavigationViewModelBase), "IsSelected", typeof(bool), false);
        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        public static readonly ModelProperty IsExpandedProperty = ModelProperty.Register(typeof(NavigationViewModelBase), "IsExpanded", typeof(bool), true);
        public bool IsExpanded
        {
            get { return (bool)GetValue(IsExpandedProperty); }
            set { SetValue(IsExpandedProperty, value); }
        }

        public ObservableCollection<NavigationViewModelBase> Children { get; private set; }
        private NavigationViewModelBase _parent;

        protected void InitChildren()
        {
            Children = new ObservableCollection<NavigationViewModelBase>();
        }

        public void AddChild(NavigationViewModelBase child)
        {
            child._parent = this;

            Children ??= new ObservableCollection<NavigationViewModelBase>();
            Children.Add(child);

            UpdateCompareState();
        }

        public IEnumerable<CommandViewModel> ContextMenu { get; protected set; }

        public static bool operator ==(NavigationViewModelBase left, NavigationViewModelBase right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left is null || right is null)
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(NavigationViewModelBase left, NavigationViewModelBase right)
        {
            if (ReferenceEquals(left, right))
                return false;
            if (left is null || right is null)
                return true;

            return !left.Equals(right);
        }

        public override bool Equals(object obj)
        {
            var that = obj as NavigationViewModelBase;

            return that != null && this.ImageName == that.ImageName && this.Label == that.Label;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
