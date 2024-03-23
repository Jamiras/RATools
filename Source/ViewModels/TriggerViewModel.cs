using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.ViewModels;
using RATools.Data;
using RATools.Parser;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Label}")]
    public class TriggerViewModel : ViewModelBase
    {
        public TriggerViewModel(string label, Achievement achievement, NumberFormat numberFormat, IDictionary<uint, string> notes)
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

            // if there are too many groups, the rendering engine will "hang" while trying to cerate the layout
            const int maxGroups = 600;
            if (groups.Count > maxGroups)
            {
                const int keepGroups = maxGroups - 100;
                groups.RemoveRange(keepGroups / 2, groups.Count - keepGroups);
                groups.Insert(keepGroups / 2, new RequirementGroupViewModel("...", new Requirement[0], numberFormat, notes));
            }

            Groups = groups.ToArray();
        }

        public TriggerViewModel(string label, string definition, NumberFormat numberFormat, IDictionary<uint, string> notes)
            : this(label, CreateAchievement(definition), numberFormat, notes)
        {
        }

        public TriggerViewModel(string label, IEnumerable<RequirementGroupViewModel> groups)
        {
            Label = label;
            Groups = groups;
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
        public ValueViewModel(string label, string definition, NumberFormat numberFormat, IDictionary<uint, string> notes)
            : base(label, CreateValue(definition), numberFormat, notes)
        {
        }

        private static Achievement CreateValue(string definition)
        {
            var valueBuilder = new ValueBuilder();
            valueBuilder.ParseValue(Tokenizer.CreateTokenizer(definition));

            var achievementBuilder = new AchievementBuilder();
            foreach (var requirement in valueBuilder.Values.First())
                achievementBuilder.CoreRequirements.Add(requirement);
            foreach (var alternateValue in valueBuilder.Values.Skip(1))
                achievementBuilder.AlternateRequirements.Add(alternateValue);

            return achievementBuilder.ToAchievement();
        }
    }
}
