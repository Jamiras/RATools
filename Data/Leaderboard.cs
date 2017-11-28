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
    }
}
