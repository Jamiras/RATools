using Jamiras.Commands;
using Jamiras.DataModels;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Title}")]
    public class GeneratedAchievementViewModel : GeneratedItemViewModelBase, ICompositeViewModel
    {
        public GeneratedAchievementViewModel(GameViewModel owner, Achievement generatedAchievement)
        {
            Generated = new AchievementViewModel(owner);
            if (generatedAchievement != null)
                Generated.LoadAchievement(generatedAchievement);

            Local = new AchievementComparisonViewModel(owner, generatedAchievement);
            Unofficial = new AchievementComparisonViewModel(owner, generatedAchievement);
            Core = new AchievementComparisonViewModel(owner, generatedAchievement);

            UpdateLocalCommand = new DelegateCommand(() => UpdateLocal(owner));
            DeleteLocalCommand = new DelegateCommand(() => DeleteLocal(owner));
        }

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
                Local = new AchievementViewModel(owner);
                Local.LoadAchievement(localAchievement);
            }

            if (Unofficial.Modified == ModifiedState.Unmodified)
            {
                var unofficialAchievement = Unofficial.Achievement;
                Unofficial = new AchievementViewModel(owner);
                Unofficial.LoadAchievement(unofficialAchievement);
            }

            if (Core.Modified == ModifiedState.Unmodified)
            {
                var coreAchievement = Core.Achievement;
                Core = new AchievementViewModel(owner);
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
                Points = Core.Points.Value.GetValueOrDefault();
            }
            else if (Unofficial.Achievement != null)
            {
                Title = Unofficial.Title.Text;
                Points = Unofficial.Points.Value.GetValueOrDefault();
            }
            else if (Local.Achievement != null)
            {
                Title = Local.Title.Text;
                Points = Local.Points.Value.GetValueOrDefault();
            }

            if (Core.Achievement != null)
                Id = Core.Id;
            else if (Unofficial.Achievement != null)
                Id = Unofficial.Id;

            UpdateModificationMessage();
        }

        private void UpdateModificationMessage()
        { 
            if (!IsGenerated)
                ModificationMessage = null;
            else if (Local.Modified == ModifiedState.Modified)
                ModificationMessage = "Local achievement differs from generated achievement";
            else if (Core.Modified == ModifiedState.Modified)
                ModificationMessage = "Core achievement differs from generated achievement";
            else if (Unofficial.Modified == ModifiedState.Modified)
                ModificationMessage = "Unofficial achievement differs from generated achievement";
            else if (Local.Modified == ModifiedState.None)
                ModificationMessage = "Local achievement does not exist";
        }

        public override ModifiedState CoreModified
        {
            get { return Core.Modified; }
        }

        public override ModifiedState UnofficialModified
        {
            get { return Unofficial.Modified; }
        }

        public override ModifiedState LocalModified
        {
            get { return Local.Modified; }
        }

        public override bool IsGenerated
        {
            get { return Generated.Achievement != null; }
        }

        public AchievementViewModel Generated { get; private set; }
        public AchievementViewModel Local { get; private set; }
        public AchievementViewModel Unofficial { get; private set; }
        public AchievementViewModel Core { get; private set; }

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

            Local = new AchievementViewModel(owner);
            Local.LoadAchievement(achievement);

            OnPropertyChanged(() => Local);
            OnPropertyChanged(() => LocalModified);

            UpdateModificationMessage();
        }

        public CommandBase DeleteLocalCommand { get; protected set; }
        private void DeleteLocal(GameViewModel owner)
        {
            owner.UpdateLocal(null, Local.Achievement);

            Local = new AchievementComparisonViewModel(owner, Generated.Achievement);

            OnPropertyChanged(() => Local);
            OnPropertyChanged(() => LocalModified);

            UpdateModificationMessage();
        }

        internal override void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e)
        {
            Generated.OnShowHexValuesChanged(e);
            Local.OnShowHexValuesChanged(e);
            Unofficial.OnShowHexValuesChanged(e);
            Core.OnShowHexValuesChanged(e);

            base.OnShowHexValuesChanged(e);
        }
    }
}
