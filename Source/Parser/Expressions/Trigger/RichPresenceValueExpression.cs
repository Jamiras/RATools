using RATools.Data;
using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class RichPresenceValueExpression : RichPresenceMacroExpressionBase
    {
        public RichPresenceValueExpression(StringConstantExpression name, ExpressionBase parameter)
            : base(name, parameter)
        {
        }

        public override string FunctionName { get { return "rich_presence_value"; } }

        public ValueFormat Format { get; set; }

        public static ValueFormat ParseFormat(string format)
        {
            var valueFormat = Leaderboard.ParseFormat(format);
            if (valueFormat == ValueFormat.None)
            {
                if (format == "ASCIICHAR")
                    valueFormat = ValueFormat.ASCIIChar;
                else if (format == "UNICODECHAR")
                    valueFormat = ValueFormat.UnicodeChar;
            }
            return valueFormat;
        }

        public static string GetFormatString(ValueFormat format)
        {
            switch (format)
            {
                case ValueFormat.ASCIIChar:
                    return "ASCIICHAR";

                case ValueFormat.UnicodeChar:
                    return "UNICODECHAR";

                default:
                    return Leaderboard.GetFormatString(format);
            }
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as RichPresenceValueExpression;
            return (that != null && that.Format == Format && that.Name == Name && that.Parameter == Parameter);
        }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("rich_presence_value(");
            Name.AppendString(builder);
            builder.Append(", ");
            Parameter.AppendString(builder);

            if (Format != ValueFormat.Value)
                builder.AppendFormat(", \"{0}\"", GetFormatString(Format));

            builder.Append(')');
        }

        public override ErrorExpression Attach(RichPresenceBuilder builder)
        {
            return builder.AddValueField(this, Name, Format);
        }
    }

    internal class RichPresenceMacroExpression : RichPresenceValueExpression
    {
        public RichPresenceMacroExpression(StringConstantExpression name, ExpressionBase parameter)
            : base(name, parameter)
        {
        }

        public override string FunctionName { get { return "rich_presence_macro"; } }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("rich_presence_macro(");
            Name.AppendString(builder);
            builder.Append(", ");
            Parameter.AppendString(builder);
            builder.Append(')');
        }
    }
}
