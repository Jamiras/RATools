using Jamiras.Components;
using RATools.Parser.Functions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace RATools.Parser.Internal
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

                _conditions = new ExpressionBase[1] { right };
            }
            else
            {
                Debug.Assert(left != null);
                Debug.Assert(right != null);

                int length = 0;

                var conditionalLeft = left as ConditionalExpression;
                if (conditionalLeft != null && conditionalLeft.Operation == operation)
                    length += conditionalLeft._conditions.Length;
                else
                    length += 1;

                var conditionalRight = right as ConditionalExpression;
                if (conditionalRight != null && conditionalRight.Operation == operation)
                    length += conditionalRight._conditions.Length;
                else
                    length += 1;

                _conditions = new ExpressionBase[length];
                int index = 0;

                if (conditionalLeft != null && conditionalLeft.Operation == operation)
                {
                    index = conditionalLeft._conditions.Length;
                    Array.Copy(conditionalLeft._conditions, _conditions, index);
                }
                else
                {
                    _conditions[0] = left;
                    index = 1;
                }

                if (conditionalRight != null && conditionalRight.Operation == operation)
                    Array.Copy(conditionalRight._conditions, 0, _conditions, index, conditionalRight._conditions.Length);
                else
                    _conditions[index] = right;
            }

            if (left != null)
                Location = new TextRange(left.Location.Start, right.Location.End);
            else
                Location = right.Location;
        }

        public ConditionalExpression(ConditionalOperation operation, ExpressionBase[] conditions)
            : base(ExpressionType.Conditional)
        {
            Operation = operation;
            _conditions = conditions;

            Location = new TextRange(conditions[0].Location.Start, conditions[conditions.Length - 1].Location.End);
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
        private readonly ExpressionBase[] _conditions;

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

            for (int i = 0; i < _conditions.Length; ++i)
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
        ///   <c>true</c> if substitution was successful, <c>false</c> if something went wrong, in which case <paramref name="result" /> will likely be a <see cref="ParseErrorExpression" />.
        /// </returns>
        public override bool ReplaceVariables(InterpreterScope scope, out ExpressionBase result)
        {
            bool hasTrue = false;
            bool hasFalse = false;
            bool isChanged = false;

            var updatedConditions = new List<ExpressionBase>(_conditions.Length);
            for (int i = 0; i < _conditions.Length; ++i)
            {
                if (!_conditions[i].ReplaceVariables(scope, out result))
                    return false;

                isChanged |= !ReferenceEquals(result, _conditions[i]);

                ParseErrorExpression parseError;
                bool? logicalValue = result.IsTrue(scope, out parseError);
                if (parseError != null)
                {
                    result = parseError;
                    return false;
                }

                if (logicalValue == true)
                {
                    hasTrue = true;

                    if (Operation == ConditionalOperation.And)
                    {
                        isChanged = true;
                        continue;
                    }
                }
                else if (logicalValue == false)
                {
                    hasFalse = true;

                    if (Operation == ConditionalOperation.Or)
                    {
                        isChanged = true;
                        continue;
                    }
                }

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
                        if (result.Type == ExpressionType.ParseError)
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
            else if (!isChanged)
            {
                result = this;
                return true;
            }
            else
            {
                result = new ConditionalExpression(Operation, updatedConditions.ToArray());
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
                var newConditions = new ExpressionBase[condition._conditions.Length];
                for (int i =0; i < newConditions.Length; ++i)
                    newConditions[i] = InvertExpression(condition._conditions[i]);

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

            // boolean constant
            var boolean = expression as BooleanConstantExpression;
            if (boolean != null)
                return new BooleanConstantExpression(!boolean.Value);

            // special handling for built-in functions
            var function = expression as FunctionCallExpression;
            if (function != null)
            {
                if (function.FunctionName.Name == "always_true")
                    return new FunctionCallExpression("always_false", function.Parameters);

                if (function.FunctionName.Name == "always_false")
                    return new FunctionCallExpression("always_true", function.Parameters);
            }

            // unsupported inversion
            return new ParseErrorExpression("! operator cannot be applied to " + expression.Type, expression);
        }

        /// <summary>
        /// Rebalances this expression based on the precendence of operators.
        /// </summary>
        /// <returns>
        /// Rebalanced expression
        /// </returns>
        internal override ExpressionBase Rebalance()
        {
            if (Operation == ConditionalOperation.And && _conditions.Length == 2)
            {
                // the tree will be built weighted to the right. AND has higher priority than OR, so if an
                // ungrouped AND is followed by an OR, shift them around so the AND will be evaluated first
                //
                //   A && B || C  ~>  (A && B) || C
                //
                //     &&                      ||
                //   A      ||           &&       C
                //        B    C       A    B
                var conditionalRight = _conditions[1] as ConditionalExpression;
                if (conditionalRight != null && conditionalRight.Operation == ConditionalOperation.Or)
                {
                    // enforce order of operations
                    var conditions = conditionalRight.Conditions.ToArray();
                    _conditions[_conditions.Length - 1] = conditions[0];
                    conditions[0] = this;
                    return new ConditionalExpression(ConditionalOperation.Or, conditions);
                }
            }

            return base.Rebalance();
        }

        /// <summary>
        /// Determines whether the expression evaluates to true for the provided <paramref name="scope" />
        /// </summary>
        /// <param name="scope">The scope object containing variable values.</param>
        /// <param name="error">[out] The error that prevented evaluation (or null if successful).</param>
        /// <returns>
        /// The result of evaluating the expression
        /// </returns>
        public override bool? IsTrue(InterpreterScope scope, out ParseErrorExpression error)
        {
            bool? isTrue;

            switch (Operation)
            {
                case ConditionalOperation.And:
                    foreach (var condition in _conditions)
                    {
                        isTrue = condition.IsTrue(scope, out error);
                        if (error != null)
                            return isTrue;

                        if (isTrue == false)
                            return false;

                        if (isTrue == null)
                            return null;
                    }

                    error = null;
                    return true;

                case ConditionalOperation.Or:
                    foreach (var condition in _conditions)
                    {
                        isTrue = condition.IsTrue(scope, out error);
                        if (error != null)
                            return isTrue;

                        if (isTrue == true)
                            return true;

                        if (isTrue == null)
                            return null;
                    }

                    error = null;
                    return false;

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
