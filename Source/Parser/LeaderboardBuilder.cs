using Jamiras.Components;
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

            Start = new TriggerBuilder(source.Start);
            Submit = new TriggerBuilder(source.Submit);
            Cancel = new TriggerBuilder(source.Cancel);
            Value = new ValueBuilder(source.Value);
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

        public static SoftwareVersion GetMinimumVersion(Leaderboard leaderboard)
        {
            var minimumVersion = GetMinimumVersion(leaderboard.Format);

            minimumVersion = minimumVersion.OrNewer(leaderboard.Start.MinimumVersion());
            minimumVersion = minimumVersion.OrNewer(leaderboard.Cancel.MinimumVersion());
            minimumVersion = minimumVersion.OrNewer(leaderboard.Submit.MinimumVersion());
            minimumVersion = minimumVersion.OrNewer(leaderboard.Value.MinimumVersion());

            return minimumVersion;
        }

        public static uint GetMaximumAddress(Leaderboard leaderboard)
        {
            var maximumAddress = leaderboard.Start.MaximumAddress();
            maximumAddress = Math.Max(maximumAddress, leaderboard.Cancel.MaximumAddress());
            maximumAddress = Math.Max(maximumAddress, leaderboard.Submit.MaximumAddress());
            maximumAddress = Math.Max(maximumAddress, leaderboard.Value.MaximumAddress());

            return maximumAddress;
        }

        private static SoftwareVersion GetMinimumVersion(ValueFormat format)
        {
            switch (format)
            {
                case ValueFormat.TimeMinutes:
                case ValueFormat.TimeSecsAsMins:
                    return Data.Version._0_77;

                case ValueFormat.Float1:
                case ValueFormat.Float2:
                case ValueFormat.Float3:
                case ValueFormat.Float4:
                case ValueFormat.Float5:
                case ValueFormat.Float6:
                    return Data.Version._1_0;

                case ValueFormat.Thousands:
                case ValueFormat.Hundreds:
                case ValueFormat.Tens:
                case ValueFormat.Fixed1:
                case ValueFormat.Fixed2:
                case ValueFormat.Fixed3:
                    return Data.Version._1_3;

                default:
                    return Data.Version.MinimumVersion;
            }
        }
    }
}
