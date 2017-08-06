using System;
using System.Collections.Generic;
using System.Windows;
using Jamiras.Commands;
using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Internal;

namespace RATools.ViewModels
{
    public class LeaderboardViewModel : AchievementViewModel
    {
        public LeaderboardViewModel(GameViewModel owner, Leaderboard leaderboard)
            : base(owner)
        {
            _leaderboard = leaderboard;
            Title = "Leaderboard: " + leaderboard.Title;
            Description = leaderboard.Description;
        }

        private readonly Leaderboard _leaderboard;

        protected override void UpdateLocal()
        {
        }

        protected override List<RequirementGroupViewModel> BuildRequirementGroups()
        {
            var groups = new List<RequirementGroupViewModel>();

            var achievement = new AchievementBuilder();
            achievement.ParseRequirements(Tokenizer.CreateTokenizer(_leaderboard.Start));
            groups.Add(new RequirementGroupViewModel("Start Conditions", achievement.ToAchievement().CoreRequirements, _owner.Notes)
            {
                CopyCommand = new DelegateCommand(() => Clipboard.SetData(DataFormats.Text, _leaderboard.Start))
            });

            achievement = new AchievementBuilder();
            achievement.ParseRequirements(Tokenizer.CreateTokenizer(_leaderboard.Cancel));
            groups.Add(new RequirementGroupViewModel("Cancel Condition", achievement.ToAchievement().CoreRequirements, _owner.Notes)
            {
                CopyCommand = new DelegateCommand(() => Clipboard.SetData(DataFormats.Text, _leaderboard.Cancel))
            });

            achievement = new AchievementBuilder();
            achievement.ParseRequirements(Tokenizer.CreateTokenizer(_leaderboard.Submit));
            groups.Add(new RequirementGroupViewModel("Submit Condition", achievement.ToAchievement().CoreRequirements, _owner.Notes)
            {
                CopyCommand = new DelegateCommand(() => Clipboard.SetData(DataFormats.Text, _leaderboard.Submit))
            });


            var group = new RequirementGroupViewModel("Value", new Requirement[0], _owner.Notes)
            {
                CopyCommand = new DelegateCommand(() => Clipboard.SetData(DataFormats.Text, _leaderboard.Value))
            };
            ((List<RequirementViewModel>)group.Requirements).Add(new RequirementViewModel(_leaderboard.Value, String.Empty));
            groups.Add(group);

            return groups;
        }
    }
}
