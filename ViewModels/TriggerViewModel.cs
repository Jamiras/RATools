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
                groups.Add(new RequirementGroupViewModel("Core", achievement.CoreRequirements, numberFormat, notes));

                int i = 0;
                foreach (var alt in achievement.AlternateRequirements)
                {
                    i++;
                    groups.Add(new RequirementGroupViewModel("Alt " + i, alt, numberFormat, notes));
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
}
