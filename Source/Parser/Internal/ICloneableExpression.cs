namespace RATools.Parser.Internal
{
    internal interface ICloneableExpression
    {
        /// <summary>
        /// Create a clone of an expression
        /// </summary>
        ExpressionBase Clone();
    }
}
