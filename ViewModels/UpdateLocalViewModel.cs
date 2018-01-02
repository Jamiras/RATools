using Jamiras.DataModels;
using Jamiras.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace RATools.ViewModels
{
    public class UpdateLocalViewModel : DialogViewModelBase
    {
        public UpdateLocalViewModel(GameViewModel game)
        {
            _game = game;
            _achievements = new ObservableCollection<UpdateAchievementViewModel>();
            DialogTitle = "Update Local - " + game.Title;

            foreach (var achievement in game.Achievements.OfType<GeneratedAchievementViewModel>())
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
            foreach (var achievement in _achievements)
            {
                if (achievement.IsUpdated)
                    achievement.Achievement.UpdateLocalCommand.Execute();
                else if (achievement.IsDeleted)
                    achievement.Achievement.DeleteLocalCommand.Execute();
            }

            DialogResult = DialogResult.Ok;
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

                if (!achievement.IsGenerated || Achievement.LocalModified == ModifiedState.Unmodified)
                    IsUpdated = false;
            }

            internal GeneratedAchievementViewModel Achievement { get; private set; }

            public string Title
            {
                get { return Achievement.Title; }
            }

            public bool CanUpdate
            {
                get { return Achievement.IsGenerated && Achievement.LocalModified != ModifiedState.Unmodified; }
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
