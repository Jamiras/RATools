using Jamiras.Commands;
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
        public TriggerViewModel(string label, Trigger trigger, NumberFormat numberFormat, IDictionary<uint, string> notes)
        {
            Label = label;

            var groups = new List<RequirementGroupViewModel>();
            if (trigger != null)
            {
                var groupLabel = (this is ValueViewModel) ? "Value" : "Core";
                groups.Add(new RequirementGroupViewModel(groupLabel, trigger.Core.Requirements, numberFormat, notes));

                int i = 0;
                groupLabel = "Alt ";
                if (this is ValueViewModel)
                {
                    i++;
                    groupLabel = "Value ";
                }

                foreach (var alt in trigger.Alts)
                {
                    i++;
                    groups.Add(new RequirementGroupViewModel(groupLabel + i, alt.Requirements, numberFormat, notes));
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

        public TriggerViewModel(string label, IEnumerable<RequirementGroupViewModel> groups)
        {
            Label = label;
            Groups = groups;
        }

        public string Label { get; private set; }
        public IEnumerable<RequirementGroupViewModel> Groups { get; protected set; }

        public CommandBase CopyToClipboardCommand { get; set; }
    }

    public class ValueViewModel : TriggerViewModel
    {
        public ValueViewModel(string label, Value value, NumberFormat numberFormat, IDictionary<uint, string> notes)
            : base(label, WrapValue(value), numberFormat, notes)
        {
        }

        private static Trigger WrapValue(Value value)
        {
            if (value.Values.Count() == 0)
                return new Trigger();

            var triggerBuilder = new TriggerBuilder();
            foreach (var requirement in value.Values.First().Requirements)
                triggerBuilder.CoreRequirements.Add(requirement);
            foreach (var alternateValue in value.Values.Skip(1))
                triggerBuilder.AlternateRequirements.Add(alternateValue.Requirements.ToArray());

            return triggerBuilder.ToTrigger();
        }
    }
}
