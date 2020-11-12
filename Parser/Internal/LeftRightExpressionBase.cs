using System.Collections.Generic;
using System.Diagnostics;

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
            {
                Line = left.Line;
                Column = left.Column;
            }
            else
            {
                Line = right.Line;
                Column = right.Column;
            }

            EndLine = right.EndLine;
            EndColumn = right.EndColumn;
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
            EndLine = Right.EndLine;
            EndColumn = Right.EndColumn;

            newRoot.Left = this.Rebalance();
            newRoot.Line = newRoot.Left.Line;
            newRoot.Column = newRoot.Left.Column;

            return newRoot;
        }

        bool INestedExpressions.GetExpressionsForLine(List<ExpressionBase> expressions, int line)
        {
            if (Left == null)
                return ExpressionGroup.GetExpressionsForLine(expressions, new[] { Right }, line);

            return ExpressionGroup.GetExpressionsForLine(expressions, new[] { Left, Right }, line);
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
