using System.Text;
using System.Linq;

namespace RATools.Parser.Internal
{
    internal class IndexedVariableExpression : VariableExpression
    {
        public IndexedVariableExpression(string name, ExpressionBase index)
            : base(name)
        {
            Index = index;
        }

        public ExpressionBase Index { get; private set; }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(Name);
            builder.Append('[');
            Index.AppendString(builder);
            builder.Append(']');
        }

        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            ExpressionBase index;
            if (!Index.ReplaceVariables(scope, out index))
            {
                result = index;
                return false;
            }

            ExpressionBase value = scope.GetVariable(Name);
            if (value == null)
            {
                result = new ParseErrorExpression("Unknown variable: " + Name);
                return false;
            }

            var dict = value as DictionaryExpression;
            if (dict != null)
            {
                var entry = dict.Entries.FirstOrDefault(e => e.Key == index);
                if (entry != null)
                {
                    result = entry.Value;
                    return true;
                }

                result = new ParseErrorExpression("No entry in dictionary matching " + index.ToString());
                return false;
            }

            result = new ParseErrorExpression("Cannot index " + value.ToString());
            return false;
        }
    }
}
