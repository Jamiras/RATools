using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class ClassInstanceExpression : ExpressionBase, IValueExpression
    {
        public ClassInstanceExpression(ClassDefinitionExpression definition)
            : base(ExpressionType.ClassInstance)
        {
            _classDefinition = definition;

            _fieldValues = new Dictionary<string, ExpressionBase>();
        }

        private readonly ClassDefinitionExpression _classDefinition;
        private readonly Dictionary<string, ExpressionBase> _fieldValues;

        public string ClassName
        {
            get { return _classDefinition.Name.Name; }
        }

        public void SetFieldValue(string fieldName, ExpressionBase value)
        {
            _fieldValues[fieldName] = value;
        }

        public ExpressionBase GetFieldValue(string fieldName)
        {
            ExpressionBase value;
            if (!_fieldValues.TryGetValue(fieldName, out value))
                return null;

            return value;
        }

        public FunctionDefinitionExpression GetFunctionDefinition(string functionName)
        {
            return _classDefinition.GetFunctionDefinition(functionName);
        }

        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as ClassInstanceExpression;
            if (that == null || _classDefinition != that._classDefinition || _fieldValues.Count != that._fieldValues.Count)
                return false;

            foreach (var kvp in _fieldValues)
            {
                ExpressionBase value;
                if (!that._fieldValues.TryGetValue(kvp.Key, out value) || value != kvp.Value)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            AppendString(builder);
            return builder.ToString();
        }

        internal override void AppendString(StringBuilder builder)
        {
            builder.Append(_classDefinition.Name.Name);
            builder.Append(" {");

            foreach (var kvp in _fieldValues)
            {
                builder.Append(kvp.Key);
                builder.Append(": ");
                kvp.Value.AppendString(builder);
                builder.Append(", ");
            }

            if (_fieldValues.Count > 0)
                builder.Length -= 2;

            builder.Append('}');
        }

        /// <summary>
        /// Evaluates an expression
        /// </summary>
        /// <returns><see cref="ErrorExpression"/> indicating the failure, or the result of evaluating the expression.</returns>
        ExpressionBase IValueExpression.Evaluate(InterpreterScope scope)
        {
            return this;
        }
    }
}
