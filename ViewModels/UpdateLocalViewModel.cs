using Jamiras.Commands;
using Jamiras.DataModels;
using Jamiras.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.ViewModels
{
    public class UpdateLocalViewModel : DialogViewModelBase
    {
        public UpdateLocalViewModel(GameViewModel game)
        {
            _game = game;
            _achievements = new ObservableCollection<UpdateAchievementViewModel>();
            DialogTitle = "Update Local - " + game.Title;

            foreach (var achievement in game.Editors.OfType<GeneratedAchievementViewModel>())
            {
                if (achievement.IsGenerated || achievement.Local.Achievement != null)
                    _achievements.Add(new UpdateAchievementViewModel(achievement));
            }
        }

        private readonly GameViewModel _game;

        /// <summary>
        /// Gets the list of local and generated achievements.
        /// </summary>
        public IEnumerable<UpdateAchievementViewModel> Achievements
        {
            get { return _achievements; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ObservableCollection<UpdateAchievementViewModel> _achievements;

        protected override void ExecuteOkCommand()
        {
            var warning = new StringBuilder();

            foreach (var achievement in _achievements)
            {
                if (achievement.IsUpdated)
                {
                    warning.Clear();
                    achievement.Achievement.UpdateLocal(warning, true);
                }
                else if (achievement.IsDeleted)
                {
                    achievement.Achievement.DeleteLocalCommand.Execute();
                }
            }

            if (warning.Length > 0)
                MessageBoxViewModel.ShowMessage(warning.ToString());

            DialogResult = DialogResult.Ok;
        }

        public CommandBase ToggleSelectedForUpdateCommand
        {
            get { return new DelegateCommand(ExecuteToggleSelectedForUpdateCommand); }
        }

        private void ExecuteToggleSelectedForUpdateCommand()
        {
            if (_achievements.Any(a => !a.IsUpdated && a.CanUpdate))
            {
                foreach (var a in _achievements)
                {
                    if (a.CanUpdate)
                        a.IsUpdated = true;
                }
            }
            else
            {
                foreach (var a in _achievements)
                {
                    if (a.CanUpdate)
                        a.IsUpdated = false;
                }
            }
        }

        public CommandBase ToggleSelectedForDeleteCommand
        {
            get { return new DelegateCommand(ExecuteToggleSelectedForDeleteCommand); }
        }

        private void ExecuteToggleSelectedForDeleteCommand()
        {
            if (_achievements.Any(a => !a.IsDeleted && a.CanDelete))
            {
                foreach (var a in _achievements)
                {
                    if (a.CanDelete)
                        a.IsDeleted = true;
                }
            }
            else
            {
                foreach (var a in _achievements)
                {
                    if (a.CanDelete)
                        a.IsDeleted = false;
                }
            }
        }

        /// <summary>
        /// A single achievement to update or delete
        /// </summary>
        public class UpdateAchievementViewModel : ViewModelBase
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="UpdateAchievementViewModel"/> class.
            /// </summary>
            public UpdateAchievementViewModel(GeneratedAchievementViewModel achievement)
            {
                Achievement = achievement;

                if (!achievement.IsGenerated || achievement.CompareState == GeneratedCompareState.Same)
                    IsUpdated = false;
            }

            internal GeneratedAchievementViewModel Achievement { get; private set; }

            public string Title
            {
                get { return Achievement.Title; }
            }

            public bool CanUpdate
            {
                get { return Achievement.IsGenerated && Achievement.CompareState != GeneratedCompareState.Same; }
            }

            public bool CanDelete
            {
                get { return Achievement.Local.Achievement != null; }
            }

            public static readonly ModelProperty IsUpdatedProperty = ModelProperty.Register(typeof(UpdateLocalViewModel), "IsUpdated", typeof(bool), true, OnIsUpdatedChanged);
            public bool IsUpdated
            {
                get { return (bool)GetValue(IsUpdatedProperty); }
                set { SetValue(IsUpdatedProperty, value); }
            }

            private static void OnIsUpdatedChanged(object sender, ModelPropertyChangedEventArgs e)
            {
                var vm = (UpdateAchievementViewModel)sender;
                if (vm.IsUpdated)
                    vm.IsDeleted = false;
            }

            public static readonly ModelProperty IsDeletedProperty = ModelProperty.Register(typeof(UpdateLocalViewModel), "IsDeleted", typeof(bool), false, OnIsDeletedChanged);
            public bool IsDeleted
            {
                get { return (bool)GetValue(IsDeletedProperty); }
                set { SetValue(IsDeletedProperty, value); }
            }

            private static void OnIsDeletedChanged(object sender, ModelPropertyChangedEventArgs e)
            {
                var vm = (UpdateAchievementViewModel)sender;
                if (vm.IsDeleted)
                    vm.IsUpdated = false;
            }
        }
    }
}
