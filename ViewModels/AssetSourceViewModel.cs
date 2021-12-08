using Jamiras.DataModels;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Title.Text}")]
    public class AssetSourceViewModel : ViewModelBase
    {
        public AssetSourceViewModel(AssetViewModelBase owner, string source)
        {
            _owner = owner;
            Source = source;

            Title = new TextFieldViewModel("Title", 240);
            Description = new TextFieldViewModel("Description", 240);
            Points = new IntegerFieldViewModel("Points", 0, 100);
        }

        protected readonly AssetViewModelBase _owner;

        internal AssetBase Asset 
        { 
            get { return _asset; } 
            set
            {
                _asset = value;
                Id = value.Id;
                Title.Text = value.Title;
                Description.Text = value.Description;
                Points.Value = value.Points;
                BadgeName = value.BadgeName;

                if (_requirementGroups != null)
                {
                    _requirementGroups = null;
                    OnPropertyChanged(() => RequirementGroups);
                }
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private AssetBase _asset;

        public static readonly ModelProperty IdProperty = ModelProperty.Register(typeof(AssetSourceViewModel), "Id", typeof(int), 0);
        public int Id
        {
            get { return (int)GetValue(IdProperty); }
            internal set { SetValue(IdProperty, value); }
        }

        public string Source { get; internal set; }
        public TextFieldViewModel Title { get; private set; }
        public TextFieldViewModel Description { get; private set; }
        public IntegerFieldViewModel Points { get; private set; }

        public static readonly ModelProperty BadgeNameProperty = ModelProperty.Register(typeof(AssetSourceViewModel), "BadgeName", typeof(string), String.Empty);
        public string BadgeName
        {
            get { return (string)GetValue(BadgeNameProperty); }
            internal set { SetValue(BadgeNameProperty, value); }
        }

        public static readonly ModelProperty BadgeProperty = ModelProperty.RegisterDependant(typeof(AssetSourceViewModel), "Badge", typeof(ImageSource), new[] { BadgeNameProperty }, GetBadge);
        public ImageSource Badge
        {
            get { return (ImageSource)GetValue(BadgeProperty); }
        }

        private static ImageSource GetBadge(ModelBase model)
        {
            var vm = (AssetSourceViewModel)model;
            if (!String.IsNullOrEmpty(vm.BadgeName) && vm.BadgeName != "0")
            {
                if (String.IsNullOrEmpty(vm._owner.RACacheDirectory))
                    return null;

                var path = Path.Combine(Path.Combine(vm._owner.RACacheDirectory, "../Badge"), vm.BadgeName + ".png");
                if (File.Exists(path))
                {
                    var image = new BitmapImage(new Uri(path));
                    image.Freeze();
                    return image;
                }
            }

            return null;
        }

        internal virtual void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e)
        {
        }

        public IEnumerable<RequirementGroupViewModel> RequirementGroups
        {
            get { return _requirementGroups ?? (_requirementGroups = _owner.BuildRequirementGroups(this)); }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IEnumerable<RequirementGroupViewModel> _requirementGroups;
    }
}
