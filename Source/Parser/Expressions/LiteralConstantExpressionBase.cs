using RATools.Parser.Internal;

namespace RATools.Parser.Expressions
{
    public abstract class LiteralConstantExpressionBase : ExpressionBase, IValueExpression
    {
        protected LiteralConstantExpressionBase(ExpressionType type)
            : base(type)
        {
            MakeReadOnly();
        }

        /// <summary>
        /// Gets whether this is non-changing.
        /// </summary>
        public override bool IsConstant
        {
            get { return true; }
        }

        /// <summary>
        /// Evaluates an expression
        /// </summary>
        /// <returns><see cref="ErrorExpression"/> indicating the failure, or the result of evaluating the expression.</returns>
        public ExpressionBase Evaluate(InterpreterScope scope)
        {
            return this;
        }
    }
}
