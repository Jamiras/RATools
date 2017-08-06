using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Jamiras.Commands;
using Jamiras.DataModels;
using Jamiras.ViewModels;
using RATools.Data;

namespace RATools.ViewModels
{
    public class AchievementViewModel : ViewModelBase
    {
        protected AchievementViewModel(GameViewModel owner)
        {
            _owner = owner;
            UpdateLocalCommand = new DelegateCommand(UpdateLocal);
        }

        public AchievementViewModel(GameViewModel owner, Achievement achievement)
            : this(owner)
        {
            Achievement = achievement;

            Id = achievement.Id;
            Title = achievement.Title;
            Description = achievement.Description;
            Points = achievement.Points;
            BadgeName = achievement.BadgeName;
        }

        internal Achievement Achievement { get; private set; }
        protected readonly GameViewModel _owner;

        public static readonly ModelProperty IdProperty = ModelProperty.Register(typeof(AchievementViewModel), "Id", typeof(int), 0);
        public int Id
        {
            get { return (int)GetValue(IdProperty); }
            internal set { SetValue(IdProperty, value); }
        }

        public static readonly ModelProperty TitleProperty = ModelProperty.Register(typeof(AchievementViewModel), "Title", typeof(string), String.Empty);
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            internal set { SetValue(TitleProperty, value); }
        }

        public static readonly ModelProperty DescriptionProperty = ModelProperty.Register(typeof(AchievementViewModel), "Description", typeof(string), String.Empty);
        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            internal set { SetValue(DescriptionProperty, value); }
        }

        public static readonly ModelProperty PointsProperty = ModelProperty.Register(typeof(AchievementViewModel), "Points", typeof(int), 0);
        public int Points
        {
            get { return (int)GetValue(PointsProperty); }
            internal set { SetValue(PointsProperty, value); }
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
            if (!String.IsNullOrEmpty(vm.BadgeName))
            {
                var path = Path.Combine(Path.Combine(vm._owner.RACacheDirectory, "../Badge"), vm.BadgeName + ".png");
                if (File.Exists(path))
                    return new BitmapImage(new Uri(path));
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
            var groups = new List<RequirementGroupViewModel>();
            groups.Add(new RequirementGroupViewModel("Core", Achievement.CoreRequirements, _owner.Notes));
            int i = 0;
            foreach (var alt in Achievement.AlternateRequirements)
            {
                i++;
                groups.Add(new RequirementGroupViewModel("Alt " + i, alt, _owner.Notes));
            }

            return groups;
        }

        public static readonly ModelProperty IsDifferentThanPublishedProperty = ModelProperty.Register(typeof(AchievementViewModel), "IsDifferentThanPublished", typeof(bool), false);
        public bool IsDifferentThanPublished
        {
            get { return (bool)GetValue(IsDifferentThanPublishedProperty); }
            internal set { SetValue(IsDifferentThanPublishedProperty, value); }
        }

        public static readonly ModelProperty IsDifferentThanLocalProperty = ModelProperty.Register(typeof(AchievementViewModel), "IsDifferentThanLocal", typeof(bool), false);
        public bool IsDifferentThanLocal
        {
            get { return (bool)GetValue(IsDifferentThanLocalProperty); }
            internal set { SetValue(IsDifferentThanLocalProperty, value); }
        }

        public static readonly ModelProperty IsNotGeneratedProperty = ModelProperty.Register(typeof(AchievementViewModel), "IsNotGenerated", typeof(bool), false);
        public bool IsNotGenerated
        {
            get { return (bool)GetValue(IsNotGeneratedProperty); }
            internal set { SetValue(IsNotGeneratedProperty, value); }
        }

        public static readonly ModelProperty StatusProperty = ModelProperty.RegisterDependant(typeof(AchievementViewModel), "Status", typeof(string),
            new[] { IsDifferentThanLocalProperty, IsDifferentThanPublishedProperty, IsNotGeneratedProperty }, GetStatus);
        public string Status
        {
            get { return (string)GetValue(StatusProperty); }
        }

        private static string GetStatus(ModelBase model)
        {
            var achievement = (AchievementViewModel)model;
            if (achievement.Id > 0)
            {
                if (achievement.IsDifferentThanPublished)
                    return achievement.IsDifferentThanLocal ? "Differs from server and local" : "Differs from server";
                if (achievement.IsNotGenerated)
                    return achievement.IsDifferentThanLocal ? "Not generated and differs from local" : "Not generated";
            }

            if (achievement.IsDifferentThanLocal)
                return "Differs from local";

            return "";
        }

        public CommandBase UpdateLocalCommand { get; private set; }
        protected virtual void UpdateLocal()
        {
            _owner.UpdateLocal(Achievement);
            IsDifferentThanLocal = false;
        }
    }
}
