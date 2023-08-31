namespace RATools.Data
{
    /// <summary>
    /// Defines a leaderboard.
    /// </summary>
    public class Leaderboard : AssetBase
    {
        /// <summary>
        /// Gets or sets the serialized start condition for the leaderboard.
        /// </summary>
        public string Start { get; set; }

        /// <summary>
        /// Gets or sets the serialized cancel condition for the leaderboard.
        /// </summary>
        public string Cancel { get; set; }

        /// <summary>
        /// Gets or sets the serialized submit condition for the leaderboard.
        /// </summary>
        public string Submit { get; set; }

        /// <summary>
        /// Gets or sets the serialized value formula for the leaderboard.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets the format to use when displaying the value.
        /// </summary>
        public ValueFormat Format { get; set; }

        /// <summary>
        /// Gets or sets whether lower scores are better than higher scores.
        /// </summary>
        public bool LowerIsBetter { get; set; }

        /// <summary>
        /// Converts a format string to a <see cref="ValueFormat"/>.
        /// </summary>
        /// <param name="format">The format as a string.</param>
        /// <returns>The format as a <see cref="ValueFormat"/>, None if not valid.</returns>
        public static ValueFormat ParseFormat(string format)
        {
            var formatStr = format.ToUpper();
            switch (formatStr)
            {
                case "VALUE":
                    return ValueFormat.Value;

                case "SECS":
                case "TIMESECS": // valid in runtime - maps to SECS
                    return ValueFormat.TimeSecs;

                case "FRAMES":
                case "TIME": // valid in runtime - maps to FRAMES
                    return ValueFormat.TimeFrames;

                case "POINTS": // valid in runtime - maps to SCORE
                case "SCORE":
                    return ValueFormat.Score;

                case "CENTISECS": // not valid in runtime. converted to MILLISECS when serialized
                case "MILLISECS":
                    return ValueFormat.TimeMillisecs;

                case "MINUTES":
                    return ValueFormat.TimeMinutes;

                case "SECS_AS_MINS":
                    return ValueFormat.TimeSecsAsMins;

                case "OTHER": // valid in runtime - maps to SCORE
                    return ValueFormat.Other;

                case "FLOAT1":
                    return ValueFormat.Float1;

                case "FLOAT2":
                    return ValueFormat.Float2;

                case "FLOAT3":
                    return ValueFormat.Float3;

                case "FLOAT4":
                    return ValueFormat.Float4;

                case "FLOAT5":
                    return ValueFormat.Float5;

                case "FLOAT6":
                    return ValueFormat.Float6;

                default:
                    return ValueFormat.None;
            }
        }

        public static string GetFormatString(ValueFormat format)
        {
            switch (format)
            {
                case ValueFormat.Value:
                    return "VALUE";

                case ValueFormat.Score:
                    return "SCORE";

                case ValueFormat.TimeSecs:
                    return "SECS";

                case ValueFormat.TimeMillisecs:
                    return "MILLISECS";

                case ValueFormat.TimeFrames:
                    return "FRAMES";

                case ValueFormat.TimeMinutes:
                    return "MINUTES";

                case ValueFormat.TimeSecsAsMins:
                    return "SECS_AS_MINS";

                case ValueFormat.Other:
                    return "OTHER";

                case ValueFormat.Float1:
                    return "FLOAT1";

                case ValueFormat.Float2:
                    return "FLOAT2";

                case ValueFormat.Float3:
                    return "FLOAT3";

                case ValueFormat.Float4:
                    return "FLOAT4";

                case ValueFormat.Float5:
                    return "FLOAT5";

                case ValueFormat.Float6:
                    return "FLOAT6";

                default:
                    return "UNKNOWN";
            }
        }
    }

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
        /// Generic value followed by "Points" - %06d Points
        /// </summary>
        Score,

        /// <summary>
        /// Convert the value (in seconds) to minutes/seconds - %02d:%02d
        /// </summary>
        TimeSecs,

        /// <summary>
        /// Convert the value (in hundredths of a second) to minutes/seconds/hundredths - %02d:%02d.%02d
        /// </summary>
        TimeMillisecs,

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
        /// Virtual format for ASCII char lookup table
        /// </summary>
        ASCIIChar,

        /// <summary>
        /// Virtual format for Unciode char lookup table
        /// </summary>
        UnicodeChar,
    }

}
