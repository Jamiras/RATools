using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.Services;
using RATools.Data;
using RATools.Parser;
using RATools.Services;
using System.Collections.Generic;

namespace RATools.ViewModels
{
    public class LeaderboardViewModel : GeneratedItemViewModelBase
    {
        public LeaderboardViewModel(GameViewModel owner, Leaderboard leaderboard)
        {
            _clipboard = ServiceRepository.Instance.FindService<IClipboardService>();

            _leaderboard = leaderboard;
            Title = leaderboard.Title;
            Description = leaderboard.Description;

            var groups = new List<LeaderboardGroupViewModel>();

            var achievementBuilder = new AchievementBuilder();
            achievementBuilder.ParseRequirements(Tokenizer.CreateTokenizer(_leaderboard.Start));
            var achievement = achievementBuilder.ToAchievement();
            groups.Add(new LeaderboardGroupViewModel("Start Conditions", achievement.CoreRequirements, owner.Notes)
            {
                CopyToClipboardCommand = new DelegateCommand(() => _clipboard.SetData(_leaderboard.Start))
            });
            var i = 1;
            foreach (var alt in achievement.AlternateRequirements)
            {
                groups.Add(new LeaderboardGroupViewModel("Alt " + i, alt, owner.Notes));
                i++;
            }

            achievementBuilder = new AchievementBuilder();
            achievementBuilder.ParseRequirements(Tokenizer.CreateTokenizer(_leaderboard.Cancel));
            achievement = achievementBuilder.ToAchievement();
            groups.Add(new LeaderboardGroupViewModel("Cancel Condition", achievement.CoreRequirements, owner.Notes)
            {
                CopyToClipboardCommand = new DelegateCommand(() => _clipboard.SetData(_leaderboard.Cancel))
            });
            i = 1;
            foreach (var alt in achievement.AlternateRequirements)
            {
                groups.Add(new LeaderboardGroupViewModel("Alt " + i, alt, owner.Notes));
                i++;
            }

            achievementBuilder = new AchievementBuilder();
            achievementBuilder.ParseRequirements(Tokenizer.CreateTokenizer(_leaderboard.Submit));
            achievement = achievementBuilder.ToAchievement();
            groups.Add(new LeaderboardGroupViewModel("Submit Condition", achievement.CoreRequirements, owner.Notes)
            {
                CopyToClipboardCommand = new DelegateCommand(() => _clipboard.SetData(_leaderboard.Submit))
            });
            i = 1;
            foreach (var alt in achievement.AlternateRequirements)
            {
                groups.Add(new LeaderboardGroupViewModel("Alt " + i, alt, owner.Notes));
                i++;
            }

            if (_leaderboard.Value.Length > 2 && _leaderboard.Value[1] == ':')
            {
                achievementBuilder = new AchievementBuilder();
                achievementBuilder.ParseRequirements(Tokenizer.CreateTokenizer(_leaderboard.Value));
                achievement = achievementBuilder.ToAchievement();

                groups.Add(new LeaderboardGroupViewModel("Value", achievement.CoreRequirements, owner.Notes)
                {
                    CopyToClipboardCommand = new DelegateCommand(() => _clipboard.SetData(_leaderboard.Value))
                });
                i = 1;
                foreach (var alt in achievement.AlternateRequirements)
                {
                    groups.Add(new LeaderboardGroupViewModel("Alt " + i, alt, owner.Notes));
                    i++;
                }
            }
            else
            {
                groups.Add(new LeaderboardGroupViewModel("Value", _leaderboard.Value, owner.Notes)
                {
                    CopyToClipboardCommand = new DelegateCommand(() => _clipboard.SetData(_leaderboard.Value))
                });
            }

            Groups = groups;

            UpdateLocalCommand = null;
            CanUpdate = false;
        }

        private readonly IClipboardService _clipboard;
        private readonly Leaderboard _leaderboard;

        public override bool IsGenerated
        {
            get { return true; }
        }

        public int SourceLine
        {
            get { return _leaderboard.SourceLine; }
        }

        public class LeaderboardGroupViewModel
        {
            public LeaderboardGroupViewModel(string label, IEnumerable<Requirement> requirements, IDictionary<int, string> notes)
            {
                Label = label;

                var numberFormat = ServiceRepository.Instance.FindService<ISettings>().HexValues ? NumberFormat.Hexadecimal : NumberFormat.Decimal;

                var conditions = new List<RequirementViewModel>();
                foreach (var requirement in requirements)
                    conditions.Add(new RequirementViewModel(requirement, numberFormat, notes));
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
                        var field = Field.Deserialize(tokenizer);
                        requirement = field.ToString();
                        note = null;
                        if (field.Type == FieldType.MemoryAddress)
                            notes.TryGetValue((int)field.Value, out note);

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

            internal void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e)
            {
                foreach (var condition in Conditions)
                    condition.OnShowHexValuesChanged(e);
            }
        }

        public IEnumerable<LeaderboardGroupViewModel> Groups { get; private set; }

        internal override void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e)
        {
            foreach (var group in Groups)
                group.OnShowHexValuesChanged(e);
            base.OnShowHexValuesChanged(e);
        }
    }
}
