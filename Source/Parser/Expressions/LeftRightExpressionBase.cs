using Jamiras.Components;
using System.Collections.Generic;

namespace RATools.Parser.Expressions
{
    public abstract class LeftRightExpressionBase : ExpressionBase, INestedExpressions
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
