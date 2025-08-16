using Jamiras.Components;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class ClassMemberReferenceExpression : VariableExpressionBase, INestedExpressions, IValueExpression, IAssignableExpression
    {
        public ClassMemberReferenceExpression(VariableExpression variable, VariableExpression member)
            : base(string.Empty)
        {
            Variable = variable;
            Member = member;

            // assert: Name is not used for IndexedVariableExpression
        }
        private ClassMemberReferenceExpression(IValueExpression source, VariableExpression member)
            : base(string.Empty)
        {
            _source = source;
            Member = member;
        }

        private readonly IValueExpression _source;

        /// <summary>
        /// Gets the variable expression.
        /// </summary>
        public VariableExpression Variable { get; private set; }

        /// <summary>
        /// Gets the member expression.
        /// </summary>
        public VariableExpression Member { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            if (Variable != null)
                Variable.AppendString(builder);
            else
                ((ExpressionBase)_source).AppendString(builder);

            builder.Append('.');
            Member.AppendString(builder);
        }

        internal static ExpressionBase Parse(ExpressionBase clause, PositionalTokenizer tokenizer)
        {
            if (tokenizer.NextChar != '.')
                return null;
            tokenizer.Advance();

            int line = tokenizer.Line;
            int column = tokenizer.Column;
            var identifier = tokenizer.ReadIdentifier();
            if (identifier.IsEmpty)
                return new ErrorExpression("Expected identifier not found after period", line, column - 1, line, column);

            var member = new VariableExpression(identifier.ToString(), line, column);

            ClassMemberReferenceExpression memberReference;
            var variable = clause as VariableExpression;
            if (variable != null)
            {
                memberReference = new ClassMemberReferenceExpression(variable, member);
            }
            else
            {
                var keyword = clause as KeywordExpression;
                if (keyword != null && keyword.Keyword == "this")
                {
                    variable = new VariableExpression(keyword.Keyword, keyword.Location.Start.Line, keyword.Location.Start.Column);
                    memberReference = new ClassMemberReferenceExpression(variable, member);
                }
                else
                {
                    var value = clause as IValueExpression;
                    if (value != null)
                        memberReference = new ClassMemberReferenceExpression(value, member);
                    else
                        return new ErrorExpression("Cannot reference members of " + clause.Type.ToLowerString(), clause);
                }
            }

            memberReference.Location = new TextRange(clause.Location.Start.Line, clause.Location.Start.Column, tokenizer.Line, tokenizer.Column - 1);
            return memberReference;
        }

        private class ClassFunctionDefinitionExpression : FunctionDefinitionExpression
        {
            public ClassFunctionDefinitionExpression(FunctionDefinitionExpression function, ClassInstanceExpression instance)
                : base(function)
            {
                _instance = instance;
            }

            private readonly ClassInstanceExpression _instance;

            internal override void AppendString(StringBuilder builder)
            {
                int index = builder.Length;
                base.AppendString(builder);
                builder.Insert(index + 9, _instance.ClassName + "::");
            }

            public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
            {
                scope.AssignVariable(new VariableExpression("this"), _instance);
                return base.Evaluate(scope, out result);
            }

            public override bool Invoke(InterpreterScope scope, out ExpressionBase result)
            {
                scope.AssignVariable(new VariableExpression("this"), _instance);
                return base.Invoke(scope, out result);
            }
        }

        /// <summary>
        /// Evaluates an expression
        /// </summary>
        /// <returns><see cref="ErrorExpression"/> indicating the failure, or the result of evaluating the expression.</returns>
        public ExpressionBase Evaluate(InterpreterScope scope)
        {
            ExpressionBase value;
            if (_source != null)
                value = _source.Evaluate(scope);
            else
                value = Variable.GetValue(scope);

            if (value is ErrorExpression)
                return value;

            var instance = value as ClassInstanceExpression;
            if (instance == null)
                return new ConversionErrorExpression(value, ExpressionType.ClassInstance);

            value = instance.GetFieldValue(Member.Name);
            if (value != null)
                return value;

            var functionDefinition = instance.GetFunctionDefinition(Member.Name);
            if (functionDefinition != null)
                return new ClassFunctionDefinitionExpression(functionDefinition, instance);

            return new UnknownVariableParseErrorExpression(Member.Name + " is not a member of " + instance.ClassName, Member);
        }

        /// <summary>
        /// Replaces the variables in the expression with values from <paramref name="scope" />.
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="result">[out] The new expression containing the replaced variables.</param>
        /// <returns>
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ErrorExpression" />.
        /// </returns>
        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            ExpressionBase value = Evaluate(scope);
            if (value is ErrorExpression)
            {
                result = value;
                return false;
            }

            return value.ReplaceVariables(scope, out result);
        }

        /// <summary>
        /// Updates the data referenced by the expression to the <see cref="newValue"/>.
        /// </summary>
        /// <returns><see cref="ErrorExpression"/> indicating the failure, or the result of evaluating the expression.</returns>
        public ErrorExpression Assign(InterpreterScope scope, ExpressionBase newValue)
        {
            var source = (Variable != null) ? Variable.GetValue(scope) : _source.Evaluate(scope);
            if (source is ErrorExpression)
                return (ErrorExpression) source;

            var instance = source as ClassInstanceExpression;
            if (instance == null)
                return new ConversionErrorExpression(source, ExpressionType.ClassInstance);

            if (instance.GetFieldValue(Member.Name) == null)
                return new UnknownVariableParseErrorExpression(Member.Name + " is not a member of " + instance.ClassName, Member);

            instance.SetFieldValue(Member.Name, newValue);
            return null;
        }

        /// <summary>
        /// Determines whether the specified <see cref="IndexedVariableExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="IndexedVariableExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="IndexedVariableExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as ClassMemberReferenceExpression;
            return that != null && Variable == that.Variable && Member == that.Member &&
                _source == that._source;
        }

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                if (Variable != null)
                    yield return Variable;
                else if (_source is ExpressionBase)
                    yield return (ExpressionBase)_source;

                yield return Member;
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            var nested = _source as INestedExpressions;
            if (nested != null)
                nested.GetDependencies(dependencies);

            if (Variable != null && Variable.Name != "this")
            {
                nested = Variable as INestedExpressions;
                if (nested != null)
                    nested.GetDependencies(dependencies);
            }

            // we can't know the type of the source object, so just
            // declare dependance on all fields matching the provided name
            dependencies.Add("." + Member.Name);
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
        }
    }
}
