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
using System.Windows.Media;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Title}")]
    public class GeneratedAchievementViewModel : GeneratedItemViewModelBase, ICompositeViewModel
    {
        public GeneratedAchievementViewModel(GameViewModel owner, Achievement generatedAchievement)
        {
            _owner = owner;

            Generated = new AchievementViewModel(owner, "Generated");
            if (generatedAchievement != null)
            {
                Generated.LoadAchievement(generatedAchievement);
                Id = Generated.Id;
            }

            Local = new AchievementViewModel(owner, "Local");
            Unofficial = new AchievementViewModel(owner, "Unofficial");
            Core = new AchievementViewModel(owner, "Core");

            UpdateLocalCommand = new DelegateCommand(() => UpdateLocal(owner));
            DeleteLocalCommand = new DelegateCommand(() => DeleteLocal(owner));
        }

        private readonly GameViewModel _owner;

        public static readonly ModelProperty BadgeProperty = ModelProperty.RegisterDependant(typeof(GeneratedAchievementViewModel), "Badge", typeof(ImageSource), new ModelProperty[0], GetBadge);
        public ImageSource Badge
        {
            get { return (ImageSource)GetValue(BadgeProperty); }
        }

        private static ImageSource GetBadge(ModelBase model)
        {
            var vm = (GeneratedAchievementViewModel)model;
            if (!String.IsNullOrEmpty(vm.Core.BadgeName))
                return vm.Core.Badge;
            if (!String.IsNullOrEmpty(vm.Unofficial.BadgeName))
                return vm.Unofficial.Badge;
            if (!String.IsNullOrEmpty(vm.Local.BadgeName))
                return vm.Local.Badge;
            return null;
        }

        internal void UpdateCommonProperties(GameViewModel owner)
        {
            if (Local.Modified == ModifiedState.Unmodified)
            {
                var localAchievement = Local.Achievement;
                Local = new AchievementViewModel(owner, "Local");
                Local.LoadAchievement(localAchievement);
            }

            if (Unofficial.Modified == ModifiedState.Unmodified)
            {
                var unofficialAchievement = Unofficial.Achievement;
                Unofficial = new AchievementViewModel(owner, "Unofficial");
                Unofficial.LoadAchievement(unofficialAchievement);
            }

            if (Core.Modified == ModifiedState.Unmodified)
            {
                var coreAchievement = Core.Achievement;
                Core = new AchievementViewModel(owner, "Core");
                Core.LoadAchievement(coreAchievement);
            }

            if (Generated.Achievement != null)
            {
                Id = Generated.Id;
                SetBinding(TitleProperty, new ModelBinding(Generated.Title, TextFieldViewModel.TextProperty, ModelBindingMode.OneWay));
                SetBinding(DescriptionProperty, new ModelBinding(Generated.Description, TextFieldViewModel.TextProperty, ModelBindingMode.OneWay));
                SetBinding(PointsProperty, new ModelBinding(Generated.Points, IntegerFieldViewModel.ValueProperty, ModelBindingMode.OneWay));
            }
            else if (Core.Achievement != null)
            {
                Title = Core.Title.Text;
                Description = Core.Description.Text;
                Points = Core.Points.Value.GetValueOrDefault();
            }
            else if (Unofficial.Achievement != null)
            {
                Title = Unofficial.Title.Text;
                Description = Unofficial.Description.Text;
                Points = Unofficial.Points.Value.GetValueOrDefault();
            }
            else if (Local.Achievement != null)
            {
                Title = Local.Title.Text;
                Description = Local.Description.Text;
                Points = Local.Points.Value.GetValueOrDefault();
            }

            if (Core.Achievement != null)
                Id = Core.Id;
            else if (Unofficial.Achievement != null)
                Id = Unofficial.Id;

            UpdateModified();
        }

        public override bool IsGenerated
        {
            get { return Generated.Achievement != null; }
        }

        public int SourceLine
        {
            get { return (Generated.Achievement != null) ? Generated.Achievement.SourceLine : 0; }
        }

        public AchievementViewModel Generated { get; private set; }
        public AchievementViewModel Local { get; private set; }
        public AchievementViewModel Unofficial { get; private set; }
        public AchievementViewModel Core { get; private set; }
        public AchievementViewModel Other { get; private set; }

        public static readonly ModelProperty RequirementSourceProperty = ModelProperty.Register(typeof(GeneratedAchievementViewModel), "RequirementSource", typeof(string), "Generated");

        public string RequirementSource
        {
            get { return (string)GetValue(RequirementSourceProperty); }
            private set { SetValue(RequirementSourceProperty, value); }
        }

        public static readonly ModelProperty RequirementGroupsProperty = ModelProperty.Register(typeof(GeneratedAchievementViewModel), 
            "RequirementGroups", typeof(IEnumerable<RequirementGroupViewModel>), new RequirementGroupViewModel[0]);

        public IEnumerable<RequirementGroupViewModel> RequirementGroups
        {
            get { return (IEnumerable<RequirementGroupViewModel>)GetValue(RequirementGroupsProperty); }
            private set { SetValue(RequirementGroupsProperty, value); }
        }

        private bool GetRequirementGroups(List<RequirementGroupViewModel> groups, Achievement achievement, Achievement compareAchievement)
        {
            var numberFormat = ServiceRepository.Instance.FindService<ISettings>().HexValues ? NumberFormat.Hexadecimal : NumberFormat.Decimal;
            groups.Add(new RequirementGroupViewModel("Core", achievement.CoreRequirements, compareAchievement.CoreRequirements, numberFormat, _owner.Notes));

            int i = 0;
            var altCompareEnumerator = compareAchievement.AlternateRequirements.GetEnumerator();

            var altEnumerator = achievement.AlternateRequirements.GetEnumerator();
            while (altEnumerator.MoveNext())
            {
                i++;

                IEnumerable<Requirement> altCompareRequirements = altCompareEnumerator.MoveNext() ? altCompareEnumerator.Current : new Requirement[0];
                groups.Add(new RequirementGroupViewModel("Alt " + i, altEnumerator.Current, altCompareRequirements, numberFormat, _owner.Notes));
            }

            while (altCompareEnumerator.MoveNext())
            {
                i++;
                groups.Add(new RequirementGroupViewModel("Alt " + i, new Requirement[0], altCompareEnumerator.Current, numberFormat, _owner.Notes));
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

        private bool IsAchievementModified(AchievementViewModel achievement)
        {
            if (achievement.Achievement == null)
                return false;

            bool isModified = false;
            if (achievement.Title.Text != Generated.Title.Text)
                IsTitleModified = isModified = true;
            if (achievement.Description.Text != Generated.Description.Text)
                IsDescriptionModified = isModified = true;
            if (achievement.Points.Value != Generated.Points.Value)
                IsPointsModified = isModified = true;

            var groups = new List<RequirementGroupViewModel>();
            if (GetRequirementGroups(groups, Generated.Achievement, achievement.Achievement))
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

        protected void UpdateModified()
        {
            if (!IsGenerated)
            {
                ModificationMessage = null;
                CanUpdate = false;

                Other = null;
                IsTitleModified = false;
                IsDescriptionModified = false;
                IsPointsModified = false;
                CompareState = GeneratedCompareState.None;

                if (Core.Achievement != null)
                {
                    RequirementGroups = Core.RequirementGroups;
                    RequirementSource = "Core (Not Generated)";
                }
                else
                {
                    RequirementGroups = Unofficial.RequirementGroups;
                    RequirementSource = "Unofficial (Not Generated)";
                }
            }
            else if (IsAchievementModified(Local))
            {
                if (Core.Achievement != null && !IsAchievementModified(Core))
                    RequirementSource = "Generated (Same as Core)";
                else if (Unofficial.Achievement != null && !IsAchievementModified(Unofficial))
                    RequirementSource = "Generated (Same as Unofficial)";
                else
                    RequirementSource = "Generated";

                Other = Local;
                ModificationMessage = "Local achievement differs from generated achievement";
                CompareState = GeneratedCompareState.LocalDiffers;
                CanUpdate = true;
            }
            else if (Core.Achievement != null && IsAchievementModified(Core))
            {
                if (Local.Achievement != null)
                    RequirementSource = "Generated (Same as Local)";
                else
                    RequirementSource = "Generated (Not in Local)";

                Other = Core;
                ModificationMessage = "Core achievement differs from generated achievement";
                CompareState = GeneratedCompareState.PublishedDiffers;
                CanUpdate = true;
            }
            else if (Unofficial.Achievement != null && IsAchievementModified(Unofficial))
            {
                if (Local.Achievement != null)
                    RequirementSource = "Generated (Same as Local)";
                else
                    RequirementSource = "Generated (Not in Local)";

                Other = Unofficial;
                ModificationMessage = "Unofficial achievement differs from generated achievement";
                CompareState = GeneratedCompareState.PublishedDiffers;
                CanUpdate = true;
            }
            else 
            {
                if (Local.Achievement == null && IsGenerated)
                {
                    if (Core.Achievement != null)
                        RequirementSource = "Generated (Same as Core, not in Local)";
                    else if (Unofficial.Achievement != null)
                        RequirementSource = "Generated (Same as Unofficial, not in Local)";
                    else
                        RequirementSource = "Generated (Not in Local)";

                    ModificationMessage = "Local achievement does not exist";
                    CompareState = GeneratedCompareState.PublishedMatchesNotGenerated;
                    CanUpdate = true;
                    Other = null;
                }
                else
                {
                    ModificationMessage = null;
                    CanUpdate = false;
                    CompareState = GeneratedCompareState.Same;

                    if (Core.Achievement != null)
                    {
                        RequirementSource = "Generated (Same as Core and Local)";
                        Other = Core;
                    }
                    else if (Unofficial.Achievement != null)
                    {
                        RequirementSource = "Generated (Same as Unofficial and Local)";
                        Other = Unofficial;
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

        public static readonly ModelProperty IsTitleModifiedProperty = ModelProperty.Register(typeof(GeneratedAchievementViewModel), "IsTitleModified", typeof(bool), false);
        public bool IsTitleModified
        {
            get { return (bool)GetValue(IsTitleModifiedProperty); }
            private set { SetValue(IsTitleModifiedProperty, value); }
        }

        public static readonly ModelProperty IsDescriptionModifiedProperty = ModelProperty.Register(typeof(GeneratedAchievementViewModel), "IsDescriptionModified", typeof(bool), false);
        public bool IsDescriptionModified
        {
            get { return (bool)GetValue(IsDescriptionModifiedProperty); }
            private set { SetValue(IsDescriptionModifiedProperty, value); }
        }

        public static readonly ModelProperty IsPointsModifiedProperty = ModelProperty.Register(typeof(GeneratedAchievementViewModel), "IsPointsModified", typeof(bool), false);
        public bool IsPointsModified
        {
            get { return (bool)GetValue(IsPointsModifiedProperty); }
            private set { SetValue(IsPointsModifiedProperty, value); }
        }

        IEnumerable<ViewModelBase> ICompositeViewModel.GetChildren()
        {
            yield return Generated;
            yield return Local;
            yield return Unofficial;
            yield return Core;
        }

        private void UpdateLocal(GameViewModel owner)
        {
            var achievement = Generated.Achievement;

            if (Local.Id > 0)
                achievement.Id = Id;
            if (!String.IsNullOrEmpty(Local.BadgeName))
                achievement.BadgeName = Local.BadgeName;

            owner.UpdateLocal(achievement, Local.Achievement);

            Local = new AchievementViewModel(owner, "Local");
            Local.LoadAchievement(achievement);

            OnPropertyChanged(() => Local);
            UpdateModified();
        }

        public CommandBase DeleteLocalCommand { get; protected set; }
        private void DeleteLocal(GameViewModel owner)
        {
            owner.UpdateLocal(null, Local.Achievement);

            Local = new AchievementViewModel(owner, "Local");
            if (Generated.Achievement != null)
                Local.LoadAchievement(Generated.Achievement);

            OnPropertyChanged(() => Local);
            UpdateModified();
        }

        internal override void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e)
        {
            foreach (var group in RequirementGroups)
                group.OnShowHexValuesChanged(e);

            Generated.OnShowHexValuesChanged(e);
            Local.OnShowHexValuesChanged(e);
            Unofficial.OnShowHexValuesChanged(e);
            Core.OnShowHexValuesChanged(e);

            base.OnShowHexValuesChanged(e);
        }
    }
}
