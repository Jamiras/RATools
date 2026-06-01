namespace RATools.Data
{
    /// <summary>
    /// Supported formats to apply to values.
    /// </summary>
    /// <remarks>
    /// Used by leaderboards and rich presence
    /// </remarks>
    public enum ValueFormat
    {
        /// <summary>
        /// Unspecified
        /// </summary>
        None,

        /// <summary>
        /// Generic value - %01d
        /// </summary>
        Value,

        /// <summary>
        /// Generic unsigned value - %01u
        /// </summary>
        Unsigned,

        /// <summary>
        /// Generic value padded with 0s to 6 digits - %06d Points
        /// </summary>
        Score,

        /// <summary>
        /// Convert the value (in seconds) to minutes/seconds - %02d:%02d
        /// </summary>
        TimeSecs,

        /// <summary>
        /// Convert the value (in hundredths of a second) to minutes/seconds/hundredths - %02d:%02d.%02d
        /// </summary>
        TimeCentisecs,

        /// <summary>
        /// Convert the value (in sixtieths of a second) to minutes/seconds/hundredths - %02d:%02d.%02d
        /// </summary>
        TimeFrames,     //	Value is a number describing the amount of frames.

        /// <summary>
        /// Zero-padded value - %06d
        /// </summary>
        Other,

        /// <summary>
        /// Convert the value (in minutes) to hours/minutes - %dh%02d
        /// </summary>
        TimeMinutes,

        /// <summary>
        /// Convert the value (in seconds) to hours/minutes - %dh%02d
        /// </summary>
        TimeSecsAsMins,

        /// <summary>
        /// A floating point value with one digit after the decimal
        /// </summary>
        Float1,

        /// <summary>
        /// A floating point value with two digits after the decimal
        /// </summary>
        Float2,

        /// <summary>
        /// A floating point value with three digits after the decimal
        /// </summary>
        Float3,

        /// <summary>
        /// A floating point value with four digits after the decimal
        /// </summary>
        Float4,

        /// <summary>
        /// A floating point value with five digits after the decimal
        /// </summary>
        Float5,

        /// <summary>
        /// A floating point value with six digits after the decimal
        /// </summary>
        Float6,

        /// <summary>
        /// A fixed-point value with one digit after the decimal
        /// </summary>
        Fixed1,

        /// <summary>
        /// A fixed-point value with two digits after the decimal
        /// </summary>
        Fixed2,

        /// <summary>
        /// A fixed-point value with three digits after the decimal
        /// </summary>
        Fixed3,

        /// <summary>
        /// A number padded with a trailing zero
        /// </summary>
        Tens,

        /// <summary>
        /// A number padded with two trailing zeroes
        /// </summary>
        Hundreds,

        /// <summary>
        /// A number padded with three trailing zeroes
        /// </summary>
        Thousands,

        /// <summary>
        /// Virtual format for ASCII char lookup table
        /// </summary>
        ASCIIChar,

        /// <summary>
        /// Virtual format for Unciode char lookup table
        /// </summary>
        UnicodeChar,
    }
}
