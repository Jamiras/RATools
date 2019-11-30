using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.ViewModels;
using RATools.Data;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace RATools.ViewModels
{
    public class RequirementViewModel : ViewModelBase
    {
        public RequirementViewModel(Requirement requirement, NumberFormat numberFormat, IDictionary<int, string> notes)
        {
            Requirement = requirement;

            if (requirement != null)
            {
                UpdateDefinition(numberFormat);

                if (requirement.Left.IsMemoryReference)
                {
                    if (requirement.Right.IsMemoryReference && requirement.Left.Value != requirement.Right.Value)
                    {
                        var builder = new StringBuilder();

                        string note;
                        if (notes.TryGetValue((int)requirement.Left.Value, out note))
                            builder.AppendFormat("0x{0:x6}:{1}", requirement.Left.Value, note);

                        if (notes.TryGetValue((int)requirement.Right.Value, out note))
                        {
                            if (builder.Length > 0)
                                builder.AppendLine();
                            builder.AppendFormat("0x{0:x6}:{1}", requirement.Right.Value, note);
                        }

                        Notes = builder.ToString();
                    }
                    else
                    {
                        string note;
                        if (notes.TryGetValue((int)requirement.Left.Value, out note))
                            Notes = note;
                    }
                }
                else if (requirement.Right.IsMemoryReference)
                {
                    string note;
                    if (notes.TryGetValue((int)requirement.Right.Value, out note))
                        Notes = note;
                }
            }
        }

        public RequirementViewModel(string definition, string notes)
        {
            Definition = definition;
            Notes = notes;
        }

        internal Requirement Requirement { get; private set; }

        public static readonly ModelProperty DefinitionProperty = ModelProperty.Register(typeof(RequirementViewModel), "Definition", typeof(string), "");
        public string Definition
        {
            get { return (string)GetValue(DefinitionProperty); }
            private set { SetValue(DefinitionProperty, value); }
        }


        public static readonly ModelProperty WrappedDefinitionProperty = 
            ModelProperty.RegisterDependant(typeof(RequirementViewModel), "WrappedDefinition", typeof(string), new[] { DefinitionProperty }, GetWrappedDefinition);

        public string WrappedDefinition
        {
            get { return (string)GetValue(WrappedDefinitionProperty); }
        }

        private static string GetWrappedDefinition(ModelBase model)
        {
            var definition = ((RequirementViewModel)model).Definition;

            if (definition.Length > 32)
            {
                var index = 32;
                while (!Char.IsWhiteSpace(definition[index]))
                    index--;

                if (index < 20)
                {
                    index = 32;
                    while (Char.IsLetterOrDigit(definition[index]))
                        index--;
                }

                definition = definition.Substring(0, index) + "\n     " + definition.Substring(index);
            }

            return definition;
        }        

        public string Notes { get; private set; }

        internal virtual void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e)
        {
            if (Requirement != null)
                UpdateDefinition((bool)e.NewValue ? NumberFormat.Hexadecimal : NumberFormat.Decimal);
        }

        private void UpdateDefinition(NumberFormat numberFormat)
        {
            Definition = BuildDefinition(Requirement, numberFormat);
        }

        protected string BuildDefinition(Requirement requirement, NumberFormat numberFormat)
        {
            var builder = new StringBuilder();
            switch (requirement.Type)
            {
                case RequirementType.AddHits:
                    builder.Append("AddHits ");
                    break;

                case RequirementType.AndNext:
                    builder.Append("AndNext ");
                    break;
            }

            if (IsValueDependentOnPreviousRequirement)
            {
                var builder2 = new StringBuilder();
                requirement.AppendString(builder2, numberFormat, "~", "~");
                builder2.Remove(0, 2); // remove "(~"
                for (int i = 0; i < builder2.Length; i++)
                {
                    if (builder2[i] == '~')
                    {
                        builder2.Remove(i, 2); // remove "~)"
                        break;
                    }
                }
                builder.Append(builder2);
            }
            else
            {
                requirement.AppendString(builder, numberFormat);
            }

            if (requirement.Type == RequirementType.SubSource)
            {
                // change " - " to "-" and add " + " to the end
                builder.Remove(0, 1);
                builder.Remove(1, 1);
                builder.Append(" + ");
            }

            return builder.ToString();
        }

        public static readonly ModelProperty IsValueDependentOnPreviousRequirementProperty = ModelProperty.Register(typeof(RequirementViewModel), "IsValueDependentOnPreviousRequirement", typeof(bool), false, OnIsValueDependentOnPreviousRequirementChanged);
        public bool IsValueDependentOnPreviousRequirement
        {
            get { return (bool)GetValue(IsValueDependentOnPreviousRequirementProperty); }
            set { SetValue(IsValueDependentOnPreviousRequirementProperty, value); }
        }

        private static void OnIsValueDependentOnPreviousRequirementChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            var numberFormat = ServiceRepository.Instance.FindService<ISettings>().HexValues ? NumberFormat.Hexadecimal : NumberFormat.Decimal;
            ((RequirementViewModel)sender).UpdateDefinition(numberFormat);
        }
    }
}
