using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Data;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Title} ({AssetType,nq})")]
    public abstract class AssetViewModelBase : GeneratedItemViewModelBase
    {
        public AssetViewModelBase(GameViewModel owner, AssetBase generatedAsset)
            : base(owner)
        {
            Generated = new AssetSourceViewModel(this, "Generated");
            if (generatedAsset != null)
                Generated.Asset = generatedAsset;

            Local = new AssetSourceViewModel(this, "Local");
            Published = new AssetSourceViewModel(this, "Published");

            if (owner == null || String.IsNullOrEmpty(owner.RACacheDirectory))
            {
                UpdateLocalCommand = DisabledCommand.Instance;
                DeleteLocalCommand = DisabledCommand.Instance;
            }
            else
            {
                UpdateLocalCommand = new DelegateCommand(UpdateLocal);
                DeleteLocalCommand = new DelegateCommand(DeleteLocal);
            }
        }


        protected abstract string AssetType { get; }

        public AssetSourceViewModel Generated { get; private set; }
        public AssetSourceViewModel Local { get; private set; }
        public AssetSourceViewModel Published { get; private set; }
        public AssetSourceViewModel Other { get; private set; }

        public CommandBase DeleteLocalCommand { get; protected set; }

        public virtual bool IsGenerated
        {
            get { return Generated.Asset != null; }
        }

        internal bool AllocateLocalId(int value)
        {
            // don't attempt to assign a temporary ID to a published asset
            if (Published != null && Published.Asset != null)
                return false;

            // if it's in the local file, generate a temporary ID if one was not previously generated
            if (Local != null && Local.Id == 0)
            {
                var localAsset = Local.Asset;
                if (localAsset != null)
                {
                    localAsset.Id = value;
                    Local.Asset = localAsset; // refresh the viewmodel's ID property
                    Id = Local.Id;
                    return true;
                }
            }

            // if it's not in the local file, generate a temporary ID if one was not provided by the code
            if (Generated != null && Generated.Id == 0)
            {
                var generatedAsset = Generated.Asset;
                if (generatedAsset != null)
                {
                    generatedAsset.Id = value;
                    Generated.Asset = generatedAsset; // refresh the viewmodel's ID property
                    Id = Generated.Id;
                    return true;
                }
            }

            return true;
        }

        public static readonly ModelProperty IdProperty = ModelProperty.Register(typeof(AssetViewModelBase), "Id", typeof(int), 0);
        public int Id
        {
            get { return (int)GetValue(IdProperty); }
            protected set { SetValue(IdProperty, value); }
        }

        public static readonly ModelProperty PointsProperty = ModelProperty.Register(typeof(AssetViewModelBase), "Points", typeof(int), 0);
        public int Points
        {
            get { return (int)GetValue(PointsProperty); }
            protected set { SetValue(PointsProperty, value); }
        }


        public static readonly ModelProperty IsPointsModifiedProperty = ModelProperty.Register(typeof(AssetViewModelBase), "IsPointsModified", typeof(bool), false);
        public bool IsPointsModified
        {
            get { return (bool)GetValue(IsPointsModifiedProperty); }
            protected set { SetValue(IsPointsModifiedProperty, value); }
        }

        protected void UpdateModified()
        {
            var coreAsset = Published.Asset;

            if (!IsGenerated)
            {
                ModificationMessage = null;
                CanUpdate = false;

                Other = null;
                IsTitleModified = false;
                IsDescriptionModified = false;
                IsPointsModified = false;
                CompareState = GeneratedCompareState.None;

                if (coreAsset != null)
                {
                    RequirementGroups = Published.RequirementGroups;
                    if (coreAsset.IsUnofficial)
                        RequirementSource = "Unofficial (Not Generated)";
                    else
                        RequirementSource = "Core (Not Generated)";
                }
            }
            else if (IsModified(Local))
            {
                if (coreAsset != null && !IsModified(Published))
                {
                    if (coreAsset.IsUnofficial)
                        RequirementSource = "Generated (Same as Unofficial)";
                    else
                        RequirementSource = "Generated (Same as Core)";
                }
                else
                {
                    RequirementSource = "Generated";
                }

                Other = Local;
                ModificationMessage = "Local differs from generated";
                CompareState = GeneratedCompareState.LocalDiffers;
                CanUpdate = true;
            }
            else if (coreAsset != null && IsModified(Published))
            {
                if (Local.Asset != null)
                    RequirementSource = "Generated (Same as Local)";
                else
                    RequirementSource = "Generated (Not in Local)";

                Other = Published;
                if (coreAsset.IsUnofficial)
                    ModificationMessage = "Unofficial differs from generated";
                else
                    ModificationMessage = "Core differs from generated";

                CompareState = GeneratedCompareState.PublishedDiffers;
                CanUpdate = true;
            }
            else
            {
                if (Local.Asset == null && IsGenerated)
                {
                    if (coreAsset == null)
                        RequirementSource = "Generated (Not in Local)";
                    else if (coreAsset.IsUnofficial)
                        RequirementSource = "Generated (Same as Unofficial, not in Local)";
                    else
                        RequirementSource = "Generated (Same as Core, not in Local)";

                    ModificationMessage = "Local " + AssetType + " does not exist";
                    CompareState = GeneratedCompareState.PublishedMatchesNotGenerated;
                    CanUpdate = true;
                    Other = null;
                }
                else
                {
                    ModificationMessage = null;
                    CanUpdate = false;
                    CompareState = GeneratedCompareState.Same;

                    if (coreAsset != null)
                    {
                        if (coreAsset.IsUnofficial)
                            RequirementSource = "Generated (Same as Unofficial and Local)";
                        else
                            RequirementSource = "Generated (Same as Core and Local)";

                        Other = Published;
                    }
                    else
                    {
                        RequirementSource = "Generated (Same as Local)";
                        Other = null;
                    }
                }

                IsTitleModified = false;
                IsDescriptionModified = false;
                IsPointsModified = false;

                RequirementGroups = Generated.RequirementGroups;
            }
        }

        protected bool IsModified(AssetSourceViewModel assetViewModel)
        {
            if (assetViewModel.Asset == null)
                return false;

            bool isModified = false;
            if (assetViewModel.Title.Text != Generated.Title.Text)
                IsTitleModified = isModified = true;
            if (assetViewModel.Description.Text != Generated.Description.Text)
                IsDescriptionModified = isModified = true;
            if (assetViewModel.Points.Value != Generated.Points.Value)
                IsPointsModified = isModified = true;

            var groups = new List<RequirementGroupViewModel>();
            if (GetRequirementGroups(groups, Generated, assetViewModel))
            {
                RequirementGroups = groups;
                return true;
            }

            if (isModified)
            {
                RequirementGroups = Generated.RequirementGroups;
                return true;
            }

            return false;
        }

        public int SourceLine
        {
            get { return (Generated.Asset != null) ? Generated.Asset.SourceLine : 0; }
        }

        private void UpdateLocal()
        {
            StringBuilder warning = new StringBuilder();
            UpdateLocal(warning, false);

            if (warning.Length > 0)
                TaskDialogViewModel.ShowWarningMessage("Your " + AssetType + " may not function as expected.", warning.ToString());
        }

        internal void UpdateLocal(StringBuilder warning, bool validateAll)
        {
            var asset = Generated.Asset;
            if (asset.Id == 0)
                asset.Id = Id;

            if (String.IsNullOrEmpty(asset.BadgeName) || asset.BadgeName == "0")
                asset.BadgeName = BadgeName;

            UpdateLocal(asset, Local.Asset, warning, validateAll);

            Local = new AssetSourceViewModel(this, "Local");
            Local.Asset = Generated.Asset;

            OnPropertyChanged(() => Local);
            UpdateModified();
        }

        protected abstract void UpdateLocal(AssetBase asset, AssetBase localAsset, StringBuilder warning, bool validateAll);

        private void DeleteLocal()
        {
            UpdateLocal(null, Local.Asset, null, false);

            Local = new AssetSourceViewModel(this, "Local");
            OnPropertyChanged(() => Local);
            UpdateModified();
        }

        internal virtual void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e)
        {
            foreach (var group in RequirementGroups)
                group.OnShowHexValuesChanged(e);

            Generated.OnShowHexValuesChanged(e);
            Local.OnShowHexValuesChanged(e);
            Published.OnShowHexValuesChanged(e);
        }

        public static readonly ModelProperty BadgeProperty = ModelProperty.RegisterDependant(typeof(AssetViewModelBase), "Badge", typeof(ImageSource), new ModelProperty[0], GetBadge);
        public ImageSource Badge
        {
            get { return (ImageSource)GetValue(BadgeProperty); }
        }

        protected string BadgeName { get; set; }

        private static ImageSource GetBadge(ModelBase model)
        {
            var vm = (AssetViewModelBase)model;
            if (!String.IsNullOrEmpty(vm.Published.BadgeName))
                return vm.Published.Badge;
            if (!String.IsNullOrEmpty(vm.Local.BadgeName))
                return vm.Local.Badge;

            if (!String.IsNullOrEmpty(vm.BadgeName))
            {
                vm.Local.BadgeName = vm.BadgeName;
                return vm.Local.Badge;
            }

            return null;
        }

        public override void Refresh()
        {
            var generatedAsset = Generated.Asset;
            var localAsset = Local.Asset;
            var coreAsset = Published.Asset;

            Published.Source = (coreAsset == null) ? "Published" :
                coreAsset.IsUnofficial ? "Published (Unofficial)" : "Published (Core)";

            if (generatedAsset != null)
                BindViewModel(Generated);
            else if (coreAsset != null)
                LoadViewModel(Published);
            else if (localAsset != null)
                LoadViewModel(Local);

            if (Generated.Id != 0)
                Id = Generated.Id;
            else if (Local.Id != 0)
                Id = Local.Id;
            else
                Id = Published.Id;

            if (!String.IsNullOrEmpty(Generated.BadgeName) && Generated.BadgeName != "0")
                BadgeName = Generated.BadgeName;
            else if (!String.IsNullOrEmpty(Local.BadgeName) && Local.BadgeName != "0")
                BadgeName = Local.BadgeName;
            else if (!String.IsNullOrEmpty(Published.BadgeName) && Published.BadgeName != "0")
                BadgeName = Published.BadgeName;
            else
                BadgeName = "00000";

            UpdateModified();

            base.Refresh();
        }

        private void BindViewModel(AssetSourceViewModel viewModel)
        {
            SetBinding(TitleProperty, new ModelBinding(viewModel.Title, TextFieldViewModel.TextProperty, ModelBindingMode.OneWay));
            SetBinding(DescriptionProperty, new ModelBinding(viewModel.Description, TextFieldViewModel.TextProperty, ModelBindingMode.OneWay));
            SetBinding(PointsProperty, new ModelBinding(viewModel.Points, IntegerFieldViewModel.ValueProperty, ModelBindingMode.OneWay));
        }

        private void LoadViewModel(AssetSourceViewModel viewModel)
        {
            Title = viewModel.Title.Text;
            Description = viewModel.Description.Text;
            Points = viewModel.Points.Value.GetValueOrDefault();
        }

        public static readonly ModelProperty RequirementSourceProperty = ModelProperty.Register(typeof(AssetViewModelBase), "RequirementSource", typeof(string), "Generated");

        public string RequirementSource
        {
            get { return (string)GetValue(RequirementSourceProperty); }
            private set { SetValue(RequirementSourceProperty, value); }
        }

        public static readonly ModelProperty RequirementGroupsProperty = ModelProperty.Register(typeof(AssetViewModelBase),
            "RequirementGroups", typeof(IEnumerable<RequirementGroupViewModel>), new RequirementGroupViewModel[0]);

        public IEnumerable<RequirementGroupViewModel> RequirementGroups
        {
            get { return (IEnumerable<RequirementGroupViewModel>)GetValue(RequirementGroupsProperty); }
            private set { SetValue(RequirementGroupsProperty, value); }
        }

        internal abstract IEnumerable<RequirementGroupViewModel> BuildRequirementGroups(AssetSourceViewModel assetViewModel);

        private bool GetRequirementGroups(List<RequirementGroupViewModel> groups, AssetSourceViewModel assetViewModel, AssetSourceViewModel compareAssetViewModel)
        {
            var numberFormat = ServiceRepository.Instance.FindService<ISettings>().HexValues ? NumberFormat.Hexadecimal : NumberFormat.Decimal;
            var compareGroups = new List<RequirementGroupViewModel>(compareAssetViewModel.RequirementGroups);

            foreach (var group in assetViewModel.RequirementGroups)
            {
                var compareGroup = compareGroups.FirstOrDefault(g => g.Label == group.Label);
                if (compareGroup != null)
                {
                    groups.Add(new RequirementGroupViewModel(group.Label,
                        group.Requirements.Select(r => r.Requirement),
                        compareGroup.Requirements.Select(r => r.Requirement),
                        numberFormat, _owner.Notes));

                    compareGroups.Remove(compareGroup);
                }
                else
                {
                    groups.Add(new RequirementGroupViewModel(group.Label,
                        group.Requirements.Select(r => r.Requirement),
                        new Requirement[0],
                        numberFormat, _owner.Notes));
                }
            }

            foreach (var compareGroup in compareGroups)
            {
                groups.Add(new RequirementGroupViewModel(compareGroup.Label,
                    new Requirement[0],
                    compareGroup.Requirements.Select(r => r.Requirement),
                    numberFormat, _owner.Notes));
            }

            foreach (var group in groups)
            {
                foreach (var requirement in group.Requirements.OfType<RequirementComparisonViewModel>())
                {
                    if (requirement.IsModified)
                        return true;
                }
            }

            return false;
        }
    }
}
