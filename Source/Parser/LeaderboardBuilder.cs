using RATools.Data;
using System;
using System.Diagnostics;

namespace RATools.Parser
{
    [DebuggerDisplay("{Title}")]
    public class LeaderboardBuilder
    {
        public LeaderboardBuilder()
        {
            Title = String.Empty;
            Description = String.Empty;
            Format = ValueFormat.Value;

            Start = new TriggerBuilder();
            Submit = new TriggerBuilder();
            Cancel = new TriggerBuilder();
            Value = new ValueBuilder();
        }

        public LeaderboardBuilder(Leaderboard source)
        {
            Title = source.Title;
            Description = source.Description;
            Format = source.Format;

            Start = new TriggerBuilder(Trigger.Deserialize(source.Start));
            Submit = new TriggerBuilder(Trigger.Deserialize(source.Submit));
            Cancel = new TriggerBuilder(Trigger.Deserialize(source.Cancel));
            Value = new ValueBuilder(Data.Value.Deserialize(source.Value));
        }

        /// <summary>
        /// Gets or sets the achievement title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the achievement description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the format of the value.
        /// </summary>
        public ValueFormat Format { get; set; }

        /// <summary>
        /// Gets the <see cref="TriggerBuilder"/> for the start trigger of the leaderboard.
        /// </summary>
        public TriggerBuilder Start { get; private set; }

        /// <summary>
        /// Gets the <see cref="TriggerBuilder"/> for the submit trigger of the leaderboard.
        /// </summary>
        public TriggerBuilder Submit { get; private set; }

        /// <summary>
        /// Gets the <see cref="TriggerBuilder"/> for the cancel trigger of the leaderboard.
        /// </summary>
        public TriggerBuilder Cancel { get; private set; }

        /// <summary>
        /// Gets the <see cref="ValueBuilder"/> for the value of the leaderboard.
        /// </summary>
        public ValueBuilder Value { get; private set; }


        /// <summary>
        /// Constructs a <see cref="Leaderboard"/> from the current state of the builder.
        /// </summary>
        public Leaderboard ToLeaderboard()
        {
            return new Leaderboard
            { 
                Title = Title,
                Description = Description,
                Format = Format,
            };
        }

        public static double GetMinimumVersion(Leaderboard leaderboard)
        {
            var trigger = Trigger.Deserialize(leaderboard.Start);
            var minimumVersion = trigger.MinimumVersion();

            trigger = Trigger.Deserialize(leaderboard.Cancel);
            minimumVersion = Math.Max(minimumVersion, trigger.MinimumVersion());

            trigger = Trigger.Deserialize(leaderboard.Submit);
            minimumVersion = Math.Max(minimumVersion, trigger.MinimumVersion());

            var value = Data.Value.Deserialize(leaderboard.Value);
            minimumVersion = Math.Max(minimumVersion, value.MinimumVersion());

            return minimumVersion;
        }
    }
}
