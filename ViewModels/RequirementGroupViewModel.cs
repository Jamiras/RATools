using System.Collections.Generic;
using Jamiras.ViewModels;
using RATools.Data;

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

        public string Label { get; private set; }
        public IEnumerable<RequirementViewModel> Requirements { get; private set; }
    }
}
