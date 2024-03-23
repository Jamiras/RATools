using Jamiras.DataModels;
using Jamiras.ViewModels;
using RATools.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Label}")]
    public class RequirementGroupViewModel : ViewModelBase
    {
        public RequirementGroupViewModel(string label, IEnumerable<Requirement> requirements, NumberFormat numberFormat, IDictionary<uint, string> notes)
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

        public RequirementGroupViewModel(string label)
        {
            Label = label;
            Requirements = new List<RequirementViewModel>();
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
                score += 2;
            else
                score--;

            if (left.HitCount == right.HitCount)
                score += (left.HitCount > 0) ? 4 : 1;
            else
                score--;

            if (left.Left.Type == right.Left.Type)
            {
                if (left.Left.Size == right.Left.Size)
                    score += 2;
                else if (!left.Left.IsMemoryReference && !right.Left.IsMemoryReference)
                    score += 2;
                else
                    score--;

                if (left.Left.Value == right.Left.Value)
                    score += 8;
                else
                    score -= 2;
            }
            else
            {
                score -= 3;
            }

            if (left.Right.Type == right.Right.Type)
            {
                if (left.Right.Size == right.Right.Size)
                    score += 2;
                else if (!left.Right.IsMemoryReference && !right.Right.IsMemoryReference)
                    score += 2;
                else
                    score--;

                if (left.Right.Value == right.Right.Value)
                    score += 8;
                else
                    score -= 2;
            }
            else
            {
                score -= 3;
            }

            return score;
        }

        private static int CalculateScore(RequirementEx left, RequirementEx right)
        {
            var score = 0;
            var unmatchedRequirements = new List<Requirement>(left.Requirements);
            foreach (var requirement in right.Requirements)
            {
                int bestScore = 0;
                int bestIndex = -1;

                for (int i = 0; i < unmatchedRequirements.Count; i++)
                {
                    if (unmatchedRequirements[i] == requirement)
                    {
                        bestScore = 40;
                        bestIndex = i;
                        break;
                    }
                    else
                    {
                        var matchScore = CalculateScore(unmatchedRequirements[i], requirement);
                        if (matchScore > bestScore)
                        {
                            bestScore = matchScore;
                            bestIndex = i;
                        }
                    }
                }

                if (bestIndex == -1)
                {
                    score -= 10;
                }
                else
                {
                    score += bestScore;
                    unmatchedRequirements.RemoveAt(bestIndex);
                }
            }

            score -= unmatchedRequirements.Count * 10;

            return score;
        }

        private static bool GetBestMerge(List<RequirementViewModel> list, out int leftIndex, out int rightIndex)
        {
            int bestScore = MinimumMatchingScore - 1;
            int bestLeft = -1;
            int bestRight = -1;

            for (int i = 0; i < list.Count; i++)
            {
                var removedRequirement = list[i] as RequirementComparisonViewModel;
                if (removedRequirement == null || removedRequirement.Requirement != null)
                    continue;

                for (int j = 0; j < list.Count; j++)
                {
                    if (j == i)
                        continue;

                    var compareRequirement = list[j] as RequirementComparisonViewModel;
                    if (compareRequirement == null || compareRequirement.CompareRequirement != null)
                        continue;

                    var score = CalculateScore(removedRequirement.CompareRequirement, compareRequirement.Requirement);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestLeft = j;
                        bestRight = i;
                    }
                }
            }

            leftIndex = bestLeft;
            rightIndex = bestRight;
            return (bestLeft != -1);
        }

        private void AppendRequirements(List<RequirementViewModel> list, RequirementEx left, RequirementEx right, NumberFormat numberFormat, IDictionary<uint, string> notes)
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
                    var bestScore = MinimumMatchingScore - 1;
                    var matchIndex = -1;
                    var compareIndex = -1;

                    for (var i = 0; i < unmatchedRequirements.Count; i++)
                    {
                        var requirement = unmatchedRequirements[i];

                        for (var j = 0; j < unmatchedCompareRequirements.Count; j++)
                        {
                            var score = CalculateScore(requirement, unmatchedCompareRequirements[j]); 
                            if (score > bestScore)
                            {
                                bestScore = score;
                                matchIndex = i;
                                compareIndex = j;
                            }
                        }
                    }

                    if (matchIndex == -1)
                        break;

                    matches[unmatchedRequirements[matchIndex]] = unmatchedCompareRequirements[compareIndex];
                    unmatchedRequirements.RemoveAt(matchIndex);
                    unmatchedCompareRequirements.RemoveAt(compareIndex);
                }

                var rightIndex = 0;

                foreach (var requirement in left.Requirements)
                {
                    Requirement match;
                    if (matches.TryGetValue(requirement, out match))
                    {
                        var matchIndex = right.Requirements.IndexOf(match);
                        while (rightIndex < matchIndex)
                        {
                            var rightRequirement = right.Requirements[rightIndex++];
                            if (unmatchedCompareRequirements.Remove(rightRequirement))
                                list.Add(new RequirementComparisonViewModel(null, rightRequirement, numberFormat, notes));
                        }

                        rightIndex++;
                    }

                    list.Add(new RequirementComparisonViewModel(requirement, match, numberFormat, notes));
                }

                foreach (var requirement in unmatchedCompareRequirements)
                    list.Add(new RequirementComparisonViewModel(null, requirement, numberFormat, notes));
            }
        }

        public RequirementGroupViewModel(string label, IEnumerable<Requirement> requirements, IEnumerable<Requirement> compareRequirements, NumberFormat numberFormat, IDictionary<uint, string> notes)
        {
            Label = label;

            var requirementExs = RequirementEx.Combine(requirements);
            var compareRequirementExs = RequirementEx.Combine(compareRequirements);

            var unmatchedCompareRequirementExs = new List<RequirementEx>(compareRequirementExs);
            var matches = new Dictionary<RequirementEx, RequirementEx>();
            var unmatchedRequirementExs = new List<RequirementEx>();

            // first pass: find exact matches
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

            // second pass: find close matches
            while (unmatchedRequirementExs.Count > 0)
            {
                var bestScore = MinimumMatchingScore - 1;
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

                if (matchIndex == -1)
                    break;

                matches[unmatchedRequirementExs[matchIndex]] = unmatchedCompareRequirementExs[compareIndex];
                unmatchedRequirementExs.RemoveAt(matchIndex);
                unmatchedCompareRequirementExs.RemoveAt(compareIndex);
            }

            // construct the output list from the requirements
            var pairs = new List<Tuple<RequirementEx, RequirementEx>>(matches.Count + unmatchedCompareRequirementExs.Count);
            foreach (var requirementEx in requirementExs)
            {
                RequirementEx match;
                matches.TryGetValue(requirementEx, out match);
                pairs.Add(new Tuple<RequirementEx, RequirementEx>(requirementEx, match));
            }

            // allow an always_true() group to match an empty group
            if (pairs.Count == 0 &&
                unmatchedCompareRequirementExs.Count == 1 &&
                unmatchedCompareRequirementExs[0].Evaluate() == true)
            {
                pairs.Add(new Tuple<RequirementEx, RequirementEx>(unmatchedCompareRequirementExs[0], unmatchedCompareRequirementExs[0]));
                unmatchedCompareRequirementExs.Clear();
            }

            // third pass: insert any unmatched comparison requirements
            if (unmatchedCompareRequirementExs.Count > 0)
            {
                var indices = new int[compareRequirementExs.Count + 2];
                for (int i = 1; i < indices.Length - 1; ++i)
                    indices[i] = -2;
                indices[0] = -1;
                indices[compareRequirementExs.Count + 1] = compareRequirementExs.Count + 1;
                for (int i = 0; i < requirementExs.Count; i++)
                {
                    RequirementEx match;
                    if (matches.TryGetValue(requirementExs[i], out match))
                        indices[compareRequirementExs.IndexOf(match) + 1] = i;
                }

                foreach (var requirementEx in unmatchedCompareRequirementExs)
                {
                    var insertIndex = pairs.Count;

                    var requirementIndex = compareRequirementExs.IndexOf(requirementEx);
                    if (requirementIndex < compareRequirementExs.Count - 1)
                    {
                        for (int i = 1; i < indices.Length - 1; i++)
                        {
                            if (indices[i - 1] == requirementIndex - 1 || indices[i + 1] == requirementIndex + 1)
                            {
                                if (i - 1 < pairs.Count)
                                {
                                    insertIndex = i - 1;
                                    break;
                                }
                            }
                        }
                    }

                    if (insertIndex < pairs.Count)
                    {
                        for (int i = 0; i < indices.Length; i++)
                        {
                            if (indices[i] >= insertIndex)
                                indices[i]++;
                        }
                    }

                    if (insertIndex < indices.Length - 1)
                        indices[insertIndex + 1] = requirementIndex;
                    pairs.Insert(insertIndex, new Tuple<RequirementEx, RequirementEx>(null, requirementEx));
                }
            }

            // convert RequirementEx pairs to RequirementComparisonViewModels
            var list = new List<RequirementViewModel>();
            foreach (var pair in pairs)
                AppendRequirements(list, pair.Item1, pair.Item2, numberFormat, notes);

            // attempt to merge requirements that may have been separated into separate RequirementExs
            int leftIndex, rightIndex;
            while (GetBestMerge(list, out leftIndex, out rightIndex))
            {
                list[leftIndex] = new RequirementComparisonViewModel(list[leftIndex].Requirement, 
                    ((RequirementComparisonViewModel)list[rightIndex]).CompareRequirement, numberFormat, notes);
                list.RemoveAt(rightIndex);
            }

            Requirements = list;
        }

        public RequirementGroupViewModel(string label, IEnumerable<string> requirements, IEnumerable<string> compareRequirements, NumberFormat numberFormat, IDictionary<uint, string> notes)
        {
            Label = label;

            var unmatchedCompareRequirements = new List<string>(compareRequirements);
            var matches = new Dictionary<string, string>();
            var unmatchedRequirements = new List<string>();

            // first pass: find exact matches
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

            // second pass: find close matches
            while (unmatchedRequirements.Count > 0)
            {
                var bestScore = MinimumMatchingScore / 2 - 1;
                var matchIndex = -1;
                var compareIndex = -1;

                for (var i = 0; i < unmatchedRequirements.Count; i++)
                {
                    var requirement = unmatchedRequirements[i];
                    if (requirement[0] != '?')
                    {
                        var parts = requirement.Split('=');
                        if (parts.Length == 2)
                        {
                            for (var j = 0; j < unmatchedCompareRequirements.Count; j++)
                            {
                                var parts2 = unmatchedCompareRequirements[j].Split('=');
                                if (parts2.Length == 2)
                                {
                                    var score = (parts2[1] == parts[1]) ? parts[1].Length + 4 : 0;
                                    if (parts2[0] == parts[0])
                                    {
                                        score += 8;
                                    }
                                    else
                                    {
                                        int int1, int2;

                                        if (parts[0].StartsWith("0x") && !parts2[0].StartsWith("0x"))
                                        {
                                            if (Int32.TryParse(parts[0].Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int1) &&
                                                Int32.TryParse(parts2[0], out int2) && int1 == int2)
                                            {
                                                score += 8;
                                            }
                                        }
                                        else if (!parts[0].StartsWith("0x") && parts2[0].StartsWith("0x"))
                                        {
                                            if (Int32.TryParse(parts2[0].Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int1) &&
                                                Int32.TryParse(parts[0], out int2) && int1 == int2)
                                            {
                                                score += 8;
                                            }
                                        }

                                    }
                                    
                                    if (score > bestScore)
                                    {
                                        bestScore = score;
                                        matchIndex = i;
                                        compareIndex = j;
                                    }
                                }
                            }
                        }
                    }
                }

                if (matchIndex == -1)
                    break;

                matches[unmatchedRequirements[matchIndex]] = unmatchedCompareRequirements[compareIndex];
                unmatchedRequirements.RemoveAt(matchIndex);
                unmatchedCompareRequirements.RemoveAt(compareIndex);
            }

            var pairs = new List<Tuple<string, string>>(matches.Count + unmatchedCompareRequirements.Count);
            foreach (var requirement in requirements)
            {
                string match;
                matches.TryGetValue(requirement, out match);
                pairs.Add(new Tuple<string, string>(requirement, match));
            }

            // insert any unmatched comparison requirements
            if (unmatchedCompareRequirements.Count > 0)
            {
                var compareRequirementsList = new List<string>(compareRequirements);

                foreach (var requirement in unmatchedCompareRequirements)
                {
                    var requirementIndex = compareRequirementsList.IndexOf(requirement);
                    var prevRequirement = (requirementIndex > 0) ? compareRequirementsList[requirementIndex - 1] : null;
                    var nextRequirement = (requirementIndex < compareRequirementsList.Count - 1) ? compareRequirementsList[requirementIndex + 1] : null;

                    bool inserted = false;
                    for (int i = 0; i < pairs.Count; i++)
                    {
                        if (prevRequirement != null && pairs[i].Item2 == prevRequirement)
                        {
                            pairs.Insert(i + 1, new Tuple<string, string>(null, requirement));
                            inserted = true;
                            break;
                        }

                        if (nextRequirement != null && pairs[i].Item2 == nextRequirement)
                        {
                            pairs.Insert(i, new Tuple<string, string>(null, requirement));
                            inserted = true;
                            break;
                        }
                    }

                    if (!inserted)
                        pairs.Add(new Tuple<string, string>(null, requirement));
                }
            }

            var list = new List<RequirementViewModel>();
            foreach (var pair in pairs)
                list.Add(new RequirementComparisonViewModel(pair.Item1, pair.Item2));

            Requirements = list;
        }

        public string Label { get; private set; }
        public IEnumerable<RequirementViewModel> Requirements { get; private set; }

        internal void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e)
        {
            foreach (var requirement in Requirements)
                requirement.OnShowHexValuesChanged(e);
        }

        public bool RequirementsAreEqual(RequirementGroupViewModel that)
        {
            // quick check to make sure the same number of requirements exist
            if (Requirements.Count() != that.Requirements.Count())
                return false;

            // convert to requirement clauses and compare
            var requirementExs = RequirementEx.Combine(Requirements.Select(r => r.Requirement));
            var compareRequirementExs = RequirementEx.Combine(that.Requirements.Select(r => r.Requirement));

            if (requirementExs.Count != compareRequirementExs.Count)
                return false;

            foreach (var requirementEx in requirementExs)
            {
                bool matched = false;
                for (int i = 0; i < compareRequirementExs.Count; i++)
                {
                    if (compareRequirementExs[i] == requirementEx)
                    {
                        compareRequirementExs.RemoveAt(i);
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                    return false;
            }

            return true;
        }
    }
}
