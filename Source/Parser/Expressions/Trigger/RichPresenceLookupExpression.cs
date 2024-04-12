using System.Text;

namespace RATools.Parser.Expressions.Trigger
{
    internal class RichPresenceLookupExpression : RichPresenceMacroExpressionBase
    {
        public RichPresenceLookupExpression(StringConstantExpression name, ExpressionBase parameter)
            : base(name, parameter)
        {
        }

        public override string FunctionName { get { return "rich_presence_lookup"; } }

        public DictionaryExpression Items { get; set; }

        public StringConstantExpression Fallback { get; set; }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as RichPresenceLookupExpression;
            return (that != null && that.Name == Name && that.Parameter == Parameter && that.Fallback == Fallback && that.Items == Items);
        }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("rich_presence_lookup(");
            Name.AppendString(builder);
            builder.Append(", ");
            Parameter.AppendString(builder);
            builder.Append(", { }");

            if (Fallback != null && Fallback.Value != "")
            {
                builder.Append(", ");
                Fallback.AppendString(builder);
            }

            builder.Append(')');
        }

        public override ErrorExpression Attach(RichPresenceBuilder builder)
        {
            return builder.AddLookupField(this, Name, Items, Fallback);
        }
    }
}
