using Jamiras.DataModels;
using Jamiras.ViewModels;
using RATools.Data;
using System.Collections.Generic;

namespace RATools.ViewModels
{
    public class RequirementGroupViewModel : ViewModelBase
    {
        public RequirementGroupViewModel(string label, IEnumerable<Requirement> requirements, NumberFormat numberFormat, IDictionary<int, string> notes)
        {
            Label = label;

            bool isValueDependentOnPreviousRequirement = false;
            var list = new List<RequirementViewModel>();
            foreach (var requirement in requirements)
            {
                var requirementViewModel = new RequirementViewModel(requirement, numberFormat, notes);
                requirementViewModel.IsValueDependentOnPreviousRequirement = isValueDependentOnPreviousRequirement;

                list.Add(requirementViewModel);

                switch (requirement.Type)
                {
                    case RequirementType.AddAddress:
                    case RequirementType.AddSource:
                    case RequirementType.SubSource:
                        isValueDependentOnPreviousRequirement = true;
                        break;

                    default:
                        isValueDependentOnPreviousRequirement = false;
                        break;
                }
            }

            Requirements = list;
        }

        public RequirementGroupViewModel(string label, IEnumerable<Requirement> requirements, IEnumerable<Requirement> compareRequirements, NumberFormat numberFormat, IDictionary<int, string> notes)
        {
            Label = label;

            var unmatchedCompareRequirements = new List<Requirement>(compareRequirements);
            var matches = new Dictionary<Requirement, Requirement>();
            var unmatchedRequirements = new List<Requirement>();

            foreach (var requirement in requirements)
            {
                bool matched = false;
                for (int i = 0; i < unmatchedCompareRequirements.Count; i++)
                {
                    if (unmatchedCompareRequirements[i] == requirement)
                    {
                        matches[requirement] = unmatchedCompareRequirements[i];
                        unmatchedCompareRequirements.RemoveAt(i);
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                    unmatchedRequirements.Add(requirement);
            }

            while (unmatchedRequirements.Count > 0)
            {
                var bestScore = 0;
                var matchIndex = -1;
                var compareIndex = -1;

                for (var i = 0; i < unmatchedRequirements.Count; i++)
                {
                    var requirement = unmatchedRequirements[i];
                    for (var j = 0; j < unmatchedCompareRequirements.Count; j++)
                    {
                        var test = unmatchedCompareRequirements[j];
                        var score = 0;

                        if (test.Type == requirement.Type)
                            score++;
                        else
                            score--;

                        if (test.Operator == requirement.Operator)
                            score += 3;
                        else
                            score--;

                        if (test.HitCount == requirement.HitCount)
                            score += 2;
                        else
                            score--;

                        if (test.Left.Type == requirement.Left.Type)
                            score += 5;
                        else
                            score--;

                        if (test.Left.Size == requirement.Left.Size)
                            score += 3;
                        else
                            score--;

                        if (test.Left.Value == requirement.Left.Value)
                            score += 8;

                        if (test.Right.Type == requirement.Right.Type)
                            score += 5;
                        else
                            score--;

                        if (test.Right.Size == requirement.Right.Size)
                            score += 3;
                        else
                            score--;

                        if (test.Right.Value == requirement.Right.Value)
                            score += 8;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            matchIndex = i;
                            compareIndex = j;
                        }
                    }
                }

                if (bestScore < 12)
                    break;

                matches[unmatchedRequirements[matchIndex]] = unmatchedCompareRequirements[compareIndex];
                unmatchedRequirements.RemoveAt(matchIndex);
                unmatchedCompareRequirements.RemoveAt(compareIndex);
            }

            var list = new List<RequirementViewModel>();
            foreach (var requirement in requirements)
            {
                Requirement match;
                matches.TryGetValue(requirement, out match);
                list.Add(new RequirementComparisonViewModel(requirement, match, numberFormat, notes));
            }

            foreach (var requirement in unmatchedCompareRequirements)
                list.Add(new RequirementComparisonViewModel(null, requirement, numberFormat, notes));

            Requirements = list;
        }

        public string Label { get; private set; }
        public IEnumerable<RequirementViewModel> Requirements { get; private set; }

        internal void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e)
        {
            foreach (var requirement in Requirements)
                requirement.OnShowHexValuesChanged(e);
        }
    }
}
