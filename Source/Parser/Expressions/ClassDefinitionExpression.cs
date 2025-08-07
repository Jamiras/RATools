using Jamiras.Components;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class ClassDefinitionExpression : ExpressionBase, INestedExpressions, IExecutableExpression
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

        private KeywordExpression _keyword;

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

        public FunctionDefinitionExpression GetFunctionDefinition(string functionName)
        {
            return _functions.FirstOrDefault(f => f.Name.Name == functionName);
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
                if (_keyword != null)
                    yield return _keyword;

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
                var nested = field.Value as INestedExpressions;
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
                modifies.Add("." + field.Variable.Name);

            var functionModifies = new HashSet<string>();
            foreach (var function in _functions)
            {
                var nested = function as INestedExpressions;
                if (nested != null)
                {
                    functionModifies.Clear();
                    nested.GetModifications(functionModifies);

                    functionModifies.Remove(function.Name.Name);
                    modifies.Add("." + function.Name.Name);
                    foreach (var m in functionModifies)
                        modifies.Add(m);
                }
            }
        }

        /// <summary>
        /// Parses a class definition.
        /// </summary>
        /// <remarks>
        /// Assumes the 'class' keyword has already been consumed.
        /// </remarks>
        internal static ExpressionBase Parse(KeywordExpression keyword, PositionalTokenizer tokenizer)
        {
            var line = tokenizer.Line;
            var column = tokenizer.Column;

            var className = tokenizer.ReadIdentifier();
            if (className.IsEmpty)
                return null;

            var classNameVariable = new VariableDefinitionExpression(className.ToString(), line, column);
            var classDefinition = new ClassDefinitionExpression(classNameVariable);
            classDefinition._keyword = keyword;
            classDefinition.Location = keyword.Location;

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

        private class InstantiateClassFunction : FunctionDefinitionExpression
        {
            public InstantiateClassFunction(ClassDefinitionExpression classDefiniton)
                : base(classDefiniton.Name)
            {
                _classDefinition = classDefiniton;
            }

            private readonly ClassDefinitionExpression _classDefinition;

            public ErrorExpression Initialize(InterpreterScope scope)
            {
                var tempScope = new InterpreterScope(scope);
                foreach (var field in _classDefinition._fields)
                {
                    var error = field.Execute(tempScope);
                    if (error != null)
                        return error;

                    var value = tempScope.GetVariable(field.Variable.Name);
                    if (value == null)
                        return new ErrorExpression("Could not initialize field: " + field.Variable.Name, field);

                    Parameters.Add(new VariableDefinitionExpression(field.Variable.Name));
                    DefaultParameters[field.Variable.Name] = value;
                }

                return null;
            }

            public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
            {
                var instance = new ClassInstanceExpression(_classDefinition);

                foreach (var field in _classDefinition._fields)
                {
                    var value = scope.GetVariable(field.Variable.Name);
                    if (value == null)
                    {
                        result = new ErrorExpression("No value for field: " + field.Variable.Name, field);
                        return false;
                    }

                    instance.SetFieldValue(field.Variable.Name, value);
                }

                result = instance;
                return true;
            }
        }

        public ErrorExpression Execute(InterpreterScope scope)
        {
            var functionDefinition = new InstantiateClassFunction(this);
            var error = functionDefinition.Initialize(scope);
            if (error != null)
                return error;

            scope.AddFunction(functionDefinition);
            return null;
        }
    }
}
