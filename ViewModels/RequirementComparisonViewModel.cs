using Jamiras.DataModels;
using RATools.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace RATools.ViewModels
{
    public class RequirementComparisonViewModel : RequirementViewModel
    {
        public RequirementComparisonViewModel(Requirement requirement, Requirement compareRequirement, NumberFormat numberFormat, IDictionary<int, string> notes)
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

        internal override void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e)
        {
            base.OnShowHexValuesChanged(e);

            if (CompareRequirement != null)
                UpdateOtherDefinition((bool)e.NewValue ? NumberFormat.Hexadecimal : NumberFormat.Decimal);
        }

        private void UpdateOtherDefinition(NumberFormat numberFormat)
        {
            var builder = new StringBuilder();
            CompareRequirement.AppendString(builder, numberFormat);
            OtherDefinition = builder.ToString();
        }
    }
}
