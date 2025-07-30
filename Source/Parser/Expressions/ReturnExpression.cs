using Jamiras.Components;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class ReturnExpression : ExpressionBase, INestedExpressions, IExecutableExpression
    {
        public ReturnExpression(ExpressionBase value)
            : base(ExpressionType.Return)
        {
            Value = value;
            Location = value.Location;
        }

        public ReturnExpression(KeywordExpression keyword, ExpressionBase value)
            : this(value)
        {
            _keyword = keyword;
            Location = new Jamiras.Components.TextRange(keyword.Location.Start, value.Location.End);
        }

        internal KeywordExpression Keyword
        {
            get { return _keyword; }
        }
        private readonly KeywordExpression _keyword;

        /// <summary>
        /// Gets the value to be returned.
        /// </summary>
        public ExpressionBase Value { get; private set; }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("return ");
            Value.AppendString(builder);
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
            ExpressionBase value;
            if (!Value.ReplaceVariables(scope, out value))
            {
                result = value;
                return false;
            }

            result = new ReturnExpression(value);
            CopyLocation(result);
            return true;
        }

        /// <summary>
        /// Determines whether the specified <see cref="ReturnExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="ReturnExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="ReturnExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as ReturnExpression;
            return that != null && Value == that.Value;
        }

        internal static ExpressionBase Parse(KeywordExpression keyword, PositionalTokenizer tokenizer)
        {
            var clause = ExpressionBase.Parse(tokenizer);
            if (clause.Type == ExpressionType.Error)
            {
                var unexpectedCharacterError = clause as UnexpectedCharacterParseErrorExpression;
                if (unexpectedCharacterError != null)
                {
                    unexpectedCharacterError.Ignore();
                    return null;
                }

                return clause;
            }

            return new ReturnExpression(keyword, clause);
        }


        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                if (_keyword != null)
                    yield return _keyword;

                if (Value != null)
                    yield return Value;
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            var nested = Value as INestedExpressions;
            if (nested != null)
                nested.GetDependencies(dependencies);
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
        }

        public ErrorExpression Execute(InterpreterScope scope)
        {
            var returnScope = new InterpreterScope(scope) 
            { 
                Context = new AssignmentExpression(new VariableExpression("@return"), Value) 
            };

            var value = Value as IValueExpression;
            if (value == null)
                return new ErrorExpression("Cannot evaluate " + Value.Type.ToLowerString());

            ExpressionBase result = value.Evaluate(returnScope);
            var error = result as ErrorExpression;
            if (error != null)
                return error;

            scope.ReturnValue = result;
            scope.IsComplete = true;
            return null;
        }
    }
}
