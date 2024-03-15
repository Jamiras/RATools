using Jamiras.Components;
using RATools.Parser.Internal;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace RATools.Parser.Expressions
{
    public class IfExpression : ExpressionBase, INestedExpressions, IExecutableExpression
    {
        public IfExpression(ExpressionBase condition)
            : base(ExpressionType.If)
        {
            Condition = condition;
            Expressions = new List<ExpressionBase>();
            ElseExpressions = new List<ExpressionBase>();
        }

        private KeywordExpression _keyword, _elseKeyword;

        /// <summary>
        /// Gets the condition expression.
        /// </summary>
        public ExpressionBase Condition { get; private set; }

        /// <summary>
        /// Gets the expressions to execute if the condition evaluates true.
        /// </summary>
        public ICollection<ExpressionBase> Expressions { get; private set; }

        /// <summary>
        /// Gets the expressions to execute if the condition evaluates false.
        /// </summary>
        public ICollection<ExpressionBase> ElseExpressions { get; private set; }

        /// <summary>
        /// Parses a if definition.
        /// </summary>
        /// <remarks>
        /// Assumes the 'if' keyword has already been consumed.
        /// </remarks>
        internal static ExpressionBase Parse(PositionalTokenizer tokenizer, int line = 0, int column = 0)
        {
            SkipWhitespace(tokenizer);

            ExpressionBase condition;
            if (tokenizer.NextChar == '(')
            {
                // special implementation of ExpressionBase.ParseClauseCore for '(' to avoid a
                // variable reference being classified as the an anonymous function parameter list
                tokenizer.Advance();

                condition = ExpressionBase.Parse(tokenizer);
                if (condition.Type == ExpressionType.Error)
                    return condition;

                if (tokenizer.NextChar != ')')
                {
                    if (tokenizer.NextChar == '\0')
                        return ParseError(tokenizer, "No closing parenthesis found");

                    return ParseError(tokenizer, "Expected closing parenthesis, found: " + tokenizer.NextChar);
                }
                tokenizer.Advance();
                condition.IsLogicalUnit = true;

                condition = ExpressionBase.ParseClauseExtension(condition, tokenizer);
                if (condition.Type == ExpressionType.Error)
                    return condition;

                condition.IsLogicalUnit = true;
            }
            else
            {
                condition = ExpressionBase.Parse(tokenizer);
                if (condition.Type == ExpressionType.Error)
                    return condition;
            }

            var ifExpression = new IfExpression(condition);
            ifExpression._keyword = new KeywordExpression("if", line, column);

            var error = ParseStatementBlock(tokenizer, ifExpression.Expressions);
            if (error != null)
                return error;

            SkipWhitespace(tokenizer);

            if (tokenizer.MatchSubstring("else") == 4)
            {
                tokenizer.PushState();
                bool isElse = tokenizer.ReadIdentifier() == "else";
                tokenizer.PopState();

                if (isElse)
                {
                    ifExpression._elseKeyword = new KeywordExpression("else", tokenizer.Line, tokenizer.Column);
                    tokenizer.Advance(4);

                    error = ParseStatementBlock(tokenizer, ifExpression.ElseExpressions);
                    if (error != null)
                        return error;
                }
            }

            return ifExpression;
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            builder.Append("if (");
            Condition.AppendString(builder);
            builder.Append(") { ... }");

            if (ElseExpressions.Count > 0)
                builder.Append(" else { ... }");
        }

        /// <summary>
        /// Determines whether the specified <see cref="IfExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="IfExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="IfExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as IfExpression;
            return that != null && Condition == that.Condition && ExpressionsEqual(Expressions, that.Expressions) &&
                ExpressionsEqual(ElseExpressions, that.ElseExpressions);
        }

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                if (_keyword != null)
                    yield return _keyword;

                if (Condition != null)
                    yield return Condition;

                foreach (var expression in Expressions)
                    yield return expression;

                if (_elseKeyword != null)
                    yield return _elseKeyword;

                foreach (var expression in ElseExpressions)
                    yield return expression;
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            var nested = Condition as INestedExpressions;
            if (nested != null)
                nested.GetDependencies(dependencies);

            foreach (var expression in Expressions)
            {
                nested = expression as INestedExpressions;
                if (nested != null)
                    nested.GetDependencies(dependencies);
            }

            foreach (var expression in ElseExpressions)
            {
                nested = expression as INestedExpressions;
                if (nested != null)
                    nested.GetDependencies(dependencies);
            }
        }
        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
            foreach (var expression in Expressions)
            {
                var nested = expression as INestedExpressions;
                if (nested != null)
                    nested.GetModifications(modifies);
            }

            foreach (var expression in ElseExpressions)
            {
                var nested = expression as INestedExpressions;
                if (nested != null)
                    nested.GetModifications(modifies);
            }
        }

        public ErrorExpression Execute(InterpreterScope scope)
        {
            ErrorExpression error;
            bool? result = Condition.IsTrue(scope, out error);
            if (result == null)
            {
                ExpressionBase value;
                if (!Condition.ReplaceVariables(scope, out value))
                    return (ErrorExpression)value;

                result = value.IsTrue(scope, out error);
                if (result == null)
                {
                    if (ContainsRuntimeLogic(value))
                        return new ErrorExpression("Comparison contains runtime logic.", Condition);

                    return new ErrorExpression("Condition did not evaluate to a boolean.", Condition) { InnerError = error };
                }
            }

            return AchievementScriptInterpreter.Execute(result.GetValueOrDefault() ? Expressions : ElseExpressions, scope);
        }

        private static bool ContainsRuntimeLogic(ExpressionBase expression)
        {
            switch (expression.Type)
            {
                case ExpressionType.MemoryAccessor:
                case ExpressionType.Requirement:
                    return true;

                default:
                    var nested = expression as INestedExpressions;
                    if (nested != null)
                    {
                        foreach (var nestedExpression in nested.NestedExpressions)
                        {
                            if (ContainsRuntimeLogic(nestedExpression))
                                return true;
                        }
                    }
                    return false;
            }
        }

    }
}
