using System;
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

        public IndexedVariableExpression(ExpressionBase variable, ExpressionBase index)
            : this(String.Empty, index)
        {
            Variable = variable;
        }

        public ExpressionBase Variable { get; private set; }
        public ExpressionBase Index { get; private set; }

        internal override void AppendString(StringBuilder builder)
        {
            if (Variable != null)
                Variable.AppendString(builder);
            else
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

            ExpressionBase value;
            if (Variable != null)
            {
                if (!Variable.ReplaceVariables(scope, out value))
                {
                    result = value;
                    return false;
                }
            }
            else
            {
                value = scope.GetVariable(Name);

                if (value == null)
                {
                    result = new ParseErrorExpression("Unknown variable: " + Name, Line, Column);
                    return false;
                }
            }

            var dict = value as DictionaryExpression;
            if (dict != null)
            {
                var entry = dict.Entries.FirstOrDefault(e => Object.Equals(e.Key, index));
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
