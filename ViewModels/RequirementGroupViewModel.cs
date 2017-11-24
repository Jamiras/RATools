using Jamiras.ViewModels;
using RATools.Data;
using System.Collections.Generic;

namespace RATools.ViewModels
{
    public class RequirementGroupViewModel : ViewModelBase
    {
        public RequirementGroupViewModel(string label, IEnumerable<Requirement> requirements, IDictionary<int, string> notes)
        {
            Label = label;

            var list = new List<RequirementViewModel>();
            foreach (var requirement in requirements)
                list.Add(new RequirementViewModel(requirement, notes));

            Requirements = list;
        }

        public RequirementGroupViewModel(string label, IEnumerable<Requirement> requirements, IEnumerable<Requirement> compareRequirements, IDictionary<int, string> notes)
        {
            Label = label;

            var unmatchedRequirements = new List<Requirement>(compareRequirements);
            var matches = new Dictionary<Requirement, Requirement>();

            foreach (var requirement in requirements)
            {
                for (int i = 0; i < unmatchedRequirements.Count; i++)
                {
                    if (unmatchedRequirements[i] == requirement)
                    {
                        matches[requirement] = unmatchedRequirements[i];
                        unmatchedRequirements.RemoveAt(i);
                        break;
                    }
                }
            }

            var list = new List<RequirementViewModel>();
            foreach (var requirement in requirements)
            {
                Requirement match;
                if (!matches.TryGetValue(requirement, out match))
                {
                    var bestScore = 0;
                    var matchIndex = -1;

                    for (int i = 0; i < unmatchedRequirements.Count; i++)
                    {
                        var test = unmatchedRequirements[i];
                        var score = 0;

                        if (test.Type == requirement.Type)
                            score += 1;
                        if (test.Operator == requirement.Operator)
                            score += 3;
                        if (test.HitCount == requirement.HitCount)
                            score += 2;
                        if (test.Left.Type == requirement.Left.Type)
                            score += 5;
                        if (test.Left.Size == requirement.Left.Size)
                            score += 3;
                        if (test.Left.Value == requirement.Left.Value)
                            score += 8;
                        if (test.Right.Type == requirement.Right.Type)
                            score += 5;
                        if (test.Right.Size == requirement.Right.Size)
                            score += 3;
                        if (test.Right.Value == requirement.Right.Value)
                            score += 8;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            matchIndex = i;
                        }
                    }

                    if (bestScore > 16)
                    {
                        match = unmatchedRequirements[matchIndex];
                        unmatchedRequirements.RemoveAt(matchIndex);
                    }
                }

                list.Add(new RequirementComparisonViewModel(requirement, match, notes));
            }

            foreach (var requirement in unmatchedRequirements)
                list.Add(new RequirementComparisonViewModel(null, requirement, notes));

            Requirements = list;
        }

        public string Label { get; private set; }
        public IEnumerable<RequirementViewModel> Requirements { get; private set; }
    }
}
