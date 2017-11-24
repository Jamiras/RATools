using Jamiras.DataModels;
using RATools.Data;
using System;
using System.Collections.Generic;

namespace RATools.ViewModels
{
    public class RequirementComparisonViewModel : RequirementViewModel
    {
        public RequirementComparisonViewModel(Requirement requirement, Requirement compareRequirement, IDictionary<int, string> notes)
            : base(requirement, notes)
        {
            if (compareRequirement == null)
            {
                IsModified = true;
            }
            else
            {
                OtherDefinition = compareRequirement.ToString();

                if (!compareRequirement.Equals(requirement))
                    IsModified = true;
            }
        }

        internal Requirement CompareRequirement { get; private set; }

        public static readonly ModelProperty IsModifiedProperty = ModelProperty.Register(typeof(RequirementComparisonViewModel), "IsModified", typeof(bool), false);
        public bool IsModified
        {
            get { return (bool)GetValue(IsModifiedProperty); }
            private set { SetValue(IsModifiedProperty, value); }
        }

        public static readonly ModelProperty OtherDefinitionProperty = ModelProperty.Register(typeof(RequirementComparisonViewModel), "OtherDefinition", typeof(string), String.Empty);
        public string OtherDefinition
        {
            get { return (string)GetValue(OtherDefinitionProperty); }
            private set { SetValue(OtherDefinitionProperty, value); }
        }
    }
}
