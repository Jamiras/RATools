using System.Diagnostics;

namespace RATools.Data
{
    /// <summary>
    /// Defines a leaderboard.
    /// </summary>
    [DebuggerDisplay("{Title}")]
    public class Leaderboard
    {
        /// <summary>
        /// Gets or sets the title of the leaderboard.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the description of the leaderboard.
        /// </summary>
        public string Description { get; set; }

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
                case "TIMESECS":
                    return ValueFormat.TimeSecs;

                case "FRAMES":
                case "TIME":
                    return ValueFormat.TimeFrames;

                case "POINTS":
                case "SCORE":
                    return ValueFormat.Score;

                case "MILLISECS":
                    return ValueFormat.TimeMillisecs;

                case "MINUTES":
                    return ValueFormat.TimeMinutes;

                case "SECS_AS_MINS":
                    return ValueFormat.TimeSecsAsMins;

                case "OTHER":
                    return ValueFormat.Other;

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

                default:
                    return "UNKNOWN";
            }
        }

        internal int SourceLine { get; set; }
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
    }

}
