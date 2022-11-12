using RATools.Data;
using RATools.Parser.Internal;
using System.Collections.Generic;

namespace RATools.Parser.Expressions.Trigger
{
    internal abstract class RequirementExpressionBase : ExpressionBase,
        ITriggerExpression, IComparisonNormalizeExpression, 
        ILogicalCombineExpression, IExecutableExpression
    {
        protected RequirementExpressionBase()
            : base(ExpressionType.Requirement)
        {
        }

        ErrorExpression ITriggerExpression.BuildTrigger(TriggerBuilderContext context)
        {
            var optimized = Optimize(context);
            return optimized.BuildTrigger(context);
        }

        /// <summary>
        /// Appends <see cref="Requirement"/>s to the <paramref name="context"/> represented by this expression.
        /// </summary>
        /// <param name="context">The context to append <see cref="Requirement"/>s to.</param>
        /// <returns><c>null</c> on success, or an <see cref="ErrorExpression"/> on failure.</returns>
        public abstract ErrorExpression BuildTrigger(TriggerBuilderContext context);

        /// <summary>
        /// Appends <see cref="Requirement"/>s to the <paramref name="context"/> represented by this expression.
        /// </summary>
        /// <param name="context">The context to append <see cref="Requirement"/>s to.</param>
        /// <returns><c>null</c> on success, or an <see cref="ErrorExpression"/> on failure.</returns>
        /// <remarks>Ensures the resulting trigger can be used as a subclause.</remarks>
        public ErrorExpression BuildSubclauseTrigger(TriggerBuilderContext context)
        {
            return BuildSubclauseTrigger(context, ConditionalOperation.None, RequirementType.None);
        }

        /// <summary>
        /// Appends <see cref="Requirement"/>s to the <paramref name="context"/> represented by this expression.
        /// </summary>
        /// <param name="context">The context to append <see cref="Requirement"/>s to.</param>
        /// <param name="splitCondition">Indicates the type of conditional joins that can be replaced with <paramref name="splitBehavior"/>.</param>
        /// <param name="splitBehavior">The behavior to apply to conditions joined by <paramref name="splitCondition"/>.</param>
        /// <returns><c>null</c> on success, or an <see cref="ErrorExpression"/> on failure.</returns>
        /// <remarks>Ensures the resulting trigger can be used as a subclause.</remarks>
        public virtual ErrorExpression BuildSubclauseTrigger(TriggerBuilderContext context, ConditionalOperation splitCondition, RequirementType splitBehavior)
        {
            return BuildTrigger(context);
        }

        /// <summary>
        /// Create a clone of the expression
        /// </summary>
        public RequirementExpressionBase Clone()
        {
            return (RequirementExpressionBase)((ICloneableExpression)this).Clone();
        }

        protected static bool CompareRequirements(List<RequirementExpressionBase> left, List<RequirementExpressionBase> right)
        {
            if (left == null)
                return (right == null);
            if (right == null)
                return false;

            if (left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        ExpressionBase IComparisonNormalizeExpression.NormalizeComparison(ExpressionBase right, ComparisonOperation operation)
        {
            return new ErrorExpression("Cannot chain comparisons", this);
        }

        /// <summary>
        /// Combines the current expression with the <paramref name="right"/> expression using the <paramref name="operation"/> operator.
        /// </summary>
        /// <param name="right">The expression to combine with the current expression.</param>
        /// <param name="operation">How to combine the expressions.</param>
        /// <returns>
        /// An expression representing the combined values on success, or <c>null</c> if the expressions could not be combined.
        /// </returns>
        public virtual ExpressionBase Combine(ExpressionBase right, ConditionalOperation operation)
        {
            if (operation == ConditionalOperation.Not)
                return InvertLogic();

            var requirement = right as RequirementExpressionBase;
            if (requirement == null)
                return null;

            var clause = new RequirementClauseExpression();
            clause.Operation = operation;
            clause.AddCondition(this);
            clause.AddCondition(requirement);
            return clause;
        }

        /// <summary>
        /// Returns a <see cref="RequirementExpressionBase"/> that represents the optimal logic for the provided context.
        /// </summary>
        public virtual RequirementExpressionBase Optimize(TriggerBuilderContext context)
        {
            return this;
        }

        /// <summary>
        /// Returns a new <see cref="RequirementExpressionBase"/> that represents the logical inversion of the source object.
        /// </summary>
        /// <returns>New requirement, or <c>null</c> if the requirement cannot be inverted.</returns>
        public virtual RequirementExpressionBase InvertLogic()
        {
            return null;
        }

        /// <summary>
        /// Returns an expression where any 'never(A)'s have been converted to '!A's
        /// </summary>
        /// <returns>New requirement, or <c>null</c> if the requirement cannot be inverted.</returns>
        /// <remarks>May return the original expression if nothing needed to be converted</remarks>
        public virtual RequirementExpressionBase InvertResetsAndPauses()
        {
            return this;
        }

        /// <summary>
        /// Returns an expression that represents the logical intersection of this expression and
        /// <paramref name="that"> expression, when they are <paramref name="condition"/>ed together.
        /// </summary>
        /// <param name="that">The second expression</param>
        /// <param name="condition">How to combine the expressions</param>
        /// <returns>
        /// <c>this</c>, <paramref name="that"/>, or a new expression if they intersect,
        /// <c>null</c> if not.
        /// </returns>
        public virtual RequirementExpressionBase LogicalIntersect(RequirementExpressionBase that, ConditionalOperation condition)
        {
            if (this == that)
                return this;

            return null;
        }

        ErrorExpression IExecutableExpression.Execute(InterpreterScope scope)
        {
            var asString = ToString();
            var index = asString.IndexOf('(');
            var functionName = (index == -1) ? "expression" : asString.Substring(0, index);
            return new ErrorExpression(functionName + " has no meaning outside of a trigger");
        }
    }
}
