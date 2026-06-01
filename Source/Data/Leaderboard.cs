using System;
using System.Collections.Generic;
using System.Linq;

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
        public Trigger Start { get; set; }

        /// <summary>
        /// Gets or sets the serialized cancel condition for the leaderboard.
        /// </summary>
        public Trigger Cancel { get; set; }

        /// <summary>
        /// Gets or sets the serialized submit condition for the leaderboard.
        /// </summary>
        public Trigger Submit { get; set; }

        /// <summary>
        /// Gets or sets the serialized value formula for the leaderboard.
        /// </summary>
        public Value Value { get; set; }

        /// <summary>
        /// Gets or sets the format to use when displaying the value.
        /// </summary>
        public ValueFormat Format { get; set; }

        /// <summary>
        /// Gets or sets whether lower scores are better than higher scores.
        /// </summary>
        public bool LowerIsBetter { get; set; }

        public static Leaderboard FindMergeLeaderboard(IEnumerable<Leaderboard> leaderboards, Leaderboard leaderboard)
        {
            Leaderboard match;

            // first pass - look for ID match
            if (leaderboard.Id != 0)
            {
                match = leaderboards.FirstOrDefault(l => l.Id == leaderboard.Id);
                if (match != null) // exact ID match, don't check anything else
                    return match;
            }

            // second pass - look for title match
            if (!String.IsNullOrEmpty(leaderboard.Title))
            {
                match = leaderboards.FirstOrDefault(l => String.Compare(l.Title, leaderboard.Title, StringComparison.InvariantCultureIgnoreCase) == 0);
                if (match != null)
                    return match;
            }

            // third pass - look for description match
            if (!String.IsNullOrEmpty(leaderboard.Description))
            {
                match = leaderboards.FirstOrDefault(l => String.Compare(l.Description, leaderboard.Description, StringComparison.InvariantCultureIgnoreCase) == 0);
                if (match != null)
                    return match;
            }

            // TODO: attempt to match requirements

            return null;
        }
    }
}
