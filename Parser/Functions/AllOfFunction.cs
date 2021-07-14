﻿using RATools.Parser.Internal;

namespace RATools.Parser.Functions
{
    internal class AllOfFunction : IterableJoiningFunction
    {
        public AllOfFunction()
            : base("all_of")
        {
        }

        protected override ExpressionBase Combine(ExpressionBase left, ExpressionBase right)
        {
            return new ConditionalExpression(left, ConditionalOperation.And, right);
        }

        protected override ExpressionBase GenerateEmptyResult()
        {
            return new FunctionCallExpression("always_true", new ExpressionBase[0]);
        }
    }
}
