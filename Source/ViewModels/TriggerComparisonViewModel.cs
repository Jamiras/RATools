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

            // first pass, match non-alt groups by name
            var matches = new Dictionary<RequirementGroupViewModel, RequirementGroupViewModel>();
            foreach (var triggerGroup in trigger.Groups)
            {
                if (triggerGroup.Label.StartsWith("Alt"))
                    continue;

                var compareTriggerGroup = compareTriggerGroups.FirstOrDefault(t => t.Label == triggerGroup.Label);
                if (compareTriggerGroup != null)
                {
                    matches[triggerGroup] = compareTriggerGroup;
                    compareTriggerGroups.Remove(compareTriggerGroup);
                }
            }

            // second pass, match alt groups by exact content. alt group order doesn't matter.
            foreach (var triggerGroup in trigger.Groups)
            {
                if (!triggerGroup.Label.StartsWith("Alt"))
                    continue;

                var compareTriggerGroup = compareTriggerGroups.FirstOrDefault(t => t.RequirementsAreEqual(triggerGroup));
                if (compareTriggerGroup != null)
                {
                    matches[triggerGroup] = compareTriggerGroup;
                    compareTriggerGroups.Remove(compareTriggerGroup);
                }
            }

            // third pass, attempt to find close matches for remaining items
            if (matches.Count != trigger.Groups.Count())
            {
                foreach (var triggerGroup in trigger.Groups)
                {
                    if (!matches.ContainsKey(triggerGroup))
                    {
                        var compareTriggerGroup = FindBestMatch(triggerGroup, compareTriggerGroups);
                        if (compareTriggerGroup != null)
                        {
                            matches[triggerGroup] = compareTriggerGroup;
                            compareTriggerGroups.Remove(compareTriggerGroup);
                        }
                    }
                }
            }

            // create the comparison view models.
            foreach (var triggerGroup in trigger.Groups)
            {
                bool isRichPresence = triggerGroup.Requirements.Any(r => r.Requirement == null);

                RequirementGroupViewModel compareTriggerGroup;
                if (matches.TryGetValue(triggerGroup, out compareTriggerGroup))
                {
                    if (!triggerGroup.Requirements.Any())
                        isRichPresence = compareTriggerGroup.Requirements.Any(r => r.Requirement == null);
                }

                if (isRichPresence)
                {
                    // rich presence lookup comparison
                    if (compareTriggerGroup != null)
                    {
                        groups.Add(new RequirementGroupViewModel(triggerGroup.Label,
                            triggerGroup.Requirements.Select(r => r.Definition),
                            compareTriggerGroup.Requirements.Select(r => r.Definition),
                            numberFormat, notes));
                    }
                    else
                    {
                        groups.Add(new RequirementGroupViewModel(triggerGroup.Label,
                            triggerGroup.Requirements.Select(r => r.Definition),
                            new string[0], numberFormat, notes));
                    }
                }
                else
                {
                    // standard comparison
                    if (compareTriggerGroup != null)
                    {
                        groups.Add(new RequirementGroupViewModel(triggerGroup.Label,
                            triggerGroup.Requirements.Select(r => r.Requirement),
                            compareTriggerGroup.Requirements.Select(r => r.Requirement),
                            numberFormat, notes));
                    }
                    else
                    {
                        groups.Add(new RequirementGroupViewModel(triggerGroup.Label,
                            triggerGroup.Requirements.Select(r => r.Requirement),
                            emptyRequirements, numberFormat, notes));
                    }
                }
            }

            // append any compared view models that weren't used
            foreach (var compareTriggerGroup in compareTriggerGroups)
            {
                if (compareTriggerGroup.Requirements.Any(r => r.Requirement == null))
                {
                    // rich presence lookup comparison
                    groups.Add(new RequirementGroupViewModel(compareTriggerGroup.Label, new string[0],
                        compareTriggerGroup.Requirements.Select(r => r.Definition),
                        numberFormat, notes));
                }
                else
                {
                    // standard comparison
                    groups.Add(new RequirementGroupViewModel(compareTriggerGroup.Label, emptyRequirements,
                        compareTriggerGroup.Requirements.Select(r => r.Requirement),
                        numberFormat, notes));
                }
            }

            Groups = groups.ToArray();
        }

        private RequirementGroupViewModel FindBestMatch(RequirementGroupViewModel needle, IEnumerable<RequirementGroupViewModel> haystack)
        {
            if (needle.Requirements.Any(r => r.Requirement == null))
                return haystack.FirstOrDefault(g => g.Label == needle.Label);

            RequirementGroupViewModel bestMatch = null;
            int bestMatchCount = 0;
            var requirementExs = RequirementEx.Combine(needle.Requirements.Select(r => r.Requirement));

            foreach (var potentialMatch in haystack)
            {
                // convert to requirement clauses and compare
                var compareRequirementExs = RequirementEx.Combine(potentialMatch.Requirements.Select(r => r.Requirement));
                int matchCount = compareRequirementExs.Count;

                foreach (var requirementEx in requirementExs)
                {
                    for (int i = 0; i < compareRequirementExs.Count; i++)
                    {
                        if (compareRequirementExs[i] == requirementEx)
                        {
                            compareRequirementExs.RemoveAt(i);
                            break;
                        }
                    }
                }

                matchCount -= compareRequirementExs.Count;
                if (matchCount > bestMatchCount)
                {
                    bestMatchCount = matchCount;
                    bestMatch = potentialMatch;
                }
            }

            return bestMatch;
        }
    }
}
