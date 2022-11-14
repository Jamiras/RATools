namespace RATools.Parser.Internal
{
    internal interface INumericConstantExpression
    {
        /// <summary>
        /// Returns <c>true</c> if the constant is numerically zero
        /// </summary>
        bool IsZero { get; }

        /// <summary>
        /// Returns <c>true</c> if the constant is numerically negative
        /// </summary>
        bool IsNegative { get; }

        /// <summary>
        /// Returns <c>true</c> if the constant is numerically positive
        /// </summary>
        bool IsPositive { get; }
    }
}
