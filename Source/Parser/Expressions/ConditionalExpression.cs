using Jamiras.Components;
using RATools.Parser.Expressions.Trigger;
using RATools.Parser.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Expressions
{
    internal class ConditionalExpression : ExpressionBase, INestedExpressions
    {
        public ConditionalExpression(ExpressionBase left, ConditionalOperation operation, ExpressionBase right)
            : base(ExpressionType.Conditional)
        {
            Operation = operation;

            if (operation == ConditionalOperation.Not)
            {
                Debug.Assert(left == null);
                Debug.Assert(right != null);

                _conditions = new List<ExpressionBase>() { right };
            }
            else
            {
                Debug.Assert(left != null);
                Debug.Assert(right != null);

                int length = 0;

                var conditionalLeft = left as ConditionalExpression;
                bool mergeLeft = conditionalLeft != null && conditionalLeft.Operation == operation;

                if (mergeLeft)
                    length += conditionalLeft._conditions.Count;
                else
                    length += 1;

                var conditionalRight = right as ConditionalExpression;
                bool mergeRight = conditionalRight != null && conditionalRight.Operation == operation;
                if (mergeRight)
                    length += conditionalRight._conditions.Count;
                else
                    length += 1;

                _conditions = new List<ExpressionBase>(length);

                if (mergeLeft)
                    _conditions.AddRange(conditionalLeft.Conditions);
                else
                    _conditions.Add(left);

                if (mergeRight)
                    _conditions.AddRange(conditionalRight.Conditions);
                else
                    _conditions.Add(right);
            }

            if (left != null)
                Location = new TextRange(left.Location.Start, right.Location.End);
            else
                Location = right.Location;
        }

        public ConditionalExpression(ConditionalOperation operation, List<ExpressionBase> conditions)
            : base(ExpressionType.Conditional)
        {
            Operation = operation;
            _conditions = conditions;

            Location = new TextRange(conditions[0].Location.Start, conditions[conditions.Count - 1].Location.End);
        }

        /// <summary>
        /// Gets the conditional operation.
        /// </summary>
        public ConditionalOperation Operation { get; private set; }

        public IEnumerable<ExpressionBase> Conditions
        {
            get { return _conditions; }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly List<ExpressionBase> _conditions;

        private bool _fullyExpanded = false;

        public void AddCondition(ExpressionBase condition)
        {
            _conditions.Add(condition);
            _fullyExpanded = false;

            if (!condition.Location.IsEmpty)
            {
                if (condition.Location.Start < Location.Start)
                    Location = new TextRange(condition.Location.Start, Location.End);
                if (condition.Location.End > Location.End)
                    Location = new TextRange(Location.Start, condition.Location.End);
            }
        }

        /// <summary>
        /// Appends the textual representation of this expression to <paramref name="builder" />.
        /// </summary>
        internal override void AppendString(StringBuilder builder)
        {
            if (Operation == ConditionalOperation.Not)
            {
                builder.Append('!');
                var right = _conditions[0];
                if (right.Type == ExpressionType.Conditional || right.Type == ExpressionType.Comparison)
                {
                    builder.Append('(');
                    right.AppendString(builder);
                    builder.Append(')');
                }
                else
                {
                    right.AppendString(builder);
                }
                return;
            }

            var operatorString = ' ' + GetOperatorString(Operation) + ' ';

            for (int i = 0; i < _conditions.Count; ++i)
            {
                if (i > 0)
                    builder.Append(operatorString);

                var condition = _conditions[i];
                if (condition.IsLogicalUnit)
                    builder.Append('(');
                condition.AppendString(builder);
                if (condition.IsLogicalUnit)
                    builder.Append(')');
            }
        }

        internal static string GetOperatorString(ConditionalOperation operation)
        {
            switch (operation)
            {
                case ConditionalOperation.Not: return "!";
                case ConditionalOperation.And: return "&&";
                case ConditionalOperation.Or: return "||";
                default: return null;
            }
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
            if (_fullyExpanded)
            {
                result = this;
                return true;
            }

            bool hasTrue = false;
            bool hasFalse = false;
            bool isChanged = false;

            var updatedConditions = new List<ExpressionBase>(_conditions.Count);
            for (int i = 0; i < _conditions.Count; ++i)
            {
                if (!_conditions[i].ReplaceVariables(scope, out result))
                    return false;

                // can eliminate true/false now, but not things that evaluate to true/false.
                // (like always_true or always_false) as those may be used to generate explicit alt groups.
                var booleanExpression = result as BooleanConstantExpression;
                if (booleanExpression != null)
                {
                    if (booleanExpression.Value)
                    {
                        hasTrue = true;

                        if (Operation == ConditionalOperation.And)
                        {
                            isChanged = true;
                            continue;
                        }
                    }
                    else
                    {
                        hasFalse = true;

                        if (Operation == ConditionalOperation.Or)
                        {
                            isChanged = true;
                            continue;
                        }
                    }
                }

                isChanged |= !ReferenceEquals(result, _conditions[i]);
                updatedConditions.Add(result);
            }

            bool? logicalResult = null;
            switch (Operation)
            {
                case ConditionalOperation.Not:
                    if (hasTrue)
                    {
                        logicalResult = false;
                    }
                    else if (hasFalse)
                    {
                        logicalResult = true;
                    }
                    else
                    {
                        result = InvertExpression(updatedConditions[0]);
                        if (result.Type == ExpressionType.Error)
                            return false;

                        CopyLocation(result);

                        // InvertExpression may distribute Nots to subnodes, recurse
                        return result.ReplaceVariables(scope, out result);
                    }
                    break;

                case ConditionalOperation.Or:
                    if (hasTrue)
                    {
                        // anything or true is true
                        logicalResult = true;
                    }
                    else if (hasFalse && updatedConditions.Count == 0)
                    {
                        // all conditions were false, entire condition is false
                        logicalResult = false;
                    }
                    break;

                case ConditionalOperation.And:
                    if (hasFalse)
                    {
                        // anything and false is false
                        logicalResult = false;
                    }
                    if (hasTrue && updatedConditions.Count == 0)
                    {
                        // all conditions were true, entire condition is true
                        logicalResult = true;
                    }
                    break;
            }

            if (logicalResult == true)
            {
                result = new BooleanConstantExpression(true);
            }
            else if (logicalResult == false)
            {
                result = new BooleanConstantExpression(false);
            }
            else
            {
                // merge with nested logic when possible
                for (int i = updatedConditions.Count - 1; i >= 0; i--)
                {
                    var conditionalExpression = updatedConditions[i] as ConditionalExpression;
                    if (conditionalExpression != null && conditionalExpression.Operation == Operation)
                    {
                        updatedConditions.RemoveAt(i);
                        updatedConditions.InsertRange(i, conditionalExpression._conditions);
                        isChanged = true;
                    }

                    var requirementExpression = updatedConditions[i] as RequirementClauseExpression;
                    if (requirementExpression != null && requirementExpression.Operation == Operation)
                    {
                        updatedConditions.RemoveAt(i);
                        updatedConditions.InsertRange(i, requirementExpression.Conditions);
                        isChanged = true;
                    }
                }

                if (!isChanged)
                {
                    _fullyExpanded = true;
                    result = this;
                    return true;
                }

                if (updatedConditions.All(c => c is RequirementExpressionBase))
                {
                    var clause = new RequirementClauseExpression { Operation = Operation };
                    foreach (var condition in updatedConditions)
                        clause.AddCondition((RequirementExpressionBase)condition);
                    CopyLocation(clause);
                    result = clause;
                    return true;
                }

                if (updatedConditions.Count >= 2)
                {
                    var logicalCombiningExpression = updatedConditions[0] as ILogicalCombineExpression;
                    if (logicalCombiningExpression != null)
                    {
                        for (int i = 1; i < updatedConditions.Count; i++)
                        {
                            var newExpression = logicalCombiningExpression.Combine(updatedConditions[i], Operation);
                            if (newExpression != null && newExpression.Type == ExpressionType.Error)
                            {
                                result = newExpression;
                                return false;
                            }

                            logicalCombiningExpression = newExpression as ILogicalCombineExpression;
                            if (logicalCombiningExpression == null)
                                break;
                        }

                        if (logicalCombiningExpression != null)
                        {
                            result = (ExpressionBase)logicalCombiningExpression;
                            CopyLocation(result);
                            return true;
                        }
                    }
                }

                var newConditionalExpression = new ConditionalExpression(Operation, updatedConditions);
                newConditionalExpression._fullyExpanded = true;
                result = newConditionalExpression;
            }

            CopyLocation(result);
            return true;
        }

        internal static ExpressionBase InvertExpression(ExpressionBase expression)
        {
            // logical inversion
            var condition = expression as ConditionalExpression;
            if (condition != null)
            {
                var newConditions = new List<ExpressionBase>(condition._conditions.Count);
                foreach (var oldCondition in condition._conditions)
                    newConditions.Add(InvertExpression(oldCondition));

                switch (condition.Operation)
                {
                    case ConditionalOperation.Not:
                        // !(!A) => A
                        return newConditions[0];

                    case ConditionalOperation.And:
                        // !(A && B) => !A || !B
                        return new ConditionalExpression(ConditionalOperation.Or, newConditions);

                    case ConditionalOperation.Or:
                        // !(A || B) => !A && !B
                        return new ConditionalExpression(ConditionalOperation.And, newConditions);

                    default:
                        throw new NotImplementedException("Unsupported condition operation");
                }
            }

            // comparative inversion
            var comparison = expression as ComparisonExpression;
            if (comparison != null)
            {
                // !(A == B) => A != B, !(A < B) => A >= B, ...
                return new ComparisonExpression(
                    comparison.Left,
                    ComparisonExpression.GetOppositeComparisonOperation(comparison.Operation),
                    comparison.Right);
            }

            // requirement clause
            var clause = expression as RequirementExpressionBase;
            if (clause != null)
            {
                var inverted = clause.InvertLogic();
                if (inverted != null)
                    return inverted;
            }

            // boolean constant
            var boolean = expression as BooleanConstantExpression;
            if (boolean != null)
                return new BooleanConstantExpression(!boolean.Value);

            // special handling for built-in functions
            if (expression is AlwaysTrueExpression)
                return new AlwaysFalseExpression();
            if (expression is AlwaysFalseExpression)
                return new AlwaysTrueExpression();

            // unsupported inversion
            return new ErrorExpression("! operator cannot be applied to " + expression.Type, expression);
        }

        /// <summary>
        /// Determines whether the expression evaluates to true for the provided <paramref name="scope" />
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="error">[out] The error that prevented evaluation (or null if successful).</param>
        /// <returns>
        /// The result of evaluating the expression
        /// </returns>
        public override bool? IsTrue(InterpreterScope scope, out ErrorExpression error)
        {
            bool? isTrue;
            bool? result;

            switch (Operation)
            {
                case ConditionalOperation.And:
                    result = true;
                    foreach (var condition in _conditions)
                    {
                        isTrue = condition.IsTrue(scope, out error);
                        if (error != null)
                            return isTrue;

                        if (isTrue == false)
                            return false;

                        if (isTrue == null)
                            result = null;
                    }

                    error = null;
                    return result;

                case ConditionalOperation.Or:
                    result = false;
                    foreach (var condition in _conditions)
                    {
                        isTrue = condition.IsTrue(scope, out error);
                        if (error != null)
                            return isTrue;

                        if (isTrue == true)
                            return true;

                        if (isTrue == null)
                            result = null;
                    }

                    error = null;
                    return result;

                case ConditionalOperation.Not:
                    isTrue = _conditions[0].IsTrue(scope, out error);
                    if (isTrue == null)
                        return null;
                    return !isTrue;

                default:
                    error = null;
                    return null;
            }
        }

        /// <summary>
        /// Determines whether the specified <see cref="ConditionalExpression" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="ConditionalExpression" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="ConditionalExpression" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        protected override bool Equals(ExpressionBase obj)
        {
            var that = obj as ConditionalExpression;
            return that != null && Operation == that.Operation && _conditions == that._conditions;
        }

        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get { return _conditions; }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            foreach (var condition in _conditions)
            {
                var nested = condition as INestedExpressions;
                if (nested != null)
                    nested.GetDependencies(dependencies);
            }
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
        }

        internal class OrNextWrapperFunction : FunctionDefinitionExpression
        {
            public OrNextWrapperFunction()
                : base("__ornext")
            {
                Parameters.Add(new VariableDefinitionExpression("comparison"));
            }

            public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
            {
                return Evaluate(scope, out result);
            }

            public override bool Evaluate(InterpreterScope scope, out ExpressionBase result)
            {
                var comparison = GetParameter(scope, "comparison", out result);
                if (comparison == null)
                    return false;

                var wrapper = new RequirementClauseExpression.OrNextRequirementClauseExpression();
                wrapper.AddCondition(comparison);
                result = wrapper;
                return true;
            }
        }
    }

    /// <summary>
    /// Specifies how the two sides of the <see cref="ConditionalExpression"/> should be compared.
    /// </summary>
    public enum ConditionalOperation
    {
        /// <summary>
        /// Unspecified.
        /// </summary>
        None = 0,

        /// <summary>
        /// Both sides must be true.
        /// </summary>
        And,

        /// <summary>
        /// Either side can be true.
        /// </summary>
        Or,

        /// <summary>
        /// Right is not true (Left is ignored)
        /// </summary>
        Not,
    }
}
