using System;
using System.Collections.Generic;
using System.Text;
using Jamiras.DataModels;
using Jamiras.ViewModels;
using RATools.Data;

namespace RATools.ViewModels
{
    public class RequirementViewModel : ViewModelBase
    {
        public RequirementViewModel(Requirement requirement, IDictionary<int, string> notes)
        {
            Requirement = requirement;

            Definition = new ModifiableTextFieldViewModel();
            Definition.Text = requirement.ToString();
            Definition.AddPropertyChangedHandler(ModifiableTextFieldViewModel.TextProperty, OnDefinitionChanged);
            OnDefinitionChanged(Definition, new ModelPropertyChangedEventArgs(ModifiableTextFieldViewModel.TextProperty, "", Definition.Text));

            if (requirement.Right.Type == FieldType.Value ||
                (requirement.Right.Type == FieldType.PreviousValue && requirement.Right.Value == requirement.Left.Value))
            {
                string note;
                if (notes.TryGetValue((int)requirement.Left.Value, out note))
                    Notes = note;
            }
            else
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
        }

        public RequirementViewModel(string definition, string notes)
        {
            Definition = new ModifiableTextFieldViewModel();
            Definition.Text = definition;
            Notes = notes;
        }

        internal Requirement Requirement { get; private set; }

        public ModifiableTextFieldViewModel Definition { get; private set; }

        public static readonly ModelProperty WrappedDefinitionProperty = ModelProperty.Register(typeof(RequirementViewModel), "WrappedDefinition", typeof(string), "");
        public string WrappedDefinition
        {
            get { return (string)GetValue(WrappedDefinitionProperty); }
            private set { SetValue(WrappedDefinitionProperty, value); }
        }

        private void OnDefinitionChanged(object sender, ModelPropertyChangedEventArgs e)
        {
            var definition = Definition.Text;

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

            WrappedDefinition = definition;
        }        

        public string Notes { get; private set; }
    }
}
