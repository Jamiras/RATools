namespace RATools.Data
{
    /// <summary>
    /// Special requirement behaviors
    /// </summary>
    public enum RequirementType
    {
        /// <summary>
        /// No special behavior.
        /// </summary>
        None = 0,

        /// <summary>
        /// Resets any HitCounts in the current requirement group if true.
        /// </summary>
        ResetIf,

        /// <summary>
        /// Pauses processing of the achievement if true.
        /// </summary>
        PauseIf,

        /// <summary>
        /// Adds the Left part of the requirement to the Left part of the next requirement.
        /// </summary>
        AddSource,

        /// <summary>
        /// Subtracts the Left part of the next requirement from the Left part of the requirement.
        /// </summary>
        SubSource,

        /// <summary>
        /// Adds the HitsCounts from this requirement to the next requirement.
        /// </summary>
        AddHits,

        /// <summary>
        /// Subtracts the HitsCounts from this requirement from the next requirement.
        /// </summary>
        SubHits,

        /// <summary>
        /// This requirement must also be true for the next requirement to be true.
        /// </summary>
        AndNext,

        /// <summary>
        /// This requirement or the following requirement must be true for the next requirement to be true.
        /// </summary>
        OrNext,

        /// <summary>
        /// Meta-flag indicating that this condition tracks progress as a raw value.
        /// </summary>
        Measured,

        /// <summary>
        /// Meta-flag indicating that this condition must be true to track progress.
        /// </summary>
        MeasuredIf,

        /// <summary>
        /// Adds the Left part of the requirement to the addresses in the next requirement.
        /// </summary>
        AddAddress,

        /// <summary>
        /// Resets any HitCounts on the next requirement group if true.
        /// </summary>
        ResetNextIf,

        /// <summary>
        /// While all non-Trigger conditions are true, a challenge indicator will be displayed.
        /// </summary>
        Trigger,

        /// <summary>
        /// Meta-flag indicating that this condition tracks progress as a percentage.
        /// </summary>
        MeasuredPercent,
    }

    internal static class RequirementTypeExtension
    {
        /// <summary>
        /// Gets whether or not the requirement affects the following requirement.
        /// </summary>
        public static bool IsCombining(this RequirementType type)
        {
            switch (type)
            {
                case RequirementType.AddHits:
                case RequirementType.SubHits:
                case RequirementType.AddSource:
                case RequirementType.SubSource:
                case RequirementType.AndNext:
                case RequirementType.OrNext:
                case RequirementType.AddAddress:
                case RequirementType.ResetNextIf:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets whether or not the requirement can be scaled.
        /// </summary>
        public static bool IsScalable(this RequirementType type)
        {
            switch (type)
            {
                case RequirementType.AddSource:
                case RequirementType.SubSource:
                case RequirementType.AddAddress:
                    return true;

                default:
                    return false;
            }
        }
    }
}
