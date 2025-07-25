using Jamiras.Components;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class ClassDefinitionExpression : ExpressionBase, INestedExpressions
    {
        public ClassDefinitionExpression(string name)
            : this(new VariableDefinitionExpression(name))
        {
        }

        protected ClassDefinitionExpression(VariableDefinitionExpression name)
            : base(ExpressionType.ClassDefinition)
        {
            Name = name;

            _fields = new List<AssignmentExpression>();
            _functions = new List<FunctionDefinitionExpression>();
        }

        /// <summary>
        /// Gets the name of the function.
        /// </summary>
        public VariableDefinitionExpression Name { get; private set; }

        private readonly List<AssignmentExpression> _fields;
        private readonly List<FunctionDefinitionExpression> _functions;

        public ErrorExpression AddField(VariableExpression variable, ExpressionBase initialValue)
        {
            if (variable.GetType() != typeof(VariableExpression))
                return new ErrorExpression("Complex field name not allowed", variable);

            _fields.Add(new AssignmentExpression(variable, initialValue));
            return null;
        }

        public override bool IsConstant { get { return true; } }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            AppendString(builder);
            return builder.ToString();
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("class ");
            Name.AppendString(builder);
            builder.Append('(');

            builder.Append(_fields.Count);
            builder.Append(" field");
            if (_fields.Count != 1)
                builder.Append('s');
            builder.Append(", ");

            builder.Append(_functions.Count);
            builder.Append(" function");
            if (_functions.Count != 1)
                builder.Append('s');

            builder.Append(')');
        }

        /// <summary>
        /// Determines whether the specified <see cref="ClassDefinitionExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="ClassDefinitionExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="ClassDefinitionExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as ClassDefinitionExpression;
            if (that == null || Name != that.Name)
                return false;

            if (_fields.Count != that._fields.Count || _functions.Count != that._functions.Count)
                return false;

            for (int i = 0; i < _fields.Count; i++)
            {
                if (_fields[i] != that._fields[i])
                    return false;
            }

            for (int i = 0; i < _functions.Count; i++)
            {
                if (_functions[i] != that._functions[i])
                    return false;
            }

            return true;
        }


        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                if (!Location.IsEmpty)
                    yield return new KeywordExpression("class", Location.Start.Line, Location.Start.Column);

                if (Name != null)
                    yield return Name;

                foreach (var field in _fields)
                    yield return field;

                foreach (var function in _functions)
                    yield return function;
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            foreach (var field in _fields)
            {
                var nested = field as INestedExpressions;
                if (nested != null)
                    nested.GetDependencies(dependencies);
            }

            foreach (var function in _functions)
            {
                var nested = function as INestedExpressions;
                if (nested != null)
                    nested.GetDependencies(dependencies);
            }
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
            modifies.Add(Name.Name);

            foreach (var field in _fields)
            {
                // TODO: class field variable reference (this, field.Variable.Name)
            }

            foreach (var function in _functions)
            {
                // TODO: replace function name with class field function reference
                var nested = function as INestedExpressions;
                if (nested != null)
                    nested.GetModifications(modifies);
            }
        }

        /// <summary>
        /// Parses a class definition.
        /// </summary>
        /// <remarks>
        /// Assumes the 'class' keyword has already been consumed.
        /// </remarks>
        internal static ExpressionBase Parse(PositionalTokenizer tokenizer, int line = 0, int column = 0)
        {
            var locationStart = new TextLocation(line, column); // location of 'class' keyword

            SkipWhitespace(tokenizer);

            line = tokenizer.Line;
            column = tokenizer.Column;

            var className = tokenizer.ReadIdentifier();
            if (className.IsEmpty)
                return ParseError(tokenizer, "Invalid function name");

            var classNameVariable = new VariableDefinitionExpression(className.ToString(), line, column);
            var classDefinition = new ClassDefinitionExpression(classNameVariable);
            classDefinition.Location = new TextRange(locationStart.Line, locationStart.Column, 0, 0);

            return classDefinition.Parse(tokenizer);
        }

        protected new ExpressionBase Parse(PositionalTokenizer tokenizer)
        {
            SkipWhitespace(tokenizer);
            if (tokenizer.NextChar != '{')
                return ParseError(tokenizer, "Expected '{' after class name", Name);

            tokenizer.Advance();
            SkipWhitespace(tokenizer);

            while (tokenizer.NextChar != '}')
            {
                var expression = ExpressionBase.Parse(tokenizer);
                switch (expression.Type)
                {
                    case ExpressionType.Error:
                        return expression;

                    case ExpressionType.Assignment:
                        var assignment = (AssignmentExpression)expression;
                        if (assignment.Variable.GetType() != typeof(VariableExpression))
                            return new ErrorExpression("Complex field name not allowed", assignment.Variable);
                        _fields.Add(assignment);
                        break;

                    case ExpressionType.FunctionDefinition:
                        var function = (FunctionDefinitionExpression)expression;
                        _functions.Add(function);
                        break;

                    default:
                        return ParseError(tokenizer, "Only variable and function definitions allowed inside a class definition", expression);
                }

                SkipWhitespace(tokenizer);
            }

            tokenizer.Advance();

            Location = new TextRange(Location.Start, tokenizer.Location);
            return MakeReadOnly();
        }
    }
}
