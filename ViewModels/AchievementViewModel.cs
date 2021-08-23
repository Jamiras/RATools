﻿using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Data;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Title.Text} ({Points.Text} points)")]
    public class AchievementViewModel : ViewModelBase, ICompositeViewModel
    {
        public AchievementViewModel(GameViewModel owner, string source)
        {
            _owner = owner;
            Source = source;

            Title = new TextFieldViewModel("Title", 240);
            Description = new TextFieldViewModel("Description", 240);
            Points = new IntegerFieldViewModel("Points", 0, 100);
        }

        internal void LoadAchievement(Achievement achievement)
        {
            Achievement = achievement;

            Id = achievement.Id;
            Title.Text = achievement.Title;

            Description.Text = achievement.Description;
            Points.Value = achievement.Points;
            BadgeName = achievement.BadgeName;

            if (_requirementGroups != null)
            {
                _requirementGroups = null;
                OnPropertyChanged(() => RequirementGroups);
            }

            Modified = GetModified();
        }

        protected virtual ModifiedState GetModified()
        {
            return ModifiedState.Unmodified;
        }

        internal Achievement Achievement { get; private set; }
        protected readonly GameViewModel _owner;

        public static readonly ModelProperty IdProperty = ModelProperty.Register(typeof(AchievementViewModel), "Id", typeof(int), 0);
        public int Id
        {
            get { return (int)GetValue(IdProperty); }
            internal set { SetValue(IdProperty, value); }
        }

        public string Source { get; private set; }
        public TextFieldViewModel Title { get; private set; }
        public TextFieldViewModel Description { get; private set; }
        public IntegerFieldViewModel Points { get; private set; }

        public bool IsUnofficial
        {
            get { return (Achievement != null) && Achievement.IsUnofficial; }
        }

        public static readonly ModelProperty BadgeNameProperty = ModelProperty.Register(typeof(AchievementViewModel), "BadgeName", typeof(string), String.Empty);
        public string BadgeName
        {
            get { return (string)GetValue(BadgeNameProperty); }
            internal set { SetValue(BadgeNameProperty, value); }
        }

        public static readonly ModelProperty BadgeProperty = ModelProperty.RegisterDependant(typeof(AchievementViewModel), "Badge", typeof(ImageSource), new[] { BadgeNameProperty }, GetBadge);
        public ImageSource Badge
        {
            get { return (ImageSource)GetValue(BadgeProperty); }
        }

        private static ImageSource GetBadge(ModelBase model)
        {
            var vm = (AchievementViewModel)model;
            if (!String.IsNullOrEmpty(vm.BadgeName) && vm.BadgeName != "0")
            {
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

        public IEnumerable<RequirementGroupViewModel> RequirementGroups
        {
            get { return _requirementGroups ?? (_requirementGroups = BuildRequirementGroups()); }
        }
        private IEnumerable<RequirementGroupViewModel> _requirementGroups;

        protected virtual List<RequirementGroupViewModel> BuildRequirementGroups()
        {
            var numberFormat = ServiceRepository.Instance.FindService<ISettings>().HexValues ? NumberFormat.Hexadecimal : NumberFormat.Decimal;

            var groups = new List<RequirementGroupViewModel>();
            if (Achievement != null)
            {
                groups.Add(new RequirementGroupViewModel("Core", Achievement.CoreRequirements, numberFormat, _owner.Notes));
                int i = 0;
                foreach (var alt in Achievement.AlternateRequirements)
                {
                    i++;
                    groups.Add(new RequirementGroupViewModel("Alt " + i, alt, numberFormat, _owner.Notes));
                }
            }

            return groups;
        }

        public static readonly ModelProperty ModifiedProperty = ModelProperty.Register(typeof(AchievementViewModel), "Modified", typeof(ModifiedState), ModifiedState.None);
        public ModifiedState Modified
        {
            get { return (ModifiedState)GetValue(ModifiedProperty); }
            private set { SetValue(ModifiedProperty, value); }
        }

        IEnumerable<ViewModelBase> ICompositeViewModel.GetChildren()
        {
            yield return Title;
            yield return Description;
            yield return Points;

            if (_requirementGroups != null)
            {
                foreach (var group in _requirementGroups)
                    yield return group;
            }
        }

        internal void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e)
        {
            if (_requirementGroups != null)
            {
                foreach (var group in _requirementGroups)
                    group.OnShowHexValuesChanged(e);
            }
        }
    }
}
