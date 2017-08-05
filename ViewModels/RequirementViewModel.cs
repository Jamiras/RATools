using System;
using System.Collections.Generic;
using System.Text;
using Jamiras.ViewModels;
using RATools.Data;

namespace RATools.ViewModels
{
    public class RequirementViewModel : ViewModelBase
    {
        public RequirementViewModel(Requirement requirement, IDictionary<int, string> notes)
        {
            Definition = requirement.ToString();

            if (Definition.Length > 32)
            {
                var index = 32;
                while (!Char.IsWhiteSpace(Definition[index]))
                    index--;

                if (index < 20)
                {
                    index = 32;
                    while (Char.IsLetterOrDigit(Definition[index]))
                        index--;
                }

                Definition = Definition.Substring(0, index) + "\n     " + Definition.Substring(index);
            }

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
            Definition = definition;
            Notes = notes;
        }

        public string Definition { get; private set; }
        public string Notes { get; private set; }
    }
}
