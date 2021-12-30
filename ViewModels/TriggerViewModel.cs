using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.ViewModels;
using RATools.Data;
using RATools.Parser;
using System.Collections.Generic;
using System.Diagnostics;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Label}")]
    public class TriggerViewModel : ViewModelBase
    {
        public TriggerViewModel(string label, Achievement achievement, NumberFormat numberFormat, IDictionary<int, string> notes)
        {
            Label = label;

            var groups = new List<RequirementGroupViewModel>();
            if (achievement != null)
            {
                var groupLabel = (this is ValueViewModel) ? "Value" : "Core";
                groups.Add(new RequirementGroupViewModel(groupLabel, achievement.CoreRequirements, numberFormat, notes));

                int i = 0;
                groupLabel = "Alt ";
                if (this is ValueViewModel)
                {
                    i++;
                    groupLabel = "Value ";
                }

                foreach (var alt in achievement.AlternateRequirements)
                {
                    i++;
                    groups.Add(new RequirementGroupViewModel(groupLabel + i, alt, numberFormat, notes));
                }
            }

            Groups = groups.ToArray();
        }

        public TriggerViewModel(string label, string definition, NumberFormat numberFormat, IDictionary<int, string> notes)
            : this(label, CreateAchievement(definition), numberFormat, notes)
        {
        }

        private static Achievement CreateAchievement(string definition)
        {
            var achievementBuilder = new AchievementBuilder();
            achievementBuilder.ParseRequirements(Tokenizer.CreateTokenizer(definition));
            return achievementBuilder.ToAchievement();
        }

        public string Label { get; private set; }
        public IEnumerable<RequirementGroupViewModel> Groups { get; protected set; }

        public CommandBase CopyToClipboardCommand { get; set; }
    }

    public class ValueViewModel : TriggerViewModel
    {
        public ValueViewModel(string label, string definition, NumberFormat numberFormat, IDictionary<int, string> notes)
            : base(label, CreateValue(definition), numberFormat, notes)
        {
        }

        private static Achievement CreateValue(string definition)
        {
            var achievementBuilder = new AchievementBuilder();
            if (definition.Length > 2)
            {
                if (definition[1] == ':')
                    achievementBuilder.ParseRequirements(Tokenizer.CreateTokenizer(definition));
                else
                    achievementBuilder.ParseValue(Tokenizer.CreateTokenizer(definition));
            }
            return achievementBuilder.ToAchievement();
        }
    }
}
