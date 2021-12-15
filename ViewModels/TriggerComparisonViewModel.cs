using RATools.Data;
using System.Collections.Generic;
using System.Linq;

namespace RATools.ViewModels
{
    public class TriggerComparisonViewModel : TriggerViewModel
    {
        public TriggerComparisonViewModel(TriggerViewModel trigger, TriggerViewModel compareTrigger, NumberFormat numberFormat, IDictionary<int, string> notes)
            : base(trigger.Label, (Achievement)null, numberFormat, notes)
        {
            var compareTriggerGroups = new List<RequirementGroupViewModel>(compareTrigger.Groups);
            var groups = new List<RequirementGroupViewModel>();
            var emptyRequirements = new Requirement[0];

            foreach (var triggerGroup in trigger.Groups)
            {
                var compareTriggerGroup = compareTriggerGroups.FirstOrDefault(t => t.Label == triggerGroup.Label);
                if (compareTriggerGroup != null)
                {
                    groups.Add(new RequirementGroupViewModel(triggerGroup.Label,
                        triggerGroup.Requirements.Select(r => r.Requirement),
                        compareTriggerGroup.Requirements.Select(r => r.Requirement),
                        numberFormat, notes));
                    compareTriggerGroups.Remove(compareTriggerGroup);
                }
                else
                {
                    groups.Add(new RequirementGroupViewModel(triggerGroup.Label,
                        triggerGroup.Requirements.Select(r => r.Requirement),
                        emptyRequirements, numberFormat, notes));
                }
            }

            foreach (var compareTriggerGroup in compareTriggerGroups)
            {
                groups.Add(new RequirementGroupViewModel(compareTriggerGroup.Label, emptyRequirements,
                    compareTriggerGroup.Requirements.Select(r => r.Requirement),
                    numberFormat, notes));
            }

            Groups = groups.ToArray();
        }
    }
}
