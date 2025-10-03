using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.ViewModels;
using RATools.Data;
using RATools.Parser;
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
        public RequirementViewModel(Requirement requirement, NumberFormat numberFormat, IDictionary<uint, CodeNote> notes)
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

                        CodeNote note;
                        if (notes.TryGetValue(requirement.Left.Value, out note))
                        {
                            var subNote = note.GetSubNote(requirement.Left.Size);
                            builder.AppendFormat("0x{0:x6}:{1}", requirement.Left.Value, subNote ?? note.Summary);
                        }

                        if (notes.TryGetValue(requirement.Right.Value, out note))
                        {
                            if (builder.Length > 0)
                                builder.AppendLine();

                            var subNote = note.GetSubNote(requirement.Right.Size);
                            builder.AppendFormat("0x{0:x6}:{1}", requirement.Right.Value, subNote ?? note.Summary);
                        }

                        Notes = builder.ToString();
                    }
                    else
                    {
                        CodeNote note;
                        if (notes.TryGetValue(requirement.Left.Value, out note))
                        {
                            Notes = note.Note;
                            var subNote = note.GetSubNote(requirement.Left.Size);
                            if (subNote != null)
                            {
                                NotesShort = subNote;
                                IsNoteShortened = true;
                            }
                        }
                    }
                }
                else if (requirement.Right.IsMemoryReference)
                {
                    CodeNote note;
                    if (notes.TryGetValue(requirement.Right.Value, out note))
                    {
                        Notes = note.Note;
                        var subNote = note.GetSubNote(requirement.Left.Size);
                        if (subNote != null)
                        {
                            NotesShort = subNote;
                            IsNoteShortened = true;
                        }
                    }
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

                case RequirementType.AddAddress:
                    builder.Append("AddAddress ");
                    break;

                case RequirementType.AndNext:
                    builder.Append("AndNext ");
                    break;

                case RequirementType.OrNext:
                    builder.Append("OrNext ");
                    break;

                case RequirementType.MeasuredIf:
                    builder.Append("MeasuredIf ");
                    break;

                case RequirementType.ResetNextIf:
                    builder.Append("ResetNextIf ");
                    break;

                case RequirementType.Remember:
                    builder.Append("Remember ");
                    break;
            }

            var context = new ScriptBuilderContext { NumberFormat = numberFormat };
            if (IsValueDependentOnPreviousRequirement)
            {
                var builder2 = new StringBuilder();
                context.AppendRequirement(builder2, requirement);
                var clause = builder2.ToString();

                if (clause == "always_false()" || clause == "always_true()")
                {
                    // always_true/always_false indicates the requirement is a comparison
                    // of constants. if we're dependent on a previous requirement, assume
                    // the previous requirement is an AddSource and the constant is a
                    // modifier that shouldn't be collapsed to always_true/always_false.
                    var clone = requirement.Clone();
                    clone.Type = RequirementType.None;
                    clause = clone.ToString();
                }
                else if (clause.Contains("always_false()"))
                {
                    var clone = new Requirement
                    {
                        Left = requirement.Left,
                        Operator = requirement.Operator,
                        Right = requirement.Right
                    };
                    clone.Type = RequirementType.None;
                    clause = clause.Replace("always_false()", clone.ToString());
                }
                else if (clause.Contains("always_true()"))
                {
                    var clone = new Requirement
                    {
                        Left = requirement.Left,
                        Operator = requirement.Operator,
                        Right = requirement.Right
                    };
                    clone.Type = RequirementType.None;
                    clause = clause.Replace("always_true()", clone.ToString());
                }

                builder.Append(clause);
            }
            else
            {
                context.AppendRequirement(builder, requirement);
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
