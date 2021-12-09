﻿using Jamiras.DataModels;
using Jamiras.ViewModels;
using RATools.Data;
using System.Collections.Generic;
using System.Diagnostics;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Label}")]
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

        private const int MinimumMatchingScore = 12;

        private static int CalculateScore(Requirement left, Requirement right)
        {
            var score = 0;

            if (left.Type == right.Type)
                score++;
            else
                score--;

            if (left.Operator == right.Operator)
                score += 3;
            else
                score--;

            if (left.HitCount == right.HitCount)
                score += 2;
            else
                score--;

            if (left.Left.Type == right.Left.Type)
                score += 5;
            else
                score--;

            if (left.Left.Size == right.Left.Size)
                score += 3;
            else
                score--;

            if (left.Left.Value == right.Left.Value)
                score += 8;

            if (left.Right.Type == right.Right.Type)
                score += 5;
            else
                score--;

            if (left.Right.Size == right.Right.Size)
                score += 3;
            else
                score--;

            if (left.Right.Value == right.Right.Value)
                score += 8;

            return score;
        }

        private static int CalculateScore(RequirementEx left, RequirementEx right)
        {
            var score = 0;
            var unmatchedRequirements = new List<Requirement>(left.Requirements);
            foreach (var requirement in right.Requirements)
            {
                bool matched = false;
                for (int i = 0; i < unmatchedRequirements.Count; i++)
                {
                    if (unmatchedRequirements[i] == requirement)
                    {
                        unmatchedRequirements.RemoveAt(i);
                        score += 40;
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                    score -= 10;
            }

            score -= unmatchedRequirements.Count * 10;

            return score;
        }

        private void AppendRequirements(List<RequirementViewModel> list, RequirementEx left, RequirementEx right, NumberFormat numberFormat, IDictionary<int, string> notes)
        {
            if (right == null)
            {
                foreach (var requirement in left.Requirements)
                    list.Add(new RequirementComparisonViewModel(requirement, null, numberFormat, notes));
            }
            else if (left == null)
            {
                foreach (var requirement in right.Requirements)
                    list.Add(new RequirementComparisonViewModel(null, requirement, numberFormat, notes));
            }
            else if (left.Requirements.Count == right.Requirements.Count)
            {
                for (int i = 0; i < left.Requirements.Count; ++i)
                    list.Add(new RequirementComparisonViewModel(left.Requirements[i], right.Requirements[i], numberFormat, notes));
            }
            else
            {
                var unmatchedCompareRequirements = new List<Requirement>(right.Requirements);
                var matches = new Dictionary<Requirement, Requirement>();
                var unmatchedRequirements = new List<Requirement>();

                foreach (var requirement in left.Requirements)
                {
                    bool matched = false;
                    for (var i = 0; i < unmatchedCompareRequirements.Count; i++)
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
                            var score = CalculateScore(requirement, unmatchedCompareRequirements[j]); 
                            if (score > bestScore)
                            {
                                bestScore = score;
                                matchIndex = i;
                                compareIndex = j;
                            }
                        }
                    }

                    if (bestScore < MinimumMatchingScore)
                        break;

                    matches[unmatchedRequirements[matchIndex]] = unmatchedCompareRequirements[compareIndex];
                    unmatchedRequirements.RemoveAt(matchIndex);
                    unmatchedCompareRequirements.RemoveAt(compareIndex);
                }

                foreach (var requirement in left.Requirements)
                {
                    Requirement match;
                    matches.TryGetValue(requirement, out match);
                    list.Add(new RequirementComparisonViewModel(requirement, match, numberFormat, notes));
                }

                foreach (var requirement in unmatchedCompareRequirements)
                    list.Add(new RequirementComparisonViewModel(null, requirement, numberFormat, notes));
            }
        }

        public RequirementGroupViewModel(string label, IEnumerable<Requirement> requirements, IEnumerable<Requirement> compareRequirements, NumberFormat numberFormat, IDictionary<int, string> notes)
        {
            Label = label;

            var requirementExs = RequirementEx.Combine(requirements);
            var compareRequirementExs = RequirementEx.Combine(compareRequirements);

            var unmatchedCompareRequirementExs = new List<RequirementEx>(compareRequirementExs);
            var matches = new Dictionary<RequirementEx, RequirementEx>();
            var unmatchedRequirementExs = new List<RequirementEx>();

            foreach (var requirementEx in requirementExs)
            {
                bool matched = false;
                for (int i = 0; i < unmatchedCompareRequirementExs.Count; i++)
                {
                    if (unmatchedCompareRequirementExs[i] == requirementEx)
                    {
                        matches[requirementEx] = unmatchedCompareRequirementExs[i];
                        unmatchedCompareRequirementExs.RemoveAt(i);
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                    unmatchedRequirementExs.Add(requirementEx);
            }

            while (unmatchedRequirementExs.Count > 0)
            {
                var bestScore = 0;
                var matchIndex = -1;
                var compareIndex = -1;

                for (var i = 0; i < unmatchedRequirementExs.Count; i++)
                {
                    var requirementEx = unmatchedRequirementExs[i];
                    var evaluation = requirementEx.Evaluate();
                    if (evaluation != null)
                    {
                        for (var j = 0; j < unmatchedCompareRequirementExs.Count; j++)
                        {
                            if (unmatchedCompareRequirementExs[j].Evaluate() == evaluation)
                            {
                                bestScore = 1000;
                                matchIndex = i;
                                compareIndex = j;
                                break;
                            }
                        }
                    }

                    for (var j = 0; j < unmatchedCompareRequirementExs.Count; j++)
                    {
                        var score = CalculateScore(requirementEx, unmatchedCompareRequirementExs[j]);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            matchIndex = i;
                            compareIndex = j;
                        }
                    }
                }

                if (bestScore < MinimumMatchingScore)
                    break;

                matches[unmatchedRequirementExs[matchIndex]] = unmatchedCompareRequirementExs[compareIndex];
                unmatchedRequirementExs.RemoveAt(matchIndex);
                unmatchedCompareRequirementExs.RemoveAt(compareIndex);
            }

            var list = new List<RequirementViewModel>();
            foreach (var requirementEx in requirementExs)
            {
                RequirementEx match;
                matches.TryGetValue(requirementEx, out match);
                AppendRequirements(list, requirementEx, match, numberFormat, notes);
            }

            // allow an always_true() group to match an empty group
            if (list.Count == 0 &&
                unmatchedCompareRequirementExs.Count == 1 &&
                unmatchedCompareRequirementExs[0].Evaluate() == true)
            {
                AppendRequirements(list, unmatchedCompareRequirementExs[0], unmatchedCompareRequirementExs[0], numberFormat, notes);
                unmatchedCompareRequirementExs.Clear();
            }

            // any remaining unmatched items still need to be added
            foreach (var requirementEx in unmatchedCompareRequirementExs)
                AppendRequirements(list, null, requirementEx, numberFormat, notes);

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
