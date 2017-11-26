using Jamiras.Commands;
using Jamiras.Components;
using RATools.Data;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Windows;

namespace RATools.ViewModels
{
    public class LeaderboardViewModel : GeneratedItemViewModelBase
    {
        public LeaderboardViewModel(GameViewModel owner, Leaderboard leaderboard)
        {
            _leaderboard = leaderboard;
            Title = "Leaderboard: " + leaderboard.Title;

            var groups = new List<LeaderboardGroupViewModel>();

            var achievement = new AchievementBuilder();
            achievement.ParseRequirements(Tokenizer.CreateTokenizer(_leaderboard.Start));
            groups.Add(new LeaderboardGroupViewModel("Start Conditions", achievement.ToAchievement().CoreRequirements, owner.Notes)
            {
                CopyToClipboardCommand = new DelegateCommand(() => Clipboard.SetData(DataFormats.Text, _leaderboard.Start))
            });

            achievement = new AchievementBuilder();
            achievement.ParseRequirements(Tokenizer.CreateTokenizer(_leaderboard.Cancel));
            groups.Add(new LeaderboardGroupViewModel("Cancel Condition", achievement.ToAchievement().CoreRequirements, owner.Notes)
            {
                CopyToClipboardCommand = new DelegateCommand(() => Clipboard.SetData(DataFormats.Text, _leaderboard.Cancel))
            });

            achievement = new AchievementBuilder();
            achievement.ParseRequirements(Tokenizer.CreateTokenizer(_leaderboard.Submit));
            groups.Add(new LeaderboardGroupViewModel("Submit Condition", achievement.ToAchievement().CoreRequirements, owner.Notes)
            {
                CopyToClipboardCommand = new DelegateCommand(() => Clipboard.SetData(DataFormats.Text, _leaderboard.Submit))
            });

            groups.Add(new LeaderboardGroupViewModel("Value", _leaderboard.Value, owner.Notes)
            {
                CopyToClipboardCommand = new DelegateCommand(() => Clipboard.SetData(DataFormats.Text, _leaderboard.Value))
            });

            Groups = groups;
        }

        private readonly Leaderboard _leaderboard;

        public class LeaderboardGroupViewModel
        {
            public LeaderboardGroupViewModel(string label, IEnumerable<Requirement> requirements, IDictionary<int, string> notes)
            {
                Label = label;

                var conditions = new List<RequirementViewModel>();
                foreach (var requirement in requirements)
                    conditions.Add(new RequirementViewModel(requirement, notes));
                Conditions = conditions;
            }

            public LeaderboardGroupViewModel(string label, string valueString, IDictionary<int, string> notes)
            {
                Label = label;

                var conditions = new List<RequirementViewModel>();
                var tokenizer = Tokenizer.CreateTokenizer(valueString);
                while (tokenizer.NextChar != '\0')
                {
                    string requirement, note;
                    if (tokenizer.NextChar == 'v')
                    {
                        tokenizer.Advance();

                        var number = tokenizer.ReadNumber();
                        requirement = number.ToString();
                        note = null;
                    }
                    else
                    {
                        var field = AchievementBuilder.ReadField(tokenizer);
                        requirement = field.ToString();
                        note = (field.Type == FieldType.MemoryAddress) ? notes[(int)field.Value] : null;

                        if (tokenizer.NextChar == '*')
                        {
                            requirement += " * ";

                            tokenizer.Advance();
                            if (tokenizer.NextChar == '-')
                            {
                                requirement += '-';
                                tokenizer.Advance();
                            }

                            var number = tokenizer.ReadNumber();
                            requirement += number.ToString();
                        }
                    }

                    if (tokenizer.NextChar == '_')
                    {
                        tokenizer.Advance();
                        requirement += " + ";
                    }

                    conditions.Add(new RequirementViewModel(requirement, note));
                }

                Conditions = conditions;
            }

            public string Label { get; private set; }
            public IEnumerable<RequirementViewModel> Conditions { get; private set; }
            public CommandBase CopyToClipboardCommand { get; set; }
        }

        public IEnumerable<LeaderboardGroupViewModel> Groups { get; private set; }
    }
}
