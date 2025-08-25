using Jamiras.DataModels;
using RATools.Data;
using System;
using System.Collections.Generic;

namespace RATools.ViewModels
{
    public class RequirementComparisonViewModel : RequirementViewModel
    {
        public RequirementComparisonViewModel(Requirement requirement, Requirement compareRequirement, NumberFormat numberFormat, IDictionary<uint, CodeNote> notes)
            : base(requirement, numberFormat, notes)
        {
            if (compareRequirement == null)
            {
                IsModified = true;
            }
            else
            {
                CompareRequirement = compareRequirement;
                UpdateOtherDefinition(numberFormat);

                if (requirement == null)
                {
                    IsModified = true;
                }
                else if (requirement.HitCount != compareRequirement.HitCount)
                {
                    // if the HitCounts differ, mark modified regardless of whether the evaluations are the same
                    IsModified = true;
                }
                else if (!compareRequirement.Equals(requirement))
                {
                    // not identical. check to see if they evaluate to the same value
                    var evaluation = requirement.Evaluate();
                    if (evaluation == null || compareRequirement.Evaluate() != evaluation)
                        IsModified = true;
                }
            }
        }

        public RequirementComparisonViewModel(string requirement, string compareRequirement)
            : base(requirement, String.Empty)
        {
            IsModified = (requirement != compareRequirement);
            OtherDefinition = compareRequirement;
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

        internal override void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e)
        {
            base.OnShowHexValuesChanged(e);

            if (CompareRequirement != null)
                UpdateOtherDefinition((bool)e.NewValue ? NumberFormat.Hexadecimal : NumberFormat.Decimal);
        }

        private void UpdateOtherDefinition(NumberFormat numberFormat)
        {
            OtherDefinition = BuildDefinition(CompareRequirement, numberFormat);
        }
    }
}
