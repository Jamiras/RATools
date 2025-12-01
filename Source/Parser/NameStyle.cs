using System;
using System.Text;

namespace RATools.Parser
{
    public enum NameStyle
    {
        None = 0,
        SnakeCase,  // lower_with_underscore
        PascalCase, // UpperEachFirst
        CamelCase,  // upperInMiddle
    }

    public static class NameStyleExtension
    {
        public static string BuildName(this NameStyle style, string fromText)
        {
            if (fromText == null)
                return null;

            if (fromText.Length == 0)
                return String.Empty;

            var name = new StringBuilder();
            var valid = false;
            var newWord = true;

            foreach (var c in fromText)
            {
                if (!Char.IsLetterOrDigit(c))
                {
                    // allow dashes and apostrophes as mid-word characters (don't -> dont)
                    if (c != '-' && c != '\'')
                        newWord = true;
                    else if (!newWord && name.Length > 0 && Char.IsDigit(name[name.Length - 1]))
                        newWord = true;

                    continue;
                }

                if (newWord)
                {
                    newWord = false;

                    if (!valid && Char.IsDigit(c))
                        name.Append('_');

                    valid = true;

                    switch (style)
                    {
                        case NameStyle.PascalCase:
                            if (Char.IsDigit(c) && Char.IsDigit(name[name.Length - 1]))
                                name.Append('_');

                            name.Append(Char.ToUpper(c));
                            continue;

                        case NameStyle.CamelCase:
                            if (name.Length != 0)
                            {
                                if (Char.IsDigit(c) && Char.IsDigit(name[name.Length - 1]))
                                    name.Append('_');

                                name.Append(Char.ToUpper(c));
                                continue;
                            }
                            break;

                        case NameStyle.SnakeCase:
                            if (name.Length != 0 && name[name.Length - 1] != '_')
                                name.Append('_');
                            break;

                        default:
                            break;
                    }
                }

                name.Append(Char.ToLower(c));
            }

            return valid ? name.ToString() : string.Empty;
        }
    }
}
