using System.Collections.Generic;
using System.Diagnostics;
using Jamiras.Components;

namespace RATools.Parser.Internal
{
    internal abstract class LeftRightExpressionBase : ExpressionBase, INestedExpressions
    {
        public LeftRightExpressionBase(ExpressionBase left, ExpressionBase right, ExpressionType type)
            : base(type)
        {
            Left = left;
            Right = right;

            if (left != null)
                Location = new TextRange(left.Location.Start, right.Location.End);
            else
                Location = right.Location;
        }

        /// <summary>
        /// Gets the left side of the expression.
        /// </summary>
        public ExpressionBase Left { get; internal set; }

        /// <summary>
        /// Gets the right side of the expression.
        /// </summary>
        public ExpressionBase Right { get; internal set; }

        /// <summary>
        /// Rebalances the current node and the <paramref name="newRoot"/> so <paramref name="newRoot" /> becomes the parent node.
        /// </summary>
        /// <param name="newRoot">The new root. (must be <see cref="Right"/>)</param>
        /// <returns>The new root.</returns>
        protected ExpressionBase Rebalance(LeftRightExpressionBase newRoot)
        {
            // newRoot has to be Right, or we're joining trees instead of rebalancing.
            Debug.Assert(ReferenceEquals(newRoot, Right));

            //     A             C            A = this              D = Right.Left
            //   B   C    >    A   E          B = Left              E = Right.Right
            //      D E       B D             C = Right (newRoot)
            Right = newRoot.Left;
            Location = new TextRange(Location.Start, Right.Location.End);

            newRoot.Left = this.Rebalance();
            newRoot.Location = new TextRange(newRoot.Left.Location.Start, newRoot.Location.End);

            return newRoot;
        }

        protected static bool ConvertToFloat(ref ExpressionBase left, ref ExpressionBase right, out ExpressionBase result)
        {
            left = FloatConstantExpression.ConvertFrom(left);
            if (left.Type != ExpressionType.FloatConstant)
            {
                result = left;
                return false;
            }

            right = FloatConstantExpression.ConvertFrom(right);
            if (right.Type != ExpressionType.FloatConstant)
            {
                result = right;
                return false;
            }

            result = null;
            return true;
        }


        IEnumerable<ExpressionBase> INestedExpressions.NestedExpressions
        {
            get
            {
                if (Left != null)
                    yield return Left;

                yield return Right;
            }
        }

        void INestedExpressions.GetDependencies(HashSet<string> dependencies)
        {
            var nested = Left as INestedExpressions;
            if (nested != null)
                nested.GetDependencies(dependencies);

            nested = Right as INestedExpressions;
            if (nested != null)
                nested.GetDependencies(dependencies);
        }

        void INestedExpressions.GetModifications(HashSet<string> modifies)
        {
        }
    }
}
