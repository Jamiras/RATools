using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.ViewModels;
using RATools.Data;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Requirement}")]
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

            if (Notes == null)
                Notes = String.Empty;
        }

        public RequirementViewModel(string definition, string notes)
        {
            Definition = definition;
            Notes = notes;
        }

        internal Requirement Requirement { get; private set; }

        public static readonly ModelProperty DefinitionProperty = ModelProperty.Register(typeof(RequirementViewModel), "Definition", typeof(string), String.Empty);
        public string Definition
        {
            get { return (string)GetValue(DefinitionProperty); }
            private set { SetValue(DefinitionProperty, value); }
        }

        public string Notes
        {
            get { return _notes; }
            private set
            {
                _notes = value;

                var index = value.IndexOf('\n');
                if (index != -1)
                {
                    NotesShort = value.Substring(0, index).TrimEnd();
                    IsNoteShortened = true;
                }
                else
                {
                    NotesShort = value;
                    IsNoteShortened = false;
                }
            }
        }
        private string _notes;

        public string NotesShort { get; private set; }

        public bool IsNoteShortened { get; private set; }

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

                case RequirementType.SubHits:
                    builder.Append("SubHits ");
                    break;

                case RequirementType.AndNext:
                    builder.Append("AndNext ");
                    break;

                case RequirementType.OrNext:
                    builder.Append("OrNext ");
                    break;
            }

            if (IsValueDependentOnPreviousRequirement)
            {
                var builder2 = new StringBuilder();
                requirement.AppendString(builder2, numberFormat, "~", "~");
                var i = 0;
                while (i < builder2.Length - 1)
                {
                    if (builder2[i+1] == '~' && builder2[i] == '(')
                    {
                        builder2.Remove(i, 2); // remove "(~"
                        break;
                    }
                    i++;
                }
                while (i < builder2.Length)
                {
                    if (builder2[i] == '~' && builder2[i + 1] == ')')
                    {
                        builder2.Remove(i, 2); // remove "~)"
                        break;
                    }
                    i++;
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
