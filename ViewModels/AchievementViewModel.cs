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
            UpdateLocalCommand = new DelegateCommand(ExecuteUpdateLocal);

            Title = new ModifiableTextFieldViewModel();
            Description = new ModifiableTextFieldViewModel();
            Points = new ModifiableTextFieldViewModel();
        }

        public AchievementViewModel(GameViewModel owner, Achievement achievement)
            : this(owner)
        {
            Achievement = achievement;

            Id = achievement.Id;
            Title.Text = achievement.Title;

            Description.Text = achievement.Description;
            Points.Text = achievement.Points.ToString();
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

        public ModifiableTextFieldViewModel Title { get; private set; }
        public ModifiableTextFieldViewModel Description { get; private set; }
        public ModifiableTextFieldViewModel Points { get; private set; }

        private IEnumerable<ModifiableTextFieldViewModel> ModifiableTextFields
        {
            get
            {
                yield return Title;
                yield return Description;
                yield return Points;

                foreach (var group in RequirementGroups)
                {
                    foreach (var requirement in group.Requirements)
                        yield return requirement.Definition;
                }
            }
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

        public string Status
        {
            get 
            {
                if (_status == null)
                    _status = GetStatus();

                return _status;
            }
        }
        private string _status;

        private string GetStatus()
        {
            if (Id > 0)
            {
                if (DiffersFromPublished())
                    return DiffersFromLocal() ? "Differs from server and local" : "Differs from server";
                if (IsNotGenerated())
                    return DiffersFromLocal() ? "Not generated and differs from local" : "Not generated";
            }

            if (DiffersFromLocal())
                return "Differs from local";

            return "";
        }

        private bool DiffersFromPublished()
        {
            foreach (var field in ModifiableTextFields)
            {
                if (field.IsModifiedFromPublished)
                    return true;
            }

            return false;
        }

        private bool DiffersFromLocal()
        {
            foreach (var field in ModifiableTextFields)
            {
                if (field.IsModifiedFromLocal)
                    return true;
            }

            return false;
        }

        private bool IsNotGenerated()
        {
            foreach (var field in ModifiableTextFields)
            {
                if (field.IsNotGenerated)
                    return true;
            }

            return false;
        }

        public CommandBase UpdateLocalCommand { get; private set; }
        private void ExecuteUpdateLocal()
        {
            UpdateLocal();

            foreach (var field in ModifiableTextFields)
                field.LocalText = field.Text;

            if (_status != null)
            {
                _status = null;
                OnPropertyChanged(() => Status);
            }
        }
        protected virtual void UpdateLocal()
        {
            _owner.UpdateLocal(Achievement, Title.LocalText);
        }
    }
}
